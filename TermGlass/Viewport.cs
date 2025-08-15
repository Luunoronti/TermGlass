namespace Visualization;

// =================== Viewport & math ===================

public sealed class Viewport
{
    // Świat → Ekran: sx = 4 + (wx - OriginX) * Zoom
    // sy = 1 + (wy - OriginY) * Zoom
    public double OriginX { get; private set; } = -40;
    public double OriginY { get; private set; } = -12;
    public double Zoom { get; private set; } = 1.0;

    private Terminal _t;

    public void AttachTerminal(Terminal t) => _t = t;

    // Zwraca indeks komórki świata (int,int) pod danym pikselem ekranu,
    // dla zoom >= 1: najbliższa komórka (round),
    // dla zoom < 1: lewa-górna komórka reprezentowana przez ten piksel.
    public (int ix, int iy) WorldCellUnderScreen(int sx, int sy)
    {
        // współrzędne świata w środku piksela
        double wx = (sx - 4) / Zoom + OriginX;
        double wy = (sy - 1) / Zoom + OriginY;

        if (Zoom >= 1.0)
        {
            return ((int)Math.Round(wx), (int)Math.Round(wy));
        }
        else
        {
            double block = 1.0 / Zoom; // ile komórek świata przypada na 1 znak terminala
                                       // lewy-górny “róg” reprezentowanego bloku wokół środka piksela
            int ix = (int)Math.Floor(wx - block * 0.5);
            int iy = (int)Math.Floor(wy - block * 0.5);
            return (ix, iy);
        }
    }

    public (double wWorld, double hWorld) VisibleWorldSize()
    {
        double wWorld = Math.Max(1.0, (_t.Width - 4) / Zoom);
        double hWorld = Math.Max(1.0, (_t.Height - 2) / Zoom);
        return (wWorld, hWorld);
    }

    // Reset do konkretnego zoomu, tak by punkt ekranu (sx,sy) pozostał zakotwiczony na tym samym punkcie świata.
    public void ResetZoomAroundScreenPoint(int sx, int sy, double newZoom)
    {
        newZoom = Math.Clamp(newZoom, 0.1, 40.0);
        var (wx, wy) = ScreenToWorld(sx, sy);
        double factor = newZoom / Zoom;
        ZoomAround(wx, wy, factor, 0.1, 40.0);
    }

    public (double wx, double wy) ScreenToWorld(int sx, int sy)
    {
        double wx = (sx - 4) / Zoom + OriginX;
        double wy = (sy - 1) / Zoom + OriginY;
        return (wx, wy);
    }

    public (int sx, int sy) WorldToScreen(double wx, double wy)
    {
        int sx = (int)Math.Round(4 + (wx - OriginX) * Zoom);
        int sy = (int)Math.Round(1 + (wy - OriginY) * Zoom);
        return (sx, sy);
    }

    public void SetZoom(double z) => Zoom = z;

    public void ZoomAround(double wx, double wy, double factor, double minZoom, double maxZoom)
    {
        var (sx1, sy1) = WorldToScreen(wx, wy);
        double newZoom = Math.Clamp(Zoom * factor, minZoom, maxZoom);
        if (Math.Abs(newZoom - Zoom) < 1e-9) return;

        double newOriginX = wx - (sx1 - 4) / newZoom;
        double newOriginY = wy - (sy1 - 1) / newZoom;
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
        OriginX = wx - (_t.Width - 4) / (2.0 * Zoom);
        OriginY = wy - (_t.Height - 2) / (2.0 * Zoom);
    }

    public (int x0, int y0, int x1, int y1) WorldRect()
    {
        var (w0x, w0y) = ScreenToWorld(4, 1);
        var (w1x, w1y) = ScreenToWorld(_t.Width - 1, _t.Height - 2);
        int x0 = (int)Math.Floor(Math.Min(w0x, w1x));
        int y0 = (int)Math.Floor(Math.Min(w0y, w1y));
        int x1 = (int)Math.Ceiling(Math.Max(w0x, w1x));
        int y1 = (int)Math.Ceiling(Math.Max(w0y, w1y));
        return (x0, y0, x1, y1);
    }
}