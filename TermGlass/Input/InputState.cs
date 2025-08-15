using System.Collections.Concurrent;

namespace TermGlass;

// =================== Input: stan + parser myszy (SGR 1006) ===================

public sealed class InputState
{
    // keys
    public ConsoleKey LastKey
    {
        get; set;
    }
    public bool Ctrl
    {
        get; set;
    }
    public bool Shift
    {
        get; set;
    }
    public bool Alt
    {
        get; set;
    }
    public bool Esc
    {
        get; set;
    }
    public volatile bool Dirty; // signal: something changed → frame to draw

    // mouse (SGR)
    private readonly object _lock = new();
    public int MouseX { get; private set; } = 10; // 0-based
    public int MouseY { get; private set; } = 5;
    private int _wheel; // accumulator (negative/positive)
    public bool MouseLeftDown
    {
        get; private set;
    }
    public bool MouseRightDown
    {
        get; private set;
    }
    public bool MouseLeftDragging => _dragLeft;
    public bool MouseRightDragging => _dragRight;

    private int _dragLastX, _dragLastY;
    private bool _dragLeft, _dragRight;

    // step loop
    public bool StepRequested
    {
        get; set;
    }

    public void OnResize()
    {
        lock (_lock)
        {
            _dragLastX = MouseX;
            _dragLastY = MouseY;
            Dirty = true;
        }
    }

    public void AddWheel(int delta)
    {
        lock (_lock) { _wheel += delta; Dirty = true; }
    }

    public void EnqueueKey(ConsoleKey key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        _keys.Enqueue(new KeyEvent { Key = key, Ctrl = ctrl, Shift = shift, Alt = alt });
        Dirty = true;
    }


    // called by MouseReader (thread)
    public void UpdateMouse(int x1Based, int y1Based, int btnCode, bool press, bool motion, bool wheel, bool ctrl, bool shift, bool alt)
    {
        lock (_lock)
        {
            var x = Math.Max(0, x1Based - 1);
            var y = Math.Max(0, y1Based - 1);

            // Always update position and dirty the frame
            MouseX = x;
            MouseY = y;
            Dirty = true;

            if (wheel) return; // wheel handled separately (AddWheel)

            var baseBtn = btnCode & 0b11; // 0=L, 1=M, 2=R

            if (motion)
            {
                // Motion: DON'T change button states or drag baseline.
                // Delta will be calculated relative to _dragLastX/_dragLastY in ConsumeDragDelta().
                return;
            }

            // Press/Release – tylko tu ruszamy drag + baseline.
            if (press)
            {
                if (baseBtn == 0) { MouseLeftDown = true; _dragLeft = true; _dragLastX = x; _dragLastY = y; }
                if (baseBtn == 2) { MouseRightDown = true; _dragRight = true; _dragLastX = x; _dragLastY = y; }
            }
            else
            {
                if (baseBtn == 0) { MouseLeftDown = false; _dragLeft = false; }
                if (baseBtn == 2) { MouseRightDown = false; _dragRight = false; }
            }
        }
    }
    public void ConsumedMouseMove()
    {
    }

    public (int dx, int dy) ConsumeDragDelta()
    {
        lock (_lock)
        {
            var dx = MouseX - _dragLastX;
            var dy = MouseY - _dragLastY;
            _dragLastX = MouseX;
            _dragLastY = MouseY;
            return (dx, dy);
        }
    }

    public int ConsumeWheel()
    {
        lock (_lock)
        {
            var v = _wheel; _wheel = 0;
            return v;
        }
    }

    public struct KeyEvent
    {
        public ConsoleKey Key;
        public bool Ctrl, Shift, Alt;
    }

    private readonly ConcurrentQueue<KeyEvent> _keys = new();

    public bool TryDequeueKey(out KeyEvent e) => _keys.TryDequeue(out e);
}
