using System.Collections.Concurrent;
using Visualization;

namespace Visualization;

// =================== Input: stan + parser myszy (SGR 1006) ===================

public sealed class InputState
{
    // klawisze
    public ConsoleKey LastKey { get; set; }
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Esc { get; set; }
    public volatile bool Dirty; // sygnał: coś się zmieniło → klatka do narysowania

    // mysz (SGR)
    private readonly object _lock = new();
    public int MouseX { get; private set; } = 10; // 0-based
    public int MouseY { get; private set; } = 5;
    private int _prevX, _prevY;
    private bool _moved;
    private int _wheel; // akumulator (ujemny/ dodatni)
    public bool MouseLeftDown { get; private set; }
    public bool MouseRightDown { get; private set; }
    public bool MouseLeftDragging => _dragLeft;
    public bool MouseRightDragging => _dragRight;

    private int _dragLastX, _dragLastY;
    private bool _dragLeft, _dragRight;

    // pętla krokowa
    public bool StepRequested { get; set; }

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


    // wywoływane przez MouseReader (wątek)
    public void UpdateMouse(int x1Based, int y1Based, int btnCode, bool press, bool motion, bool wheel, bool ctrl, bool shift, bool alt)
    {
        lock (_lock)
        {
            int x = Math.Max(0, x1Based - 1);
            int y = Math.Max(0, y1Based - 1);

            // Zawsze aktualizuj pozycję i brudź klatkę
            MouseX = x;
            MouseY = y;
            Dirty = true;

            if (wheel) return; // kółko obsługujemy osobno (AddWheel)

            int baseBtn = btnCode & 0b11; // 0=L, 1=M, 2=R

            if (motion)
            {
                // Ruch: NIE zmieniaj stanów przycisków ani baseline drag.
                // Delta zostanie policzona względem _dragLastX/_dragLastY w ConsumeDragDelta().
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
    public void ConsumedMouseMove() { lock (_lock) _moved = false; }

    public (int dx, int dy) ConsumeDragDelta()
    {
        lock (_lock)
        {
            int dx = MouseX - _dragLastX;
            int dy = MouseY - _dragLastY;
            _dragLastX = MouseX;
            _dragLastY = MouseY;
            return (dx, dy);
        }
    }

    public int ConsumeWheel()
    {
        lock (_lock)
        {
            int v = _wheel; _wheel = 0;
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
