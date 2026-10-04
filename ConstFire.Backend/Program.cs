using System.Text;
using ConstFire.Backend;
using ConstFire.Backend.Data;
using ConstFire.Backend.Middleware;
using ConstFire.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Production.local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IModuleService, ModuleService>();
builder.Services.AddScoped<IFieldOptionsService, FieldOptionsService>();
builder.Services.AddScoped<IDataBackupService, DataBackupService>();
builder.Services.AddScoped<IFormattedExcelExportService, FormattedExcelExportService>();
builder.Services.AddScoped<ConstFire.Backend.Services.Print.IRecordPrintPdfService, ConstFire.Backend.Services.Print.RecordPrintPdfService>();
builder.Services.AddScoped<ExcelDummyDataSeeder>();
builder.Services.AddHostedService<DbSeedHostedService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtSettingsHelper.CreateValidationParameters(configuration);
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrEmpty(context.Token))
                    return Task.CompletedTask;

                var authHeader = context.Request.Headers.Authorization.FirstOrDefault()
                    ?? context.Request.Headers["X-Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) &&
                    authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = authHeader["Bearer ".Length..].Trim();
                    return Task.CompletedTask;
                }

                var cookieToken = context.Request.Cookies["steelstone_auth"];
                if (!string.IsNullOrEmpty(cookieToken))
                    context.Token = cookieToken;

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                var uri = new Uri(origin);
                return uri.Host is "localhost" or "127.0.0.1"
                    || uri.Host.EndsWith(".itempurl.com", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".smarterasp.net", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".ookpro.com", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Serve Vue SPA from wwwroot (production / SmarterASP)
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

if (args.Contains("--jwt-validate-test", StringComparer.OrdinalIgnoreCase))
{
    var token = Environment.GetEnvironmentVariable("JWT_TEST_TOKEN");
    if (string.IsNullOrWhiteSpace(token))
    {
        Console.Error.WriteLine("Set JWT_TEST_TOKEN to a bearer token to validate.");
        return;
    }

    var handler = new JwtSecurityTokenHandler();
    try
    {
        handler.ValidateToken(token, JwtSettingsHelper.CreateValidationParameters(builder.Configuration), out _);
        Console.WriteLine("JWT valid with current configuration.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("JWT invalid: " + ex.GetType().Name + " — " + ex.Message);
        Console.WriteLine("Configured Issuer=" + builder.Configuration["Jwt:Issuer"]
            + " Audience=" + builder.Configuration["Jwt:Audience"]
            + " KeyLength=" + (builder.Configuration["Jwt:Key"] ?? "").Length);
    }

    return;
}

if (args.Contains("--seed-excel-dummy", StringComparer.OrdinalIgnoreCase))
{
    var fileArg = args.FirstOrDefault(a => a.StartsWith("--file=", StringComparison.OrdinalIgnoreCase));
    var envForCli = app.Services.GetRequiredService<IWebHostEnvironment>();
    var workbookPath = fileArg?["--file=".Length..].Trim('"')
        ?? Path.Combine(CliOutputHelper.GetOutputDirectory(envForCli), "TEST DATE.xlsx");

    var clear = !args.Contains("--merge", StringComparer.OrdinalIgnoreCase);

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ExcelDummyDataSeeder>();
    var result = await seeder.ImportAsync(workbookPath, clearExisting: clear);
    Console.WriteLine($"Excel dummy import complete: {result.RecordsInserted} records from {workbookPath}");
    foreach (var (code, count) in result.CountByModuleCode.OrderBy(k => k.Key))
        Console.WriteLine($"  Module {code}: {count}");
    if (result.SkippedSheets.Count > 0)
        Console.WriteLine("Skipped sheets: " + string.Join(", ", result.SkippedSheets));
    return;
}

if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    await DbSeeder.SeedAsync(db, env);
    Console.WriteLine("Database migration and seed completed.");
    return;
}

if (args.Contains("--db-stats", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var modules = await db.ErpModules
        .AsNoTracking()
        .OrderBy(m => m.Code)
        .Select(m => new
        {
            m.Code,
            m.Name,
            RecordCount = m.Records.Count,
        })
        .ToListAsync();
    var total = await db.ErpRecords.CountAsync();
    var users = await db.Users.CountAsync();
    var fields = await db.ErpModuleFields.CountAsync();
    var options = await db.ErpFieldOptions.CountAsync();
    Console.WriteLine($"Total ErpRecords (form/business data): {total}");
    Console.WriteLine($"Users: {users}, Module field definitions: {fields}, Lookup options (banks): {options}");
    Console.WriteLine();
    foreach (var m in modules)
        Console.WriteLine($"  Module {m.Code} ({m.Name}): {m.RecordCount} records");
    return;
}

if (args.Contains("--export-formatted", StringComparer.OrdinalIgnoreCase))
{
    var outputArg = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
    var envForCli = app.Services.GetRequiredService<IWebHostEnvironment>();
    var outputPath = outputArg?["--output=".Length..]
        ?? Path.Combine(
            CliOutputHelper.GetOutputDirectory(envForCli),
            $"steelstone-erp-forms-preview-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");

    using var scope = app.Services.CreateScope();
    var export = scope.ServiceProvider.GetRequiredService<IFormattedExcelExportService>();
    var bytes = await export.ExportAsync();
    var fullPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    await File.WriteAllBytesAsync(fullPath, bytes);
    Console.WriteLine($"Formatted workbook written: {fullPath}");
    Console.WriteLine($"Size: {bytes.Length / 1024.0:F1} KB");
    return;
}

if (args.Contains("--inspect-record", StringComparer.OrdinalIgnoreCase))
{
    var codeArg = args.FirstOrDefault(a => a.StartsWith("--code=", StringComparison.OrdinalIgnoreCase));
    var idArg = args.FirstOrDefault(a => a.StartsWith("--id=", StringComparison.OrdinalIgnoreCase));
    var moduleCode = codeArg?["--code=".Length..].Trim() ?? "11";
    if (!int.TryParse(idArg?["--id=".Length..], out var recordId))
    {
        Console.Error.WriteLine("Usage: --inspect-record --code=11 --id=123");
        return;
    }

    using var scope = app.Services.CreateScope();
    var modules = scope.ServiceProvider.GetRequiredService<IModuleService>();
    var record = await modules.GetRecordAsync(moduleCode, recordId);
    if (record is null)
    {
        Console.Error.WriteLine("Record not found.");
        return;
    }

    Console.WriteLine($"Record {recordId} ({record.RecordCode}) module {moduleCode}:");
    foreach (var key in record.Data.Keys.OrderBy(k => k, StringComparer.Ordinal))
    {
        if (key.StartsWith('_')) continue;
        Console.WriteLine($"  {key} = {record.Data[key]}");
    }

    return;
}

if (args.Contains("--print-record-pdf", StringComparer.OrdinalIgnoreCase))
{
    var codeArg = args.FirstOrDefault(a => a.StartsWith("--code=", StringComparison.OrdinalIgnoreCase));
    var idArg = args.FirstOrDefault(a => a.StartsWith("--id=", StringComparison.OrdinalIgnoreCase));
    var outputArg = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));

    var moduleCode = codeArg?["--code=".Length..].Trim() ?? "09";
    int? recordId = int.TryParse(idArg?["--id=".Length..], out var parsedId) ? parsedId : null;
    var usePreviewSample = args.Contains("--preview", StringComparer.OrdinalIgnoreCase);

    using var scope = app.Services.CreateScope();
    var print = scope.ServiceProvider.GetRequiredService<ConstFire.Backend.Services.Print.IRecordPrintPdfService>();

    (byte[] Pdf, string FileName)? generated;
    if (usePreviewSample && moduleCode == "02")
    {
        var modules = scope.ServiceProvider.GetRequiredService<IModuleService>();
        var envForPrint = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var module = await modules.GetModuleAsync(moduleCode);
        if (module is null)
        {
            Console.Error.WriteLine($"Module {moduleCode} not found.");
            return;
        }

        var config = ModuleConfigHelper.LoadConfig(envForPrint, moduleCode);
        var pdf = ConstFire.Backend.Services.Print.SteelstoneControlledPrintComposer.BuildPreviewSampleModule02(module, config);
        generated = (pdf, "steelstone-manufacturer-registration-preview-mfr-00021.pdf");
    }
    else
    {
        generated = recordId is int rid
            ? await print.GenerateAsync(moduleCode, rid)
            : await print.GenerateFirstRecordSampleAsync(moduleCode);
    }

    if (generated is null)
    {
        Console.Error.WriteLine($"No PDF generated for module {moduleCode}" + (recordId is int ? $" record {recordId}" : " (no records found)."));
        return;
    }

    var (bytes, fileName) = generated.Value;
    var envForCli = app.Services.GetRequiredService<IWebHostEnvironment>();
    var outputPath = outputArg?["--output=".Length..]
        ?? Path.Combine(CliOutputHelper.GetOutputDirectory(envForCli), fileName);
    var fullPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    await File.WriteAllBytesAsync(fullPath, bytes);
    Console.WriteLine($"Record PDF written: {fullPath}");
    Console.WriteLine($"Size: {bytes.Length / 1024.0:F1} KB");
    return;
}

if (args.Contains("--export-backup", StringComparer.OrdinalIgnoreCase))
{
    var outputArg = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
    var envForCli = app.Services.GetRequiredService<IWebHostEnvironment>();
    var outputPath = outputArg?["--output=".Length..]
        ?? Path.Combine(
            CliOutputHelper.GetOutputDirectory(envForCli),
            $"steelstone-erp-backup-preview-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");

    using var scope = app.Services.CreateScope();
    var backup = scope.ServiceProvider.GetRequiredService<IDataBackupService>();
    var bytes = await backup.ExportExcelAsync();
    var fullPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    await File.WriteAllBytesAsync(fullPath, bytes);
    Console.WriteLine($"Backup written: {fullPath}");
    Console.WriteLine($"Size: {bytes.Length / 1024.0:F1} KB");
    return;
}

app.Run();
