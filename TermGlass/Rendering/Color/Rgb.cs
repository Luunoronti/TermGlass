using System;

namespace TermGlass;

// Simple TrueColor colors helper
public readonly record struct Rgb(byte R, byte G, byte B)
{

    // ---------- ANSI-16 palette (RGB) ----------
    public static readonly Rgb[] Ansi16Palette =
    {
    new(  0,   0,   0), // 0  black
    new(128,   0,   0), // 1  red
    new(  0, 128,   0), // 2  green
    new(128, 128,   0), // 3  yellow
    new(  0,   0, 128), // 4  blue
    new(128,   0, 128), // 5  magenta
    new(  0, 128, 128), // 6  cyan
    new(192, 192, 192), // 7  white (light gray)
    new(128, 128, 128), // 8  bright black (dark gray)
    new(255,   0,   0), // 9  bright red
    new(  0, 255,   0), // 10 bright green
    new(255, 255,   0), // 11 bright yellow
    new(  0,   0, 255), // 12 bright blue
    new(255,   0, 255), // 13 bright magenta
    new(  0, 255, 255), // 14 bright cyan
    new(255, 255, 255), // 15 bright white
};

    public static Rgb Transparent => new(0, 0, 0);
    public static Rgb Black => new(0, 0, 0);
    public static Rgb White => new(255, 255, 255);
    public static Rgb Gray => new(180, 180, 180);
    public static Rgb Yellow => new(255, 220, 0);
    public static Rgb Red => new(220, 40, 40);
    public static Rgb Green => new(40, 200, 120);
    public static Rgb Blue => new(60, 120, 220);

    public static Rgb Lerp(Rgb a, Rgb b, double t) =>
        new((byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));


    /// <summary>Nearest index in ANSI-16 palette (0..15).</summary>
    public int ToAnsi16Index() => NearestAnsi16Index(this);

    // ---------- HSV → RGB ----------
    public static Rgb FromHsv(double h, double s, double v)
    {
        // h∈[0,360), s∈[0,1], v∈[0,1]
        if (s <= 0.0)
        {
            var g = (byte)Math.Round(v * 255.0);
            return new Rgb(g, g, g);
        }

        h = (h % 360.0 + 360.0) % 360.0;
        var c = v * s;
        var x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
        var m = v - c;

        double r1 = 0, g1 = 0, b1 = 0;
        switch ((int)(h / 60.0))
        {
            case 0: r1 = c; g1 = x; b1 = 0; break;
            case 1: r1 = x; g1 = c; b1 = 0; break;
            case 2: r1 = 0; g1 = c; b1 = x; break;
            case 3: r1 = 0; g1 = x; b1 = c; break;
            case 4: r1 = x; g1 = 0; b1 = c; break;
            default: r1 = c; g1 = 0; b1 = x; break; // sector 5
        }
        return new Rgb(
            (byte)Math.Round((r1 + m) * 255.0),
            (byte)Math.Round((g1 + m) * 255.0),
            (byte)Math.Round((b1 + m) * 255.0));
    }

    // Instance/Static: find nearest ANSI-16 index by Euclidean distance in RGB
    public int NearestAnsi16Index() => NearestAnsi16Index(this);
    public static int NearestAnsi16Index(Rgb c)
    {
        var best = 0;
        var bestD = int.MaxValue;
        for (var i = 0; i < Ansi16Palette.Length; i++)
        {
            var p = Ansi16Palette[i];
            int dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
            var d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // From ANSI-16 index back to RGB
    public static Rgb FromAnsi16Index(int idx)
    {
        if ((uint)idx >= (uint)Ansi16Palette.Length) idx = 0;
        return Ansi16Palette[idx];
    }

    /// <summary>Mapping to System.ConsoleColor (uses ANSI-16 index).</summary>
    public ConsoleColor ToConsoleColor()
    {
        // Map consistent with Ansi16Palette order:
        // 0..15 -> Black, DarkRed, DarkGreen, DarkYellow, DarkBlue, DarkMagenta, DarkCyan, Gray,
        //          DarkGray, Red, Green, Yellow, Blue, Magenta, Cyan, White
        return (ConsoleColor)ToAnsi16Index();
    }

    /// <summary>RGB corresponding to given ConsoleColor (exact values from ANSI-16 palette).</summary>
    public static Rgb FromConsoleColor(ConsoleColor color)
    {
        var idx = (int)color;
        if ((uint)idx >= (uint)Ansi16Palette.Length) idx = 0;
        return Ansi16Palette[idx];
    }


    // --- Conversion operators ---

    /// <summary>Explicit conversion Rgb -> ConsoleColor.</summary>
    public static explicit operator ConsoleColor(Rgb c) => c.ToConsoleColor();

    /// <summary>Implicit conversion ConsoleColor -> Rgb.</summary>
    public static implicit operator Rgb(ConsoleColor color) => FromConsoleColor(color);

}