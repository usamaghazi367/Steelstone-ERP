namespace ConstFire.Backend;

internal static class CliOutputHelper
{
    /// <summary>
    /// Artifacts (PDF, ZIP exports) go under D:\Usama Data\ConstFire\output — not C: Downloads.
    /// </summary>
    public static string GetOutputDirectory(IWebHostEnvironment env)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, ".."));
        var outputDir = Path.Combine(projectRoot, "output");
        Directory.CreateDirectory(outputDir);
        return outputDir;
    }
}
