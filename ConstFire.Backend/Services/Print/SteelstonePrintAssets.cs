namespace ConstFire.Backend.Services.Print;

internal static class SteelstonePrintAssets
{
    public static byte[]? TryLoadLogoBytes()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Print", "steelstone-logo-blue.jpg"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Print", "steelstone-logo-blue.jpg"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        return null;
    }
}
