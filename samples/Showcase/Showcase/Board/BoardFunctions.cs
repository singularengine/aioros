using System.Collections.Immutable;

namespace Showcase.Board;

/// <summary>The board, kept in memory: one owner swaps immutable snapshots, readers never wait.</summary>
public sealed class BoardStore
{
    private BoardState _state = BoardSample.State;

    public BoardState State => Volatile.Read(ref _state);

    public BoardState Update(Func<BoardState, BoardState> change)
    {
        ImmutableInterlocked.Update(ref _state, change);
        return Volatile.Read(ref _state);
    }
}

public sealed class BoardFunctions(BoardStore store) : IBoardFunctions
{
    public Task<BoardState> LoadAsync() => Task.FromResult(store.State);

    public Task<BoardState> MoveAsync(string item, int step) => Task.FromResult(store.Update(state =>
    {
        var from = state.Lanes.ToList().FindIndex(lane => lane.Items.Any(card => card.Id == item));
        var to = from + Math.Sign(step);
        if (from < 0 || to < 0 || to >= state.Lanes.Count)
        {
            return state;
        }

        var card = state.Lanes[from].Items.First(entry => entry.Id == item);
        return new BoardState([.. state.Lanes.Select((lane, index) => index == from
            ? lane with { Items = [.. lane.Items.Where(entry => entry.Id != item)] }
            : index == to ? lane with { Items = [card, .. lane.Items] } : lane)]);
    }));

    public Task<BoardState> PlaceAsync(string item, string lane, int index) => Task.FromResult(store.Update(state =>
    {
        var card = state.Lanes.SelectMany(entry => entry.Items).FirstOrDefault(entry => entry.Id == item);
        if (card is null || state.Lanes.All(entry => entry.Key != lane))
        {
            return state;
        }

        return new BoardState([.. state.Lanes.Select(entry =>
        {
            var kept = entry.Items.Where(other => other.Id != item).ToList();
            if (entry.Key == lane)
            {
                kept.Insert(Math.Clamp(index, 0, kept.Count), card);
            }

            return entry with { Items = kept };
        })]);
    }));

    public Task<BoardState> BlockAsync(string item, bool blocked) => Task.FromResult(store.Update(state =>
        new BoardState([.. state.Lanes.Select(lane => lane with { Items = [.. lane.Items.Select(card => card.Id == item ? card with { Blocked = blocked } : card)] })])));
}
