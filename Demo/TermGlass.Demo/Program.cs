using TermGlass.Core;
using TermGlass.Rendering.Color;
using TermGlass.Modes;

// Simple demo: render a gradient DemoWorld with interactive zoom/pan
Console.OutputEncoding = System.Text.Encoding.UTF8;

var cfg = new VizConfig
{
    ColorMode = ColorMode.TrueColor,
    Layers = UiLayers.All,
    AutoPlay = false,
    AutoStepPerSecond = 10,
};

// Create a sample world (80x40)
var world = new TermGlass.DemoWorld.DemoWorld(120, 60);

TooltipProvider? tooltip = (ix, iy) =>
{
    if (ix < 0 || iy < 0 || ix >= world.Width || iy >= world.Height) return null;
    return $"Cell {ix},{iy}";
};

Visualizer.Run(cfg, frame =>
{
    frame.DrawWorld(world);
}, tooltip);
