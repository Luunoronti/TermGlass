namespace TermGlass;

public interface IViewport
{
    double Zoom
    {
        get;
    }
    double OffsetX
    {
        get;
    }
    double OffsetY
    {
        get;
    }
    (int wx, int wy) ScreenToWorld(int sx, int sy);
    (int sx, int sy) WorldToScreen(int wx, int wy);
}
