using System;
using System.Collections.Generic;


using Visualization;

public sealed class Window
{
    // ---- własności instancji ----
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
    }  // callback zamknięcia (opcjonalny)
       
    public Rgb BgColor { get; set; } = new Rgb(20, 20, 20);
    public byte BgAlpha { get; set; } = 180;          // 255 = nieprzezroczyste tło
    public Rgb BorderColor { get; set; } = new Rgb(255, 255, 255);
    public byte BorderAlpha { get; set; } = 220;      // 255 = nieprzezroczysta ramka
    public Rgb TextColor { get; set; } = new Rgb(245, 245, 245);

    public bool Visible { get; set; } = true;
    public int Z { get; set; } = 0;                   // z-index: większe rysują się później (na wierzchu)

    /// <summary>Opcjonalny render zawartości; wywoływany po tle/ramce.</summary>
    public Action<CellBuffer, Window>? Content
    {
        get; set;
    }

    // ---- statyczny menedżer ----
    private static readonly List<Window> s_windows = new();

    private static Window? s_active;
    private static Window? s_dragging;
    private static int s_dragMouseStartX, s_dragMouseStartY;
    private static int s_dragWinStartX, s_dragWinStartY;
    private static bool s_prevMouseLeftDown;
    private static bool s_clickOnClose; // true, gdy mousedown poszedł w [X]

    public static bool IsDragging => s_dragging != null;
    public static IReadOnlyList<Window> All() => s_windows;



    // Najwyższe (po Z) widoczne okno pod punktem (mx,my)
    private static Window? HitTestTop(int mx, int my)
    {
        Window? best = null;
        int bestZ = int.MinValue;
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

    // obszar przycisku zamknięcia: "[X]" w prawym górnym rogu ramki
    private (int x0, int x1)? CloseButtonSpan()
    {
        if (!ShowCloseButton || W < 5) return null;
        int x1 = X + W - 2;      // ']' na X+W-2
        int x0 = X + W - 4;      // '[' na X+W-4, 'X' na X+W-3
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
        return (mx >= X && mx < X + W);
    }


    // Podnieś okno na wierzch (zwiększ Z tak, by rysowało się ostatnie)
    private static void BringToFront(Window w)
    {
        int maxZ = 0;
        foreach (var ww in s_windows) if (ww.Z > maxZ) maxZ = ww.Z;
        w.Z = maxZ + 1;
    }

    // Upewnij się, że okno nie ucieka poza ekran
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
            BgAlpha = bgAlpha ?? (byte)180,
            BorderColor = border ?? new Rgb(255, 255, 255),
            BorderAlpha = borderAlpha ?? (byte)220,
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
        // stabilne sortowanie po Z
        s_windows.Sort((a, b) => a.Z.CompareTo(b.Z));
        foreach (var w in s_windows)
            if (w.Visible) w.Draw(buf);
    }

    // ---- rysowanie pojedynczego okna ----
    public void Draw(CellBuffer buf)
    {
        if (W <= 0 || H <= 0) return;

        int Wb = buf.Width, Hb = buf.Height;

        // clip prostokąta do bufora
        int x0 = Math.Max(0, X);
        int y0 = Math.Max(0, Y);
        int x1 = Math.Min(Wb - 1, X + W - 1);
        int y1 = Math.Min(Hb - 1, Y + H - 1);
        if (x0 > x1 || y0 > y1) return;

        bool opaque = (BgAlpha == 255 && BorderAlpha == 255);
        // Force opaque if blending is disabled (Console16)
        bool opaqueMode = opaque || !buf.AlphaBlendEnabled;

        // 1) TŁO
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (opaqueMode)
                    buf.TrySet(x, y, new Cell(' ', TextColor, BgColor));
                else
                    buf.BlendBgAndFg(x, y, BgColor, BgAlpha, BgColor, BgAlpha);
            }
        }

        // 2) RAMKA (po krawędziach)
        // 2) RAMKA — FG = kolor ramki, BG: przy opaque = BgColor, przy transparent = bez zmian
        var borderCol = (this == s_active) ? BorderColorActive : BorderColor;

        // helper: ustaw znak ramki z fg=borderCol, nie ruszaj tła (transparent) lub ustaw tło na BgColor (opaque)
        void PutBorderFg(int x, int y, char ch)
        {
            if ((uint)x >= (uint)Wb || (uint)y >= (uint)Hb) return;

            if (opaqueMode)
            {
                // tło już jest BgColor po kroku 1, ale ustawiamy jawnie dla spójności
                buf.TrySet(x, y, new Cell(ch, borderCol, BgColor));
            }
            else
            {
                // NIE mieszamy tła kolorem ramki — zostawiamy tło z kroku 1 (zblendowane BgColor)
                var cur = buf[x, y];
                buf.TrySet(x, y, new Cell(ch, borderCol, cur.Bg));
            }
        }

        // znaki ramki (pojedyncza linia)
        char tl = '┌', tr = '┐', bl = '└', br = '┘', hz = '─', vt = '│';

        // rogi
        PutBorderFg(x0, y0, tl);
        PutBorderFg(x1, y0, tr);
        PutBorderFg(x0, y1, bl);
        PutBorderFg(x1, y1, br);

        // krawędzie poziome
        for (int x = x0 + 1; x < x1; x++)
        {
            PutBorderFg(x, y0, hz);
            PutBorderFg(x, y1, hz);
        }
        // krawędzie pionowe
        for (int y = y0 + 1; y < y1; y++)
        {
            PutBorderFg(x0, y, vt);
            PutBorderFg(x1, y, vt);
        }

        // [X] w prawym górnym rogu (jeśli włączony i jest miejsce)
        var span = CloseButtonSpan();
        if (span != null)
        {
            int cx0 = span.Value.x0; // '['
            int cx1 = span.Value.x1; // ']'
                                     // "[X]" — fg = kolor ramki, tło jak wyżej (opaque=BgColor, transparent=bez zmian)
            PutBorderFg(cx0, Y, '[');
            PutBorderFg(cx0 + 1, Y, 'X');
            PutBorderFg(cx1, Y, ']');
        }


        // 3) ZAWARTOŚĆ (opcjonalnie)
        Content?.Invoke(buf, this);
    }


    public static bool HandleMouse(InputState input, int screenW, int screenH)
    {
        bool changed = false;

        bool down = input.MouseLeftDown;
        int mx = input.MouseX;
        int my = input.MouseY;

        // Złap aktywne okno na klik gdziekolwiek w jego obszarze
        if (down && !s_prevMouseLeftDown && s_dragging == null)
        {
            var hit = HitTestTop(mx, my);
            if (hit != null)
            {
                if (s_active != hit) { s_active = hit; BringToFront(hit); changed = true; }

                // klik na [X] – zaznacz intencję zamknięcia
                if (hit.IsInCloseButton(mx, my))
                {
                    s_clickOnClose = true;
                    // nie zaczynamy drag
                }
                // drag tylko jeśli tytuł i nie klikamy w [X]
                else if (hit.IsInTitleBar(mx, my))
                {
                    s_dragging = hit;
                    s_dragMouseStartX = mx; s_dragMouseStartY = my;
                    s_dragWinStartX = hit.X; s_dragWinStartY = hit.Y;
                    changed = true; // podniesienie / focus
                }
            }
            else
            {
                // klik w tło — brak aktywnego
                if (s_active != null) { s_active = null; changed = true; }
            }
        }
        // PRZESUWANIE
        else if (down && s_dragging != null)
        {
            int dx = mx - s_dragMouseStartX;
            int dy = my - s_dragMouseStartY;

            int newX = s_dragWinStartX + dx;
            int newY = s_dragWinStartY + dy;
            if (newX != s_dragging.X || newY != s_dragging.Y)
            {
                s_dragging.X = newX;
                s_dragging.Y = newY;
                ClampToScreen(s_dragging, screenW, screenH);
                changed = true;
            }
        }
        // ZWOLNIENIE
        else if (!down && s_prevMouseLeftDown)
        {
            // jeśli był klik na [X] i puszczono nad [X] tego samego okna → zamknij
            if (s_clickOnClose)
            {
                s_clickOnClose = false;
                var hit = HitTestTop(mx, my);
                if (hit != null && hit == s_active && hit.IsInCloseButton(mx, my))
                {
                    // callback
                    hit.OnClose?.Invoke(hit);
                    // usunięcie z managera
                    Remove(hit);
                    if (s_active == hit) s_active = null;
                    changed = true;
                }
            }

            // koniec drag
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
