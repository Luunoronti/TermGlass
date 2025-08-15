
namespace TermGlass;

// =================== Terminal & buffers ===================

public sealed class CellBuffer
{
    public int Width
    {
        get; private set;
    }
    public int Height
    {
        get; private set;
    }
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
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
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
        {
            _data[x, y] = c; return true;
        }
        return false;
    }

    public Cell this[int x, int y] => _data[x, y];

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
            var fg = replaceChar && top.Ch != ' ' ? top.Fg : cur.Fg;
            _data[x, y] = new Cell(ch, fg, bg);
            return;
        }

        var outBg = Blend(top.Bg, bgAlpha, cur.Bg);

        var newCh = cur.Ch;
        var outFg = cur.Fg;
        if (replaceChar || top.Ch != ' ')
        {
            newCh = top.Ch == '\0' ? cur.Ch : top.Ch;
            outFg = Blend(top.Fg, fgAlpha, cur.Fg);
        }

        _data[x, y] = new Cell(newCh, outFg, outBg);
    }

}