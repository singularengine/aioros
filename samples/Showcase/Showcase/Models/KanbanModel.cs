using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Showcase.Board;

namespace Showcase.Models;

/// <summary>A card as the board shows it. Each kind has its own template; the board's commands move it.</summary>
public abstract class CardView(BoardItem item, bool first, bool last)
{
    public string Id => item.Id;

    public string Title => item.Title;

    public Priority Priority => item.Priority;

    public string? Owner => item.Owner;

    public DateOnly? Due => item.Due;

    public bool Blocked => item.Blocked;

    public bool CanMoveLeft { get; } = !first;

    public bool CanMoveRight { get; } = !last;

    public static CardView Of(BoardItem item, bool first, bool last) => item switch
    {
        BugItem bug => new BugCard(bug, first, last),
        EpicItem epic => new EpicCard(epic, first, last),
        TaskItem task => new TaskCard(task, first, last),
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}

public sealed class TaskCard(TaskItem item, bool first, bool last) : CardView(item, first, last)
{
    public string Area => item.Area;
}

public sealed class BugCard(BugItem item, bool first, bool last) : CardView(item, first, last)
{
    public int Reports => item.Reports;

    public string Environment => item.Environment;
}

public sealed class EpicCard(EpicItem item, bool first, bool last) : CardView(item, first, last)
{
    public int Done => item.Done;

    public int Total => item.Total;

    public double Percent => item.Total == 0 ? 0 : 100.0 * item.Done / item.Total;
}

public sealed class LaneView(BoardLane lane, IReadOnlyList<CardView> cards, bool last)
{
    public bool IsDone => last;

    public string Key => lane.Key;

    public string Title => lane.Title;

    public int Limit => lane.Limit;

    public bool HasLimit => lane.Limit > 0;

    public bool IsOverLimit => lane.Limit > 0 && cards.Count > lane.Limit;

    public int Count => cards.Count;

    public bool IsEmpty => cards.Count == 0;

    public IReadOnlyList<CardView> Cards => cards;
}

public sealed record KanbanData(IReadOnlyList<BoardLane> Lanes, string Xaml)
{
    public static KanbanData Of(BoardState state, string xaml)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new(state.Lanes, xaml);
    }
}

public sealed partial class KanbanModel : ObservableObject
{
    public KanbanModel(KanbanData data, IBoardFunctions board)
    {
        ArgumentNullException.ThrowIfNull(data);
        Xaml = data.Xaml;
        MoveLeftCommand = new AsyncRelayCommand<string>(id => board.MoveAsync(id!, -1));
        MoveRightCommand = new AsyncRelayCommand<string>(id => board.MoveAsync(id!, 1));
        PlaceCommand = new AsyncRelayCommand<(string Card, string Lane, int Index)>(move => board.PlaceAsync(move.Card, move.Lane, move.Index));
        BlockCommand = new AsyncRelayCommand<string>(id => board.BlockAsync(id!, !Lanes.SelectMany(lane => lane.Cards).First(card => card.Id == id).Blocked));
        Receive(data);
    }

    public string Xaml { get; }

    [ObservableProperty]
    public partial IReadOnlyList<LaneView> Lanes { get; set; } = [];

    public IAsyncRelayCommand<string> MoveLeftCommand { get; }

    public IAsyncRelayCommand<string> MoveRightCommand { get; }

    public IAsyncRelayCommand<string> BlockCommand { get; }

    /// <summary>Puts a dragged card in a lane at an index, from the board's drag and drop.</summary>
    public IAsyncRelayCommand<(string Card, string Lane, int Index)> PlaceCommand { get; }

    public void Receive(KanbanData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Lanes = [.. data.Lanes.Select((lane, index) =>
            new LaneView(lane, [.. lane.Items.Select(item => CardView.Of(item, index == 0, index == data.Lanes.Count - 1))], index == data.Lanes.Count - 1))];
    }
}
