namespace TermGlass;

// =================== Public API ===================

[Flags]
public enum UiLayers
{
    None = 0,
    Map = 1 << 0,
    Rulers = 1 << 1,
    StatusBar = 1 << 2,
    Overlays = 1 << 3,
    All = Map | Rulers | StatusBar | Overlays
}