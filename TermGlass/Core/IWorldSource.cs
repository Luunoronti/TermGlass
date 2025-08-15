
namespace TermGlass;

// World: the user provides data for sampling (character + color)
public interface IWorldSource
{
    int Width
    {
        get;
    }
    int Height
    {
        get;
    }
    // Returns a "world cell" (character + color). Outside the map: null → background.
    Cell? GetCell(int x, int y);

}
