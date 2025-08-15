using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TermGlass;

public sealed class Terminal : IDisposable
{
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

    const uint ENABLE_PROCESSED_INPUT = 0x0001;
    const uint ENABLE_LINE_INPUT = 0x0002;
    const uint ENABLE_ECHO_INPUT = 0x0004;
    const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    const uint ENABLE_EXTENDED_FLAGS = 0x0080;
    const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;


    private readonly bool _vtOk;
    private ColorMode _mode;
    private int _width, _height;

    public int Width => _width;
    public int Height => _height;
    private readonly StringBuilder _sb = new StringBuilder(64 * 1024);

    // ANSI 16 SGR code tables (indexes line up with Rgb.Ansi16Palette)
    private static readonly int[] Ansi16FgCodes = { 30, 31, 32, 33, 34, 35, 36, 37, 90, 91, 92, 93, 94, 95, 96, 97 };
    private static readonly int[] Ansi16BgCodes = { 40, 41, 42, 43, 44, 45, 46, 47, 100, 101, 102, 103, 104, 105, 106, 107 };


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int fg, int bg) MapToAnsi16(Rgb fg, Rgb bg)
    {
        var fi = fg.NearestAnsi16Index();
        var bi = bg.NearestAnsi16Index();
        return (Ansi16FgCodes[fi], Ansi16BgCodes[bi]);
    }

    public Terminal(ColorMode mode)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        _mode = mode;
        _vtOk = EnableVT();
        _width = Console.WindowWidth;
        _height = Console.WindowHeight;
        Console.TreatControlCAsInput = true;
        Console.CursorVisible = true;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; };
    }

    public void EnterAltScreen() => Write("\x1b[?1049h");
    public void ExitAltScreen() => Write("\x1b[?1049l");
    public void HideCursor() => Write("\x1b[?25l");
    public void ShowCursor() => Write("\x1b[?25h");
    public void Clear() => Write("\x1b[2J\x1b[H");
    public void EnableMouse(bool on)
    {
        if (on) Console.Write("\x1b[?1003h\x1b[?1006h");
        else Console.Write("\x1b[?1003l\x1b[?1006l");
    }

    public void SetColorMode(ColorMode mode)
    {
        // change drawing mode
        _mode = mode;
        // reset attributes so old colors don't remain
        Console.Write("\x1b[0m");
    }

    public bool TryRefreshSize()
    {
        var w = Console.WindowWidth;
        var h = Console.WindowHeight;
        if (w != _width || h != _height)
        {
            _width = w; _height = h;
            return true;
        }
        return false;
    }

    // Only keys (mouse goes through MouseReader on stdin)
    public bool TryReadKey(InputState input)
    {
        if (!Console.KeyAvailable) return false;
        var key = Console.ReadKey(intercept: true);
        input.LastKey = key.Key;
        input.Ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        input.Shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        input.Alt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
        input.Esc = key.Key == ConsoleKey.Escape;
        return true;
    }

    public void Draw(CellBuffer buf)
    {
        _sb.Clear();
        _sb.Append("\x1b[H"); // Home (1,1)

        if (_mode == ColorMode.TrueColor)
        {
            // Initial state: no colors set → first cell will force both codes
            var curFg = new Rgb(0, 0, 0);
            var curBg = new Rgb(0, 0, 0);
            var colorInited = false;

            for (var y = 0; y < buf.Height; y++)
            {
                for (var x = 0; x < buf.Width; x++)
                {
                    var c = buf[x, y];

                    if (!colorInited || !RgbEquals(c.Bg, curBg))
                    {
                        AppendBgTrueColor(_sb, c.Bg);
                        curBg = c.Bg;
                        colorInited = true;
                    }
                    if (!colorInited || !RgbEquals(c.Fg, curFg))
                    {
                        AppendFgTrueColor(_sb, c.Fg);
                        curFg = c.Fg;
                        colorInited = true;
                    }

                    _sb.Append(c.Ch);
                }
                if (y < buf.Height - 1) _sb.Append("\r\n");
            }
        }
        else // ColorMode.Console16
        {
            int curFg = -1, curBg = -1; // "no collour"
            for (var y = 0; y < buf.Height; y++)
            {
                for (var x = 0; x < buf.Width; x++)
                {
                    var c = buf[x, y];
                    var (fgCode, bgCode) = MapToAnsi16(c.Fg, c.Bg);

                    if (bgCode != curBg) { _sb.Append("\x1b[").Append(bgCode).Append('m'); curBg = bgCode; }
                    if (fgCode != curFg) { _sb.Append("\x1b[").Append(fgCode).Append('m'); curFg = fgCode; }

                    _sb.Append(c.Ch);
                }
                if (y < buf.Height - 1) _sb.Append("\r\n");
            }
        }

        // Reset at the end to avoid leaving terminal in custom colors
        _sb.Append("\x1b[0m");
        Console.Write(_sb);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool RgbEquals(Rgb a, Rgb b) => a.R == b.R && a.G == b.G && a.B == b.B;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendFgTrueColor(StringBuilder sb, Rgb c)
        => sb.Append("\x1b[38;2;").Append(c.R).Append(';').Append(c.G).Append(';').Append(c.B).Append('m');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendBgTrueColor(StringBuilder sb, Rgb c)
        => sb.Append("\x1b[48;2;").Append(c.R).Append(';').Append(c.G).Append(';').Append(c.B).Append('m');


    private static void Write(string s) => Console.Write(s);

    private static bool EnableVT()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;

        try
        {
            // OUT: enable VT
            var outH = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (!GetConsoleMode(outH, out var outMode)) return false;
            outMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            SetConsoleMode(outH, outMode);

            // IN: switch to VT mode + "raw-ish"
            var inH = GetStdHandle(-10); // STD_INPUT_HANDLE
            if (!GetConsoleMode(inH, out var inMode)) return false;

            // To disable QUICK_EDIT, EXTENDED_FLAGS must be set.
            inMode |= ENABLE_EXTENDED_FLAGS;
            // Remove line cooking/echo/processing, add VT input.
            inMode &= ~(ENABLE_ECHO_INPUT | ENABLE_LINE_INPUT | ENABLE_PROCESSED_INPUT | ENABLE_QUICK_EDIT_MODE);
            inMode |= ENABLE_VIRTUAL_TERMINAL_INPUT;

            SetConsoleMode(inH, inMode);
            return true;
        }
        catch { return false; }
    }

    [DllImport("kernel32.dll", SetLastError = true)] static extern nint GetStdHandle(int nStdHandle);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

    public void Dispose()
    {
        ShowCursor(); EnableMouse(false);
    }
}
