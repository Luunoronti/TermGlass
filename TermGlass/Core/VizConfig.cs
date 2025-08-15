
namespace TermGlass;

public record VizConfig
{
    public ColorMode ColorMode { get; set; } = ColorMode.TrueColor;
    public int TargetFps { get; init; } = 30;

    // autoplay / step loop:
    public bool AutoPlay { get; set; } = false;
    public double AutoStepPerSecond { get; set; } = 5.0;

    public double PanSpeed { get; set; } = 1.0;        // mouse panning speed multiplier (world/px)
    public double PanKeyStepFrac { get; set; } = 0.10; // fraction of world view width per 1 keyboard step


    // LAYERS:
    public UiLayers Layers { get; set; } = UiLayers.All;

    public Rgb RulerHighlight { get; set; } = new Rgb(80, 140, 240);
    public Rgb RulerBgColor { get; set; } = new Rgb(40, 40, 40); // dark gray
    public byte RulerBgAlpha { get; set; } = 190;                 // ~47% transparency
    public byte RulerHighlightAlpha { get; set; } = 160;          // highlight is slightly stronger
    public int LeftRulerWidth { get; set; } = 4;                 // left ruler width (columns 0..W-1)


    public bool ContinuousRenderWhenAutoPlay { get; set; } = true;

    public byte TooltipBgAlpha { get; set; } = 255;   // 0..255 (255 = opaque)
    public byte TooltipBorderAlpha { get; set; } = 255; // 0..255

}