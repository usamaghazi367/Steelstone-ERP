namespace ConstFire.Backend;

/// <summary>
/// CLI tools (seed, PDF export, backup) write files here — D: project output on dev machines, else app output folder.
/// </summary>
public static class CliOutputHelper
{
    private const string DefaultWindowsOutput = @"D:\Usama Data\ConstFire\output";

    public static string GetOutputDirectory(IWebHostEnvironment env)
    {
        var fromEnv = Environment.GetEnvironmentVariable("CONSTFIRE_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            var path = Path.GetFullPath(fromEnv.Trim());
            Directory.CreateDirectory(path);
            return path;
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                Directory.CreateDirectory(DefaultWindowsOutput);
                return DefaultWindowsOutput;
            }
            catch
            {
                // fall through
            }
        }

        var fallback = Path.Combine(env.ContentRootPath, "output");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
