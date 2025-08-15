using TermGlass;

namespace TermGlass;

public sealed class Window
{
    // ---- instance properties ----
    public int X
    {
        get; set;
    }
    public int Y
    {
        get; set;
    }
    public int W
    {
        get; set;
    }
    public int H
    {
        get; set;
    }

    public bool Draggable { get; set; } = true;
    public bool ShowCloseButton { get; set; } = true;
    public Rgb BorderColorActive { get; set; } = new Rgb(255, 230, 120);
    public Action<Window>? OnClose
    {
        get; set;
    }  // close callback (optional)

    public Rgb BgColor { get; set; } = new Rgb(20, 20, 20);
    public byte BgAlpha { get; set; } = 180;          // 255 = opaque background
    public Rgb BorderColor { get; set; } = new Rgb(255, 255, 255);
    public byte BorderAlpha { get; set; } = 220;      // 255 = opaque border
    public Rgb TextColor { get; set; } = new Rgb(245, 245, 245);

    public bool Visible { get; set; } = true;
    public int Z { get; set; } = 0;                   // z-index: larger values are drawn later (on top)

    /// <summary>Optional content renderer; invoked after background/border.</summary>
    internal Action<CellBuffer, Window>? Content
    {
        get; set;
    }

    // ---- static manager ----
    private static readonly List<Window> s_windows = new();

    private static Window? s_active;
    private static Window? s_dragging;
    private static int s_dragMouseStartX, s_dragMouseStartY;
    private static int s_dragWinStartX, s_dragWinStartY;
    private static bool s_prevMouseLeftDown;
    private static bool s_clickOnClose; // true when mousedown occurred on [X]

    public static bool IsDragging => s_dragging != null;
    public static IReadOnlyList<Window> All() => s_windows;



    // Topmost (by Z) visible window under point (mx,my)
    private static Window? HitTestTop(int mx, int my)
    {
        Window? best = null;
        var bestZ = int.MinValue;
        foreach (var w in s_windows)
        {
            if (!w.Visible) continue;
            if (mx >= w.X && mx < w.X + w.W && my >= w.Y && my < w.Y + w.H)
            {
                if (w.Z >= bestZ) { best = w; bestZ = w.Z; }
            }
        }
        return best;
    }

    // close button area: "[X]" at the top-right corner of the frame
    private (int x0, int x1)? CloseButtonSpan()
    {
        if (!ShowCloseButton || W < 5) return null;
        var x1 = X + W - 2;      // ']' at X+W-2
        var x0 = X + W - 4;      // '[' at X+W-4, 'X' at X+W-3
        return (x0, x1);
    }
    private bool IsInCloseButton(int mx, int my)
    {
        var span = CloseButtonSpan();
        if (span == null) return false;
        return my == Y && mx >= span.Value.x0 && mx <= span.Value.x1;
    }
    private bool IsInTitleBar(int mx, int my)
    {
        if (!Draggable) return false;
        if (my != Y) return false;
        return mx >= X && mx < X + W;
    }


    // Bring window to front (increase Z so it draws last)
    private static void BringToFront(Window w)
    {
        var maxZ = 0;
        foreach (var ww in s_windows) if (ww.Z > maxZ) maxZ = ww.Z;
        w.Z = maxZ + 1;
    }

    // Ensure the window doesn't go off-screen
    private static void ClampToScreen(Window w, int screenW, int screenH)
    {
        if (w.W > screenW) { w.X = 0; w.W = screenW; }
        else
        {
            if (w.X < 0) w.X = 0;
            if (w.X + w.W > screenW) w.X = Math.Max(0, screenW - w.W);
        }

        if (w.H > screenH) { w.Y = 0; w.H = screenH; }
        else
        {
            if (w.Y < 0) w.Y = 0;
            if (w.Y + w.H > screenH) w.Y = Math.Max(0, screenH - w.H);
        }
    }

    public static Window Create(int x, int y, int w, int h,
                                Rgb? bg = null, byte? bgAlpha = null,
                                Rgb? border = null, byte? borderAlpha = null,
                                int z = 0,
                                Action<CellBuffer, Window>? content = null)
    {
        var win = new Window
        {
            X = x,
            Y = y,
            W = w,
            H = h,
            BgColor = bg ?? new Rgb(20, 20, 20),
            BgAlpha = bgAlpha ?? 180,
            BorderColor = border ?? new Rgb(255, 255, 255),
            BorderAlpha = borderAlpha ?? 220,
            Z = z,
            Content = content,
        };
        s_windows.Add(win);
        return win;
    }

    public static void Remove(Window w) => s_windows.Remove(w);
    public static void Clear() => s_windows.Clear();

    public static void DrawAll(CellBuffer buf)
    {
        if (s_windows.Count == 0) return;
        // stable sort by Z
        s_windows.Sort((a, b) => a.Z.CompareTo(b.Z));
        foreach (var w in s_windows)
            if (w.Visible) w.Draw(buf);
    }

    // ---- drawing a single window ----
    public void Draw(CellBuffer buf)
    {
        if (W <= 0 || H <= 0) return;

        int Wb = buf.Width, Hb = buf.Height;

        // clip rectangle to buffer
        var x0 = Math.Max(0, X);
        var y0 = Math.Max(0, Y);
        var x1 = Math.Min(Wb - 1, X + W - 1);
        var y1 = Math.Min(Hb - 1, Y + H - 1);
        if (x0 > x1 || y0 > y1) return;

        var opaque = BgAlpha == 255 && BorderAlpha == 255;
        // Force opaque if blending is disabled (Console16)
        var opaqueMode = opaque || !buf.AlphaBlendEnabled;

        // 1) BACKGROUND
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (opaqueMode)
                    buf.TrySet(x, y, new Cell(' ', TextColor, BgColor));
                else
                    buf.BlendBgAndFg(x, y, BgColor, BgAlpha, BgColor, BgAlpha);
            }
        }

        // 2) BORDER (edges)
        //    BORDER — FG = border color, BG: if opaque = BgColor, if transparent = unchanged
        var borderCol = this == s_active ? BorderColorActive : BorderColor;

        // helper: set border glyph with fg=borderCol; keep background (transparent) or set BG to BgColor (opaque)
        void PutBorderFg(int x, int y, char ch)
        {
            if ((uint)x >= (uint)Wb || (uint)y >= (uint)Hb) return;

            if (opaqueMode)
            {
                // background has been set to BgColor in step 1, but set explicitly for consistency
                buf.TrySet(x, y, new Cell(ch, borderCol, BgColor));
            }
            else
            {
                // do NOT blend background with border color — keep background from step 1 (blended BgColor)
                var cur = buf[x, y];
                buf.TrySet(x, y, new Cell(ch, borderCol, cur.Bg));
            }
        }

        // border characters (single line)
        char tl = '┌', tr = '┐', bl = '└', br = '┘', hz = '─', vt = '│';

        // corners
        PutBorderFg(x0, y0, tl);
        PutBorderFg(x1, y0, tr);
        PutBorderFg(x0, y1, bl);
        PutBorderFg(x1, y1, br);

        // horizontal edges
        for (var x = x0 + 1; x < x1; x++)
        {
            PutBorderFg(x, y0, hz);
            PutBorderFg(x, y1, hz);
        }
        // vertical edges
        for (var y = y0 + 1; y < y1; y++)
        {
            PutBorderFg(x0, y, vt);
            PutBorderFg(x1, y, vt);
        }

        // [X] in the top-right corner (if enabled and there is room)
        var span = CloseButtonSpan();
        if (span != null)
        {
            var cx0 = span.Value.x0; // '['
            var cx1 = span.Value.x1; // ']'
            // "[X]" — fg = border color, background as above (opaque = BgColor, transparent = unchanged)
            PutBorderFg(cx0, Y, '[');
            PutBorderFg(cx0 + 1, Y, 'X');
            PutBorderFg(cx1, Y, ']');
        }


        // 3) CONTENT (optional)
        Content?.Invoke(buf, this);
    }


    public static bool HandleMouse(InputState input, int screenW, int screenH)
    {
        var changed = false;

        var down = input.MouseLeftDown;
        var mx = input.MouseX;
        var my = input.MouseY;

        // Pick active window when clicking anywhere in its area
        if (down && !s_prevMouseLeftDown && s_dragging == null)
        {
            var hit = HitTestTop(mx, my);
            if (hit != null)
            {
                if (s_active != hit) { s_active = hit; BringToFront(hit); changed = true; }

                // click on [X] – mark close intent
                if (hit.IsInCloseButton(mx, my))
                {
                    s_clickOnClose = true;
                    // do not start drag
                }
                // drag only if title bar and not clicking [X]
                else if (hit.IsInTitleBar(mx, my))
                {
                    s_dragging = hit;
                    s_dragMouseStartX = mx; s_dragMouseStartY = my;
                    s_dragWinStartX = hit.X; s_dragWinStartY = hit.Y;
                    changed = true; // raise / focus
                }
            }
            else
            {
                // click on background — no active window
                if (s_active != null) { s_active = null; changed = true; }
            }
        }
        // DRAGGING
        else if (down && s_dragging != null)
        {
            var dx = mx - s_dragMouseStartX;
            var dy = my - s_dragMouseStartY;

            var newX = s_dragWinStartX + dx;
            var newY = s_dragWinStartY + dy;
            if (newX != s_dragging.X || newY != s_dragging.Y)
            {
                s_dragging.X = newX;
                s_dragging.Y = newY;
                ClampToScreen(s_dragging, screenW, screenH);
                changed = true;
            }
        }
        // RELEASE
        else if (!down && s_prevMouseLeftDown)
        {
            // if click started on [X] and released over [X] of the same window → close
            if (s_clickOnClose)
            {
                s_clickOnClose = false;
                var hit = HitTestTop(mx, my);
                if (hit != null && hit == s_active && hit.IsInCloseButton(mx, my))
                {
                    // callback
                    hit.OnClose?.Invoke(hit);
                    // remove from manager
                    Remove(hit);
                    if (s_active == hit) s_active = null;
                    changed = true;
                }
            }

            // end drag
            if (s_dragging != null)
            {
                s_dragging = null;
                changed = true;
            }
        }

        s_prevMouseLeftDown = down;
        return changed;
    }

}
