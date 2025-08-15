namespace Visualization;

// =================== Terminal & buffers ===================

public sealed class CellBuffer
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    private Cell[,] _data;

    public bool AlphaBlendEnabled { get; set; } = true;

    public CellBuffer(int w, int h)
    {
        Width = w; Height = h;
        _data = new Cell[w, h];
        Fill(new Cell(' ', Rgb.White, Rgb.Black));
    }

    public void Resize(int w, int h)
    {
        Width = w; Height = h;
        _data = new Cell[w, h];
        Fill(new Cell(' ', Rgb.White, Rgb.Black));
    }

    public void Fill(Cell c)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _data[x, y] = c;
    }

    public void Set(int x, int y, Cell c)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            _data[x, y] = c;
    }
    public bool TrySet(int x, int y, Cell c)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
        { _data[x, y] = c; return true; }
        return false;
    }

    public Cell this[int x, int y] => _data[x, y];


    // Alpha-blend: top nad bottom (alpha 0..255)
    //private static Rgb Blend(Rgb top, byte alpha, Rgb bottom)
    //{
    //    if (alpha >= 255) return top;
    //    if (alpha == 0) return bottom;
    //    int a = alpha;
    //    int ia = 255 - a;
    //    byte r = (byte)((top.R * a + bottom.R * ia) / 255);
    //    byte g = (byte)((top.G * a + bottom.G * ia) / 255);
    //    byte b = (byte)((top.B * a + bottom.B * ia) / 255);
    //    return new Rgb(r, g, b);
    //}


    // existing
    private static Rgb Blend(Rgb top, byte alpha, Rgb bottom)
    {
        if (alpha >= 255) return top;
        if (alpha == 0) return bottom;
        int a = alpha, ia = 255 - a;
        return new Rgb(
            (byte)((top.R * a + bottom.R * ia) / 255),
            (byte)((top.G * a + bottom.G * ia) / 255),
            (byte)((top.B * a + bottom.B * ia) / 255));
    }

    // existing
    public void BlendBg(int x, int y, Rgb bg, byte alpha)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        var cur = _data[x, y];

        if (!AlphaBlendEnabled)
        {
            // No transparency in Console16 → treat as opaque BG paint, keep char/fg
            _data[x, y] = new Cell(cur.Ch, cur.Fg, bg);
            return;
        }

        var outBg = Blend(bg, alpha, cur.Bg);
        _data[x, y] = new Cell(cur.Ch, cur.Fg, outBg);
    }

    // existing
    public void BlendBgAndFg(int x, int y, Rgb bg, byte bgAlpha, Rgb fgTint, byte fgAlpha)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        var cur = _data[x, y];

        if (!AlphaBlendEnabled)
        {
            // Opaque paint: set BG, simple FG tint disabled (keep original fg)
            _data[x, y] = new Cell(cur.Ch, cur.Fg, bg);
            return;
        }

        var outBg = Blend(bg, bgAlpha, cur.Bg);
        var outFg = Blend(fgTint, fgAlpha, cur.Fg);
        _data[x, y] = new Cell(cur.Ch, outFg, outBg);
    }

    // existing
    public void BlendCell(int x, int y, Cell top, byte fgAlpha = 255, byte bgAlpha = 255, bool replaceChar = true)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        var cur = _data[x, y];

        if (!AlphaBlendEnabled)
        {
            // Opaque overlay semantics in Console16:
            var ch = replaceChar && top.Ch != '\0' ? top.Ch : cur.Ch;
            // Use top bg fully; use top fg fully if we replace char, else keep fg
            var bg = top.Bg;
            var fg = (replaceChar && top.Ch != ' ') ? top.Fg : cur.Fg;
            _data[x, y] = new Cell(ch, fg, bg);
            return;
        }

        var outBg = Blend(top.Bg, bgAlpha, cur.Bg);

        char newCh = cur.Ch;
        Rgb outFg = cur.Fg;
        if (replaceChar || top.Ch != ' ')
        {
            newCh = top.Ch == '\0' ? cur.Ch : top.Ch;
            outFg = Blend(top.Fg, fgAlpha, cur.Fg);
        }

        _data[x, y] = new Cell(newCh, outFg, outBg);
    }



    ///// <summary>
    ///// Zmieszaj TŁO komórki kolorem 'bg' i dodatkowo ztintuj KOLOR ZNAKU 'fg' kolorem 'fgTint'.
    ///// Znak (char) pozostaje bez zmian.
    ///// </summary>
    //public void BlendBgAndFg(int x, int y, Rgb bg, byte bgAlpha, Rgb fgTint, byte fgAlpha)
    //{
    //    if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
    //    var cur = _data[x, y];
    //    var outBg = Blend(bg, bgAlpha, cur.Bg);
    //    var outFg = Blend(fgTint, fgAlpha, cur.Fg);
    //    _data[x, y] = new Cell(cur.Ch, outFg, outBg);
    //}

    ///// <summary>
    ///// Zmieszaj tylko TŁO w (x,y) z podanym kolorem i alphą (0..255).
    ///// Nie zmienia znaku ani koloru tekstu.
    ///// </summary>
    //public void BlendBg(int x, int y, Rgb bg, byte alpha)
    //{
    //    if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
    //    var cur = _data[x, y];
    //    _data[x, y] = new Cell(cur.Ch, cur.Fg, Blend(bg, alpha, cur.Bg));
    //}

    ///// <summary>
    ///// Nałóż komórkę z alphą (osobno dla fg i bg).
    ///// Jeśli replaceChar=false i ch==' ', pozostaw znak spod spodu.
    ///// </summary>
    //public void BlendCell(int x, int y, Cell top, byte fgAlpha = 255, byte bgAlpha = 255, bool replaceChar = true)
    //{
    //    if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
    //    var cur = _data[x, y];

    //    var outBg = Blend(top.Bg, bgAlpha, cur.Bg);

    //    char ch = cur.Ch;
    //    Rgb outFg = cur.Fg;

    //    if (replaceChar || top.Ch != ' ')
    //    {
    //        ch = top.Ch == '\0' ? cur.Ch : top.Ch; // '\0' = brak zmiany znaku
    //        outFg = Blend(top.Fg, fgAlpha, cur.Fg);
    //    }

    //    _data[x, y] = new Cell(ch, outFg, outBg);
    //}
}