namespace BattleCatCalc.Infrastructure.Tests;

/// <summary>
/// Resolves paths to shared HTML fixture files (from Python test suite).
/// </summary>
internal static class FixtureHelper
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string LoadHtml(string fileName)
    {
        var path = Path.Combine(FixtureDir, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture not found: {path}");
        return File.ReadAllText(path);
    }
}
