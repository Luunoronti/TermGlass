namespace TermGlass;

// =================== Utils ===================

internal static class Ext
{
    public static int Clamp(this int v, int lo, int hi) => (v < lo) ? lo : (v > hi ? hi : v);
}