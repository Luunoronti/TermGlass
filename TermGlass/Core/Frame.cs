namespace TermGlass;

// Drawing in world vs. screen coordinates
public sealed class Frame
{
    private readonly Terminal _t;
    private readonly Viewport _vp;
    private readonly CellBuffer _buf;
    public readonly InputState Input;
    public readonly VizConfig Cfg;

    public Frame(Terminal t, Viewport vp, CellBuffer buf, InputState input, VizConfig cfg)
    {
        _t = t; _vp = vp; _buf = buf; Input = input; Cfg = cfg;
    }

    // Transformations
    public (double wx, double wy) ScreenToWorld(int sx, int sy) => _vp.ScreenToWorld(sx, sy);
    public (int sx, int sy) WorldToScreen(double wx, double wy) => _vp.WorldToScreen(wx, wy);

    // Drawing the world map by sampling the viewport window
    public void DrawWorld(IWorldSource world)
    {
        int W = _t.Width, H = _t.Height;
        for (var sy = 0; sy < H; sy++)
        {
            for (var sx = 0; sx < W; sx++)
            {
                var (wx, wy) = _vp.ScreenToWorld(sx, sy);

                // if the world has boundaries – safely clamp / check
                if (wx < 0 || wy < 0 || wx >= world.Width || wy >= world.Height)
                    continue;

                var cell = world.GetCell((int)wx, (int)wy)!.Value;
                _buf.TrySet(sx, sy, cell);
            }
        }
    }

    // (Overlays) — we leave the interface for drawing from the outside
    public void DrawRectWorld(double x, double y, double w, double h, char ch, Rgb fg, Rgb bg)
        => Renderer.DrawRectWorld(_buf, _vp, x, y, w, h, ch, fg, bg, Cfg.Layers.HasFlag(UiLayers.Overlays));

    public void DrawCircleWorld(double cx, double cy, double r, char ch, Rgb fg, Rgb bg)
        => Renderer.DrawCircleWorld(_buf, _vp, cx, cy, r, ch, fg, bg, Cfg.Layers.HasFlag(UiLayers.Overlays));

    public void DrawTextScreen(int sx, int sy, string text, Rgb fg, Rgb bg)
        => Renderer.DrawTextScreen(_buf, sx, sy, text, fg, bg, Cfg.Layers.HasFlag(UiLayers.Overlays));
}
