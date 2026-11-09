namespace Showcase.Board;

public interface IBoardFunctions
{
    Task<BoardState> LoadAsync();

    Task<BoardState> MoveAsync(string item, int step);

    Task<BoardState> PlaceAsync(string item, string lane, int index);

    Task<BoardState> BlockAsync(string item, bool blocked);
}
