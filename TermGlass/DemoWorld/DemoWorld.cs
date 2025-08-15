using TermGlass;

namespace TermGlass.DemoWorld;

// =================== DemoWorld (example world source) ===================

public sealed class DemoWorld : IWorldSource
{


    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    private readonly Cell[,] _cells;

    public DemoWorld(int width, int height)
    {
        Width = width; Height = height;
        _cells = new Cell[width, height];

        var chars = " .:-=+*#%@".AsSpan();

        for (var y = 0; y < height; y++)
        {
            // Hue runs left→right, Value (brightness) runs top→bottom
            var v = 0.25 + 0.75 * (height <= 1 ? 0.0 : (double)y / (height - 1)); // 0.25..1.0
            const double s = 1.0;

            for (var x = 0; x < width; x++)
            {
                var h = (width <= 1 ? 0.0 : (double)x / (width - 1)) * 360.0; // 0..360

                // Background: full-spectrum gradient
                var bg = Rgb.FromHsv(h, s, v);

                // Foreground: pick contrasting color (simple luma-based switch)
                // If background is dark, use bright fg; if bright, use dark fg.
                var luma = (int)(0.2126 * bg.R + 0.7152 * bg.G + 0.0722 * bg.B);
                var fg = luma < 140 ? new Rgb(240, 240, 240) : new Rgb(20, 20, 20);

                // Character pattern (deterministic)
                var ch = chars[(x + y) % chars.Length];

                _cells[x, y] = new Cell(ch, fg, bg);
            }
        }
    }

    public Cell? GetCell(int x, int y)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            return _cells[x, y];
        return null;
    }
}