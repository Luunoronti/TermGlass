using System.Diagnostics;

namespace Visualization;

// =================== Core loop & rendering ===================

internal sealed class MainLoop
{
    private const int MaxKeyEventsPerFrame = 64;

    private readonly Terminal _t;
    private readonly VizConfig _cfg;
    private readonly Action<Frame> _draw;
    private readonly Viewport _vp;
    private readonly CellBuffer _buf;
    private readonly InputState _input = new();
    private readonly Stopwatch _sw = new();
    private double _accum = 0;
    private InputReader _inputReader;
    private readonly TooltipProvider? _tooltip;
    private bool _tooltipEnabled = true;

    private int _frameCounter = 0;
    private double _fps = 0.0;
    private DateTime _fpsLastTime = DateTime.UtcNow;

    private Window? _helpWin;

    public MainLoop(Terminal t, VizConfig cfg, Action<Frame> draw, TooltipProvider? tooltip = null)
    {
        _t = t; _cfg = cfg; _draw = draw;
        _tooltip = tooltip;
        _vp = new Viewport();
        _buf = new CellBuffer(_t.Width, _t.Height);
        _vp.AttachTerminal(_t);
    }

    public void Run()
    {
        _t.EnterAltScreen();
        _t.HideCursor();
        _t.EnableMouse(true);
        _t.Clear();

        _vp.CenterOn(0, 0);
        _vp.SetZoom(1.0);

        _inputReader = new InputReader(_input);
        _inputReader.Start();

        _sw.Start();
        bool running = true;

        while (running)
        {
            if (_t.TryRefreshSize())
            {
                _buf.Resize(_t.Width, _t.Height);
                _t.Clear();
                _t.EnableMouse(true);
                _input.OnResize();          // ustawia _dragLastX/Y = MouseX/Y
                _inputReader?.RequestReset(); // zrywa ewentualną niedomkniętą sekwencję ESC
                _input.Dirty = true;

                // przytnij wszystkie okna do nowego rozmiaru ekranu
                foreach (var w in Window.All())
                    ;
            }

            // ODBIÓR KLAWISZY — limit na ramkę:
            int consumed = 0;
            while (consumed < MaxKeyEventsPerFrame && _input.TryDequeueKey(out var ke))
            {
                consumed++;
                _input.LastKey = ke.Key;
                _input.Ctrl = ke.Ctrl; _input.Shift = ke.Shift; _input.Alt = ke.Alt;

                // Quit: Ctrl+Q
                if (ke.Ctrl && ke.Key == ConsoleKey.Q) { running = false; break; }
                HandleKeys();
            }

            // MYSZ (drag/wheel) — jeśli coś zrobiło zmianę, _input.Dirty już = true
            HandleMouse();

            if (Window.HandleMouse(_input, _t.Width, _t.Height))
            {
                _input.Dirty = true;
            }

            // Jeśli chcemy „ciągły render” przy AutoPlay – wymuś Dirty co iterację:
            if (_cfg.AutoPlay && _cfg.ContinuousRenderWhenAutoPlay)
            {
                _input.Dirty = true;
            }

            // Autoplay (może ustawić StepRequested, a przez to też zbrudzić)
            if (_cfg.AutoPlay)
            {
                _accum += _sw.Elapsed.TotalSeconds;
                _sw.Restart();
                double stepEvery = 1.0 / Math.Max(0.0001, _cfg.AutoStepPerSecond);
                while (_accum >= stepEvery)
                {
                    _accum -= stepEvery;
                    _input.StepRequested = true;
                    _input.Dirty = true;
                }
            }
            else
            {
                _sw.Restart();
            }

            // RENDER TYLKO GDY DIRTY lub po resize
            if (_input.Dirty)
            {
                _input.Dirty = false;

                _buf.AlphaBlendEnabled = (_cfg.ColorMode == ColorMode.TrueColor);

                _buf.Fill(new Cell(' ', Rgb.White, Rgb.Black));

                var frame = new Frame(_t, _vp, _buf, _input, _cfg);
                _draw(frame);

                if (_cfg.Layers.HasFlag(UiLayers.Rulers)) DrawRulers();

                // okna – na wierzchu nad treścią i rulerami
                Window.DrawAll(_buf);

                // === TOOLTIP (nad wszystkim oprócz status bara) ===
                DrawTooltipIfAny();

                if (_cfg.Layers.HasFlag(UiLayers.StatusBar)) DrawStatusBar();

                _t.Draw(_buf);

                // FPS liczymy tylko gdy AutoPlay jest aktywny
                if (_cfg.AutoPlay)
                {
                    _frameCounter++;
                    var now = DateTime.UtcNow;
                    var elapsed = (now - _fpsLastTime).TotalSeconds;
                    if (elapsed >= 1.0)
                    {
                        _fps = _frameCounter / elapsed;
                        _frameCounter = 0;
                        _fpsLastTime = now;
                    }
                }

                _input.StepRequested = false;
                _input.ConsumedMouseMove();
            }
            else
            {
                // nic się nie zmieniło → mikro-drzemka
                Thread.Sleep(1);
            }

            // FPS throttle (opcjonalny — ale zostawmy, by nie grzać CPU)
            if (_cfg.TargetFps > 0)
            {
                int targetMs = 1000 / _cfg.TargetFps;
                Thread.Sleep(Math.Max(0, targetMs - (int)_sw.ElapsedMilliseconds));
            }
        }

        _inputReader?.Stop();
        _t.EnableMouse(false);
        _t.ShowCursor();
        _t.ExitAltScreen();
    }

    // =================== Sterowanie klawiaturą ===================

    private void HandleKeys()
    {
        var k = _input.LastKey;
        bool ctrl = _input.Ctrl;

        switch (k)
        {
            case ConsoleKey.Spacebar:
                _input.StepRequested = true;
                break;

            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                if (ctrl) ZoomAtCursor(1.25);
                else Pan(0, -1);
                break;


            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                if (ctrl) ZoomAtCursor(1 / 1.25);
                else Pan(0, +1);
                break;

            case ConsoleKey.LeftArrow: Pan(-1, 0); break;
            case ConsoleKey.RightArrow: Pan(+1, 0); break;
            case ConsoleKey.UpArrow: Pan(0, -1); break;
            case ConsoleKey.DownArrow: Pan(0, +1); break;

            case ConsoleKey.W: Pan(0, -1); break;
            case ConsoleKey.S: Pan(0, +1); break;
            case ConsoleKey.A: Pan(-1, 0); break;
            case ConsoleKey.D: Pan(+1, 0); break;

            case ConsoleKey.F1:
                ToggleHelpWindow();
                _input.Dirty = true;
                break;

            case ConsoleKey.D0: // reset zoomu do 1.0 wokół środka ekranu
                {
                    int sx = _t.Width / 2;
                    int sy = _t.Height / 2;
                    _vp.ResetZoomAroundScreenPoint(sx, sy, 1.0);
                    _input.Dirty = true;
                    break;
                }

            // Autoplay szybko:
            case ConsoleKey.D1: _cfg.AutoPlay = false; break;
            case ConsoleKey.D2: _cfg.AutoPlay = true; _cfg.AutoStepPerSecond = 5; break;
            case ConsoleKey.D3: _cfg.AutoPlay = true; _cfg.AutoStepPerSecond = 30; break;

            // PRESETY WARSTW (F5–F8):
            case ConsoleKey.F5: _cfg.Layers = UiLayers.All; break;                    // wszystko
            case ConsoleKey.F6: _cfg.Layers = UiLayers.Map; break;                    // tylko mapa
            case ConsoleKey.F7: _cfg.Layers = UiLayers.Rulers | UiLayers.StatusBar; break; // tylko UI bez mapy
            case ConsoleKey.F8: _cfg.Layers ^= UiLayers.Overlays; break;              // toggle overlays

            // Precyzyjna zmiana szybkości:
            case ConsoleKey.Oem4: _cfg.AutoStepPerSecond = Math.Max(0.2, _cfg.AutoStepPerSecond / 1.25); break;
            case ConsoleKey.Oem6: _cfg.AutoStepPerSecond *= 1.25; break;

            case ConsoleKey.T:
                _tooltipEnabled = !_tooltipEnabled;
                break;

            case ConsoleKey.C:
                {
                    // przełącz
                    _cfg.ColorMode = _cfg.ColorMode == ColorMode.TrueColor ? ColorMode.Console16 : ColorMode.TrueColor;

                    // poinformuj Terminal i wyczyść ekran, żeby nie zostały stare atrybuty
                    _t.SetColorMode(_cfg.ColorMode);
                    _t.Clear();

                    _input.Dirty = true; // wymuś pełny redraw
                    break;
                }

        }
    }

    // =================== Obsługa myszy ===================

    private void HandleMouse()
    {
        int wheel = _input.ConsumeWheel();
        if (wheel != 0)
        {
            double factor = Math.Pow(1.1, wheel);
            ZoomAtCursor(factor);
            _input.Dirty = true;
        }

        // jeśli przeciągamy okno – nie pan’ujemy sceny
        if (Window.IsDragging)
        {
            return;
        }

        if (_input.MouseLeftDragging || _input.MouseRightDragging)
        {
            var (dx, dy) = _input.ConsumeDragDelta();
            double mul = _cfg.PanSpeed / _vp.Zoom;   // zawsze proporcjonalnie do zoomu
            if (dx != 0 || dy != 0)
            {
                _vp.Offset(-dx * mul, -dy * mul);
                _input.Dirty = true;
            }
        }
    }

    private void ZoomAtCursor(double factor)
    {
        int sx = _input.MouseX.Clamp(0, _t.Width - 1);
        int sy = _input.MouseY.Clamp(0, _t.Height - 1);
        var (wx, wy) = _vp.ScreenToWorld(sx, sy);
        _vp.ZoomAround(wx, wy, factor, minZoom: 0.1, maxZoom: 40.0);
    }

    private void Pan(int dx, int dy)
    {
        var (wWorld, hWorld) = _vp.VisibleWorldSize();
        double stepX = wWorld * _cfg.PanKeyStepFrac;
        double stepY = hWorld * _cfg.PanKeyStepFrac;
        _vp.Offset(dx * stepX, dy * stepY);
        _input.Dirty = true;
    }

    // =================== UI: Rulers + Status ===================

    private void DrawRulers()
    {
        int W = _t.Width, H = _t.Height;
        if (W < 10 || H < 5) return;

        var bg = _cfg.RulerBgColor;
        byte a = _cfg.RulerBgAlpha;
        int lw = Math.Max(1, _cfg.LeftRulerWidth);

        bool opaqueMode = !_buf.AlphaBlendEnabled ? true : false;

        // 1) Półprzezroczyste tło całego ruler’a
        // top (y=0) — cała szerokość
        for (int x = 0; x < W; x++)
        {
            if (opaqueMode)
                _buf.TrySet(x, 0, new Cell(' ', Rgb.White, bg)); // clear char, solid bg
            else
                _buf.BlendBgAndFg(x, 0, bg, a, bg, a);
        }


        // left (x=0..lw-1) — cała wysokość
        for (int x = 0; x < Math.Min(lw, W); x++)
        {
            for (int y = 0; y < H; y++)
            {
                if (opaqueMode)
                    _buf.TrySet(x, y, new Cell(' ', Rgb.White, bg));
                else
                    _buf.BlendBgAndFg(x, y, bg, a, bg, a);
            }
        }

        // 2) Napisy (bez zmiany tła!)
        // top ruler: co ~10 kolumn wypisz współrzędną świata w tej kolumnie
        for (int sx = lw; sx < W; sx++)
        {
            var (wx, _) = _vp.ScreenToWorld(sx, 1);
            if (sx % 10 == 0)
            {
                var label = ((int)Math.Round(wx)).ToString();
                Renderer.PutTextKeepBg(_buf, sx, 0, label, Rgb.White);
            }
        }

        // left ruler: co 2 wiersze 3-znakowa etykieta
        for (int sy = 1; sy < H - 1; sy++)
        {
            var (_, wy) = _vp.ScreenToWorld(lw, sy);
            if (sy % 2 == 0)
            {
                var s = ((int)Math.Round(wy)).ToString();
                s = s.Length <= 3 ? s.PadLeft(3) : s[^3..];
                Renderer.PutTextKeepBg(_buf, 0, sy, s, Rgb.White);
            }
        }

        // 3) Highlight pozycji myszy (półprzezroczysty) – cienkie linie jak wcześniej
        int msx = _input.MouseX.Clamp(0, W - 1);
        int msy = _input.MouseY.Clamp(0, H - 1);
        byte ha = _cfg.RulerHighlightAlpha;
        var hi = new Rgb(80, 140, 240);

        if (opaqueMode)
        {
            _buf.TrySet(msx, 0, new Cell(_buf[msx, 0].Ch, hi, bg));   // fg=hi, bg already set above
            _buf.TrySet(0, msy, new Cell(_buf[0, msy].Ch, hi, bg));
        }
        else
        {
            _buf.BlendBgAndFg(msx, 0, hi, ha, hi, ha);
            _buf.BlendBgAndFg(0, msy, hi, ha, hi, ha);
        }
    }
    private void DrawStatusBar()
    {
        int W = _t.Width, H = _t.Height;
        if (W < 10 || H < 3) return;

        for (int x = 0; x < W; x++)
            _buf.Set(x, H - 1, new Cell(' ', Rgb.Black, Rgb.Gray));

        // komórka świata pod kursorem (z korektą dla zoom<1)
        var (ix, iy) = _vp.WorldCellUnderScreen(_input.MouseX, _input.MouseY);
        string autoInfo = _cfg.AutoPlay ? $"{_cfg.AutoStepPerSecond:F1}/s | FPS {_fps:F1}" : "off";

        // <<< nowa linia statusu
        string info = $" {_cfg.ColorMode} | {_cfg.Layers} | Zoom {_vp.Zoom:F2} | Auto {autoInfo} | Cell {ix}, {iy}";
        string status = $"F1 Help | {info}".PadRight(W);

        Renderer.PutText(_buf, 0, H - 1, status[..Math.Min(W, status.Length)], Rgb.Black, Rgb.Gray);
    }


    private void DrawTooltipIfAny()
    {
        if (!_tooltipEnabled) return; // <-- nowa linia
        if (_tooltip == null) return;
        if (!_cfg.Layers.HasFlag(UiLayers.Overlays)) return;

        var (wx, wy) = _vp.ScreenToWorld(_input.MouseX, _input.MouseY);
        // Skip tooltip if cursor is over a window
        bool overWindow = Window.All().Any(w =>
            w.Visible &&
            _input.MouseX >= w.X &&
            _input.MouseX < w.X + w.W &&
            _input.MouseY >= w.Y &&
            _input.MouseY < w.Y + w.H);

        if (overWindow)
        {
            return;
        }


        var (ix, iy) = _vp.WorldCellUnderScreen(_input.MouseX, _input.MouseY);
        string? text = _tooltip(ix, iy);
        if (string.IsNullOrEmpty(text)) return;

        // wstępna pozycja przy kurszorze
        int sx = Math.Clamp(_input.MouseX + 2, 0, _t.Width - 1);
        int sy = Math.Clamp(_input.MouseY + 1, 0, _t.Height - 2);

        // oszacuj docelową szerokość/wysokość (jak w Renderer.DrawTooltipBox)
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int maxLen = 0;
        foreach (var ln in lines) maxLen = Math.Max(maxLen, ln?.Length ?? 0);
        int w = Math.Clamp(maxLen + 2, 6, _t.Width); // +2 padding
        int h = Math.Min(lines.Length, Math.Max(1, _t.Height - 1)); // bez statusu

        // jeżeli nie mieści się w prawo – przesuń w lewo
        if (sx + w >= _t.Width) sx = Math.Max(0, _t.Width - w - 1);
        // jeżeli nie mieści się w dół (powyżej statusu) – przesuń NAD kursorem
        if (sy + lines.Length >= _t.Height - 1)
            sy = Math.Max(0, _input.MouseY - lines.Length - 1);

        Renderer.DrawTooltipBox(_buf, sx, sy, lines, _cfg.TooltipBgAlpha, _cfg.TooltipBorderAlpha);
    }



    private void ToggleHelpWindow()
    {
        if (_helpWin == null)
        {
            // rozmiar i pozycja (wyśrodkuj)
            int w = Math.Min(64, _t.Width - 6);
            int h = Math.Min(18, _t.Height - 6);
            int x = (_t.Width - w) / 2;
            int y = (_t.Height - h) / 2;

            _helpWin = Window.Create(
                x: x, y: y, w: w, h: h,
                bg: new Rgb(20, 20, 24), bgAlpha: 220,
                z: 100,
                content: (buf, self) =>
                {
                    // tytuł
                    Renderer.PutTextKeepBg(buf, self.X + 2, self.Y, "[ Help ]", new Rgb(255, 230, 120));

                    int ln = self.Y + 2;
                    int lx = self.X + 2;

                    // lista skrótów
                    void L(string s) { Renderer.PutTextKeepBg(buf, lx, ln++, s, new Rgb(230, 230, 230)); }

                    L("Esc/Ctrl+Q : quit");
                    L("= / -       : zoom in/out (also mouse wheel)");
                    L("0           : reset zoom");
                    L("LMB drag    : pan (PPM also if terminal allows)");
                    L("Arrows/WASD : pan");
                    L("Space       : step");
                    L("F5/F6/F7/F8 : layers (all/map/ui/overlays)");
                    L("C           : toggle color mode (TrueColor/16)");
                    L("T           : toggle tooltip");

                    // mała podpowiedź
                    ln++;
                    Renderer.PutTextKeepBg(buf, lx, ln, "Press F1 to hide this window.", new Rgb(200, 220, 255));
                }
            );

            _helpWin.ShowCloseButton = true;
            _helpWin.OnClose = w =>
            {
                // po zamknięciu wyczyść referencję i odrysuj
                _helpWin = null;
                _input.Dirty = true;
            };

            // (opcjonalnie) wyróżnij aktywną ramkę:
            _helpWin.BorderColorActive = new Rgb(255, 200, 80);

        }
        else
        {
            _helpWin.Visible = !_helpWin.Visible;
        }
    }

}