using Microsoft.Data.SqlClient;

var settingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "migrate-db.settings.json");
if (!File.Exists(settingsPath))
    settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "migrate-db.settings.json");

if (!File.Exists(settingsPath))
{
    Console.Error.WriteLine("Missing migrate-db.settings.json (Source + Destination connection strings).");
    return 1;
}

var json = await File.ReadAllTextAsync(settingsPath);
using var doc = System.Text.Json.JsonDocument.Parse(json);
var sourceCs = doc.RootElement.GetProperty("Source").GetString()
    ?? throw new InvalidOperationException("Source connection missing");
var destCs = doc.RootElement.GetProperty("Destination").GetString()
    ?? throw new InvalidOperationException("Destination connection missing");

var migrate = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);

await using var source = new SqlConnection(sourceCs);
await using var dest = new SqlConnection(destCs);
await source.OpenAsync();
await dest.OpenAsync();

Console.WriteLine("Connected to SOURCE and DESTINATION.");
await PrintStatsAsync(source, "SOURCE (old)");
await PrintStatsAsync(dest, "DESTINATION (new)");

if (!migrate)
{
    Console.WriteLine();
    Console.WriteLine("Dry run only. To copy ErpRecords into destination, run:");
    Console.WriteLine("  dotnet run --project tools-seed.csproj -- --migrate");
    return 0;
}

var copied = await CopyRecordsAsync(source, dest);
Console.WriteLine();
Console.WriteLine($"Migration finished. ErpRecords copied/updated: {copied}");
await PrintStatsAsync(dest, "DESTINATION (after)");
return 0;

static async Task PrintStatsAsync(SqlConnection conn, string label)
{
    Console.WriteLine();
    Console.WriteLine($"--- {label} ---");
    var users = await ScalarAsync(conn, "SELECT COUNT(1) FROM Users");
    var records = await ScalarAsync(conn, "SELECT COUNT(1) FROM ErpRecords");
    Console.WriteLine($"Users: {users}, ErpRecords: {records}");
    await using var cmd = new SqlCommand(
        """
        SELECT m.Code, COUNT(r.Id) AS Cnt
        FROM ErpModules m
        LEFT JOIN ErpRecords r ON r.ModuleId = m.Id
        GROUP BY m.Code
        ORDER BY m.Code
        """, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        Console.WriteLine($"  Module {reader.GetString(0)}: {reader.GetInt32(1)} records");
}

static async Task<int> CopyRecordsAsync(SqlConnection source, SqlConnection dest)
{
    var destModuleIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    await using (var cmd = new SqlCommand("SELECT Id, Code FROM ErpModules", dest))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
            destModuleIds[reader.GetString(1)] = reader.GetInt32(0);
    }

    var select = new SqlCommand(
        """
        SELECT m.Code, r.DataJson, r.CreatedAt, r.UpdatedAt, r.RecordCode
        FROM ErpRecords r
        INNER JOIN ErpModules m ON m.Id = r.ModuleId
        ORDER BY r.Id
        """, source);

    var copied = 0;
    await using var rows = await select.ExecuteReaderAsync();
    while (await rows.ReadAsync())
    {
        var code = rows.GetString(0);
        if (!destModuleIds.TryGetValue(code, out var destModuleId))
        {
            Console.WriteLine($"Skip module {code}: not found on destination (run app seed first).");
            continue;
        }

        var dataJson = rows.GetString(1);
        var createdAt = rows.GetDateTime(2);
        var updatedAt = rows.GetDateTime(3);
        var recordCode = rows.IsDBNull(4) ? null : rows.GetString(4);

        if (!string.IsNullOrEmpty(recordCode))
        {
            var exists = await ScalarAsync(dest,
                "SELECT COUNT(1) FROM ErpRecords WHERE ModuleId=@m AND RecordCode=@c",
                ("@m", destModuleId), ("@c", recordCode));
            if (exists > 0)
                continue;
        }

        await using var ins = new SqlCommand(
            """
            INSERT INTO ErpRecords (ModuleId, DataJson, CreatedAt, UpdatedAt, RecordCode)
            VALUES (@m, @j, @ca, @ua, @rc)
            """, dest);
        ins.Parameters.AddWithValue("@m", destModuleId);
        ins.Parameters.AddWithValue("@j", dataJson);
        ins.Parameters.AddWithValue("@ca", createdAt);
        ins.Parameters.AddWithValue("@ua", updatedAt);
        ins.Parameters.AddWithValue("@rc", (object?)recordCode ?? DBNull.Value);
        await ins.ExecuteNonQueryAsync();
        copied++;
    }

    return copied;
}

static async Task<int> ScalarAsync(SqlConnection conn, string sql, params (string Name, object Value)[] parameters)
{
    await using var cmd = new SqlCommand(sql, conn);
    foreach (var (name, value) in parameters)
        cmd.Parameters.AddWithValue(name, value);
    return (int)(await cmd.ExecuteScalarAsync() ?? 0);
}
