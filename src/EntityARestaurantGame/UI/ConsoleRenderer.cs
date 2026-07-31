namespace EntityARestaurantGame.UI;

/// <summary>
/// Centralises all Console output. Screens must not call Console.Write directly.
/// </summary>
public class ConsoleRenderer
{
    private const int PanelWidth = 70;

    public void Clear() => Console.Clear();

    public void DrawStatusBar(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($" {text}");
        Console.ResetColor();
        Console.WriteLine(new string('─', PanelWidth));
    }

    public void DrawPanel(string title, IEnumerable<string> lines)
    {
        string header = $"┌─ {title} ";
        Console.WriteLine(header + new string('─', Math.Max(0, PanelWidth - header.Length)) + "┐");

        foreach (var line in lines)
        {
            string padded = line.Length > PanelWidth - 4 ? line[..(PanelWidth - 4)] : line;
            Console.WriteLine($"│ {padded.PadRight(PanelWidth - 3)}│");
        }

        Console.WriteLine($"└{new string('─', PanelWidth - 1)}┘");
    }

    public void DrawOptions(IList<MenuOption> options)
    {
        Console.WriteLine();
        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (!opt.IsEnabled)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [{i + 1}] {opt.Label}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"  [{i + 1}] {opt.Label}");
            }

            if (opt.Description != null)
                Console.WriteLine($"      {opt.Description}");
        }
    }

    public void Write(int x, int y, string text)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    public void WriteLine(string text = "") => Console.WriteLine(text);
}
