namespace TermGlass;


public static class Visualizer
{
    public static void Run(VizConfig cfg, Action<Frame> draw)
    {
        using var term = new Terminal(cfg.ColorMode);
        var loop = new MainLoop(term, cfg, draw);
        loop.Run();
    }
    public static void Run(VizConfig cfg, Action<Frame> draw, TooltipProvider? info)
    {
        using var term = new Terminal(cfg.ColorMode);
        var loop = new MainLoop(term, cfg, draw, info);
        loop.Run();
    }
}