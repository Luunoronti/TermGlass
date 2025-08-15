// InputReader.cs
using System.Text;
using Visualization;

namespace Visualization;

public sealed class InputReader
{
    private readonly Thread _thread;
    private volatile bool _running = true;
    private readonly InputState _input;

    private volatile bool _resetRequested;

    public void RequestReset() => _resetRequested = true;

    public InputReader(InputState input)
    {
        _input = input;
        _thread = new Thread(ReadLoop) { IsBackground = true, Name = "InputReader" };
    }

    public void Start() => _thread.Start();
    public void Stop() { _running = false; try { _thread.Join(150); } catch { } }

    private void ReadLoop()
    {
        using var stdin = Console.OpenStandardInput(8192);

        var buf = new byte[4096];
        var esc = new StringBuilder(128);
        bool inEsc = false;

        while (_running)
        {
            if (_resetRequested)
            {
                // wyczyść potencjalnie "uciętą" sekwencję po resize
                inEsc = false;
                esc.Clear();
                _resetRequested = false;
            }

            int n;
            try { n = stdin.Read(buf, 0, buf.Length); } // blokująco, brak timeoutów
            catch { continue; }
            if (n <= 0) continue;

            for (int i = 0; i < n; i++)
            {
                char c = (char)buf[i];

                if (!inEsc)
                {
                    if (c == '\x1b') { inEsc = true; esc.Clear(); esc.Append(c); continue; }

                    // ASCII klawisze (Ctrl+Q już masz):
                    switch (c)
                    {
                        case '\x11': _input.EnqueueKey(ConsoleKey.Q, ctrl: true); break; // Ctrl+Q
                        case ' ': _input.EnqueueKey(ConsoleKey.Spacebar); break;
                        case '=': case '+': _input.EnqueueKey(ConsoleKey.OemPlus); break;
                        case '-': _input.EnqueueKey(ConsoleKey.OemMinus); break;
                        case '[': _input.EnqueueKey(ConsoleKey.Oem4); break;   // '['
                        case ']': _input.EnqueueKey(ConsoleKey.Oem6); break;   // ']'
                        case '0': _input.EnqueueKey(ConsoleKey.D0); break;
                        case '1': _input.EnqueueKey(ConsoleKey.D1); break;
                        case '2': _input.EnqueueKey(ConsoleKey.D2); break;
                        case '3': _input.EnqueueKey(ConsoleKey.D3); break;
                        case 'w': case 'W': _input.EnqueueKey(ConsoleKey.W); break;
                        case 'a': case 'A': _input.EnqueueKey(ConsoleKey.A); break;
                        case 's': case 'S': _input.EnqueueKey(ConsoleKey.S); break;
                        case 'd': case 'D': _input.EnqueueKey(ConsoleKey.D); break;
                        case 't': case 'T': _input.EnqueueKey(ConsoleKey.T); break;
                        case 'c': case 'C': _input.EnqueueKey(ConsoleKey.C); break;
                        default: break;
                    }
                    continue;
                }
                else
                {
                    esc.Append(c);

                    // --- MYSZ SGR 1006: ESC [ < b ; x ; y (M|m) ---
                    if (IsMouseSeqComplete(esc, out bool press))
                    {
                        ParseMouseSGR(esc.ToString(), press); // NIE enqueue’ujemy kluczy dla wheel/motion
                        inEsc = false; esc.Clear();
                        continue;
                    }

                    // --- STRZAŁKI: ESC [ A/B/C/D ---
                    if (esc.Length >= 3 && esc[0] == '\x1b' && esc[1] == '[')
                    {
                        char last = esc[^1];
                        if (last == 'A' || last == 'B' || last == 'C' || last == 'D')
                        {
                            switch (last)
                            {
                                case 'A': _input.EnqueueKey(ConsoleKey.UpArrow); break;
                                case 'B': _input.EnqueueKey(ConsoleKey.DownArrow); break;
                                case 'C': _input.EnqueueKey(ConsoleKey.RightArrow); break;
                                case 'D': _input.EnqueueKey(ConsoleKey.LeftArrow); break;
                            }
                            inEsc = false; esc.Clear(); continue;
                        }
                    }

                    // F1..F12 (SS3 lub CSI-tilde)
                    if (TryParseFn(esc.ToString(), out var fkey, out bool sh, out bool al, out bool ct))
                    {
                        _input.EnqueueKey(fkey, ctrl: ct, shift: sh, alt: al);
                        inEsc = false; esc.Clear();
                        continue;
                    }

                    if (esc.Length > 64) { inEsc = false; esc.Clear(); } // sanity
                }
            }
        }
    }

    private void ParseMouseSGR(string s, bool press)
    {
        try
        {
            int lt = s.IndexOf('<');
            int end = s.Length - 1;
            var payload = s.Substring(lt + 1, end - (lt + 1));
            var parts = payload.Split(';');
            if (parts.Length != 3) return;

            int b = int.Parse(parts[0]);
            int x = int.Parse(parts[1]);
            int y = int.Parse(parts[2]);

            bool motion = (b & 32) != 0;
            bool wheel = (b & 64) != 0;
            bool shift = (b & 4) != 0;
            bool meta = (b & 8) != 0;
            bool ctrl = (b & 16) != 0;

            // Aktualizujemy STAN (koaleskowanie ruchów) — bez kolejki klawiszy
            _input.UpdateMouse(x, y, b, press, motion, wheel, ctrl, shift, meta);

            // Wheel → skumuluj delta zamiast generować klawisze
            if (wheel)
            {
                int low = b & 0xFF; // 64=up, 65=down
                _input.AddWheel(low == 64 ? +1 : -1);
            }
        }
        catch { }
    }
    private static bool IsMouseSeqComplete(StringBuilder esc, out bool press)
    {
        press = false;
        if (esc.Length < 3) return false;
        if (esc[0] != '\x1b' || esc[1] != '[' || esc[2] != '<') return false;
        char last = esc[^1];
        if (last == 'M' || last == 'm') { press = (last == 'M'); return true; }
        return false;
    }


    private static bool TryParseFn(string seq, out ConsoleKey key, out bool shift, out bool alt, out bool ctrl)
    {
        key = default; shift = alt = ctrl = false;

        // --- Wariant 1: SS3 bez modyfikatorów: ESC O P/Q/R/S => F1..F4 ---
        if (seq.Length >= 3 && seq[0] == '\x1b' && seq[1] == 'O')
        {
            key = seq[^1] switch
            {
                'P' => ConsoleKey.F1,
                'Q' => ConsoleKey.F2,
                'R' => ConsoleKey.F3,
                'S' => ConsoleKey.F4,
                _ => default
            };
            return key != default;
        }

        // --- Wariant 2a: CSI „tilde”: ESC [ <num> (;<mod>)? ~ => F1..F12 + mody ---
        int lb = seq.IndexOf('[');
        int til = seq.IndexOf('~');
        if (lb >= 0 && til > lb)
        {
            var payload = seq.Substring(lb + 1, til - (lb + 1)); // "11" albo "11;5" itd.
            int sem = payload.IndexOf(';');
            string numStr = sem >= 0 ? payload.Substring(0, sem) : payload;
            string modStr = sem >= 0 ? payload.Substring(sem + 1) : null;

            key = numStr switch
            {
                "11" => ConsoleKey.F1,
                "12" => ConsoleKey.F2,
                "13" => ConsoleKey.F3,
                "14" => ConsoleKey.F4,
                "15" => ConsoleKey.F5,
                "17" => ConsoleKey.F6,
                "18" => ConsoleKey.F7,
                "19" => ConsoleKey.F8,
                "20" => ConsoleKey.F9,
                "21" => ConsoleKey.F10,
                "23" => ConsoleKey.F11,
                "24" => ConsoleKey.F12,
                _ => default
            };
            if (key == default) return false;

            if (!string.IsNullOrEmpty(modStr) && int.TryParse(modStr, out int m))
            {
                // xterm: m = 1 + (Shift?1) + (Alt?2) + (Ctrl?4)
                int mask = Math.Max(1, m) - 1;
                shift = (mask & 1) != 0;
                alt = (mask & 2) != 0;
                ctrl = (mask & 4) != 0;
            }
            return true;
        }

        // --- Wariant 2b: CSI dla F1..F4 w formie litery: ESC [ 1 ; <mod> P/Q/R/S ---
        // przykłady: ESC [ 1 ; 2 P  (F1+Shift), ESC [ 1 ; 5 Q (F2+Ctrl)
        int lb2 = seq.IndexOf('[');
        if (lb2 >= 0 && seq.Length >= lb2 + 4) // min: "[1;2P"
        {
            // Wyciągnij "1;<mod>" i ostatnią literę
            char last = seq[^1];
            if (last is 'P' or 'Q' or 'R' or 'S')
            {
                string between = seq.Substring(lb2 + 1, seq.Length - (lb2 + 2)); // np. "1;5P" -> "1;5"
                int sem2 = between.IndexOf(';');
                if (sem2 > 0)
                {
                    string n = between.Substring(0, sem2);
                    string mstr = between.Substring(sem2 + 1);
                    if (n == "1" && int.TryParse(mstr, out int m))
                    {
                        key = last switch
                        {
                            'P' => ConsoleKey.F1,
                            'Q' => ConsoleKey.F2,
                            'R' => ConsoleKey.F3,
                            'S' => ConsoleKey.F4,
                            _ => default
                        };
                        if (key == default) return false;
                        int mask = Math.Max(1, m) - 1;
                        shift = (mask & 1) != 0;
                        alt = (mask & 2) != 0;
                        ctrl = (mask & 4) != 0;
                        return true;
                    }
                }
            }
        }

        return false;
    }


}
