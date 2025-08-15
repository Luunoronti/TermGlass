using TermGlass.Rendering.Color;

namespace TermGlass.Rendering.Buffer;

public readonly record struct Cell(char Ch, Rgb Fg, Rgb Bg);