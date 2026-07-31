namespace EntityARestaurantGame.Scenes;

/// <summary>
/// Finds the game's typeface. Drop any .ttf into data/fonts/ to own the look
/// (first file alphabetically wins); until then we borrow a humanist serif
/// from the OS — a realistic face baked small reads as PS2-era text.
/// </summary>
public static class GameFonts
{
    public static byte[] LoadTtf()
    {
        string fontsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "fonts");
        if (Directory.Exists(fontsDir))
        {
            string? dropped = Directory.GetFiles(fontsDir, "*.ttf").OrderBy(f => f).FirstOrDefault();
            if (dropped is not null)
            {
                Console.WriteLine($"[UI] font: {Path.GetFileName(dropped)}");
                return File.ReadAllBytes(dropped);
            }
        }

        string systemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (string name in new[] { "pala.ttf", "georgia.ttf", "constan.ttf", "times.ttf" })
        {
            string path = Path.Combine(systemFonts, name);
            if (File.Exists(path))
            {
                Console.WriteLine($"[UI] font: {name} (system fallback — drop a .ttf into data/fonts/ to override)");
                return File.ReadAllBytes(path);
            }
        }

        throw new FileNotFoundException("No usable font found. Put a .ttf into data/fonts/.");
    }
}
