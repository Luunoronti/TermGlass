namespace TermGlass.Core;


// =================== Viewport & math ===================

public sealed class Viewport : IViewport
{
    // Świat → Ekran: sx = 4 + (wx - OriginX) * Zoom
    // sy = 1 + (wy - OriginY) * Zoom
    public double OriginX { get; private set; } = -40;
    public double OriginY { get; private set; } = -12;
    public double Zoom { get; private set; } = 1.0;

    private Terminal? _t;

    public void AttachTerminal(Terminal t) => _t = t;

    // Zwraca indeks komórki świata (int,int) pod danym pikselem ekranu,
    // dla zoom >= 1: najbliższa komórka (round),
    // dla zoom < 1: lewa-górna komórka reprezentowana przez ten piksel.
    public (int ix, int iy) WorldCellUnderScreen(int sx, int sy)
    {
        // współrzędne świata w środku piksela
        var wx = (sx - 4) / Zoom + OriginX;
        var wy = (sy - 1) / Zoom + OriginY;

        if (Zoom >= 1.0)
        {
            return ((int)Math.Round(wx), (int)Math.Round(wy));
        }
        else
        {

            /* Unmerged change from project 'TermGlass (net9.0)'
            Before:
                        double block = 1.0 / Zoom; // ile komórek świata przypada na 1 znak terminala
                                                   // lewy-górny “róg” reprezentowanego bloku wokół środka piksela
                        int ix = (int)Math.Floor(wx - block * 0.5);
                        int iy = (int)Math.Floor(wy - block * 0.5);
                        return (ix, iy);
            After:
                        var block = 1.0 / Zoom; // ile komórek świata przypada na 1 znak terminala
                                                   // lewy-górny “róg” reprezentowanego bloku wokół środka piksela
                        var ix = (int)Math.Floor(wx - block * 0.5);
                        var iy = (int)Math.Floor(wy - block * 0.5);
                        return (ix, iy);
            */
            var block = 1.0 / Zoom; // ile komórek świata przypada na 1 znak terminala
                                    // lewy-górny “róg” reprezentowanego bloku wokół środka piksela
            var ix = (int)Math.Floor(wx - block * 0.5);
            var iy = (int)Math.Floor(wy - block * 0.5);
            return (ix, iy);
        }
    }

    public (double wWorld, double hWorld) VisibleWorldSize()
    {
        var wWorld = Math.Max(1.0, (_t?.Width ?? 0 - 4) / Zoom);
        var hWorld = Math.Max(1.0, (_t?.Height ?? 0 - 2) / Zoom);
        return (wWorld, hWorld);
    }

    // Reset do konkretnego zoomu, tak by punkt ekranu (sx,sy) pozostał zakotwiczony na tym samym punkcie świata.
    public void ResetZoomAroundScreenPoint(int sx, int sy, double newZoom)
    {
        newZoom = Math.Clamp(newZoom, 0.1, 40.0);
        var (wx, wy) = ScreenToWorld(sx, sy);
        var factor = newZoom / Zoom;
        ZoomAround(wx, wy, factor, 0.1, 40.0);
    }

    public (double wx, double wy) ScreenToWorld(int sx, int sy)
    {
        var wx = (sx - 4) / Zoom + OriginX;
        var wy = (sy - 1) / Zoom + OriginY;
        return (wx, wy);
    }

    public (int sx, int sy) WorldToScreen(double wx, double wy)
    {
        var sx = (int)Math.Round(4 + (wx - OriginX) * Zoom);
        var sy = (int)Math.Round(1 + (wy - OriginY) * Zoom);
        return (sx, sy);
    }

    public void SetZoom(double z) => Zoom = z;

    public void ZoomAround(double wx, double wy, double factor, double minZoom, double maxZoom)
    {
        var (sx1, sy1) = WorldToScreen(wx, wy);
        var newZoom = Math.Clamp(Zoom * factor, minZoom, maxZoom);
        if (Math.Abs(newZoom - Zoom) < 1e-9) return;

        var newOriginX = wx - (sx1 - 4) / newZoom;
        var newOriginY = wy - (sy1 - 1) / newZoom;
        Zoom = newZoom;
        OriginX = newOriginX;
        OriginY = newOriginY;
    }

    public void Offset(double dx, double dy)
    {
        OriginX += dx;
        OriginY += dy;
    }

    public void CenterOn(double wx, double wy)
    {
        OriginX = wx - (_t?.Width ?? 0 - 4) / (2.0 * Zoom);
        OriginY = wy - (_t?.Height ?? 0 - 2) / (2.0 * Zoom);
    }

    public (int x0, int y0, int x1, int y1) WorldRect()
    {
        var (w0x, w0y) = ScreenToWorld(4, 1);
        var (w1x, w1y) = ScreenToWorld(_t?.Width ?? 0 - 1, _t?.Height ?? 0 - 2);
        var x0 = (int)Math.Floor(Math.Min(w0x, w1x));
        var y0 = (int)Math.Floor(Math.Min(w0y, w1y));
        var x1 = (int)Math.Ceiling(Math.Max(w0x, w1x));
        var y1 = (int)Math.Ceiling(Math.Max(w0y, w1y));
        return (x0, y0, x1, y1);
    }

    public double OffsetX => OriginX;
    public double OffsetY => OriginY;

    (int wx, int wy) IViewport.ScreenToWorld(int sx, int sy)
    {
        var (wx, wy) = ScreenToWorld(sx, sy);
        return ((int)wx, (int)wy);
    }
    public (int sx, int sy) WorldToScreen(int wx, int wy)
    {
        var (sx, sy) = WorldToScreen(wx, wy);
        return ((int)sx, (int)sy);
    }
}