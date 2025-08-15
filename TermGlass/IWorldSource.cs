namespace Visualization;

// Świat: użytkownik dostarcza dane do samplowania (znak+kolor)
public interface IWorldSource
{
    int Width { get; }
    int Height { get; }
    // Zwraca “komórkę świata” (znak + kolor). Poza mapą: null → tło.
    Cell? GetCell(int x, int y);

}