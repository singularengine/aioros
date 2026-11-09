namespace Showcase.Board;

public static class BoardSample
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public static BoardState State { get; } = new(
    [
        new("backlog", "Backlog", 0,
        [
            new EpicItem("AIO-1", "Native install from the browser", Priority.High, "edsger", Today.AddDays(21), false, 2, 7),
            new TaskItem("AIO-2", "Pricing page copy", Priority.Low, null, null, false, "Marketing"),
            new BugItem("AIO-3", "Date picker shows UTC on Safari", Priority.Medium, "ana", Today.AddDays(6), false, 4, "Safari 19"),
            new TaskItem("AIO-4", "Audit color contrast", Priority.Medium, "lee", Today.AddDays(10), false, "Accessibility"),
        ]),
        new("doing", "In progress", 3,
        [
            new BugItem("AIO-5", "Live charts stutter at 512 players", Priority.Urgent, "edsger", Today.AddDays(-2), false, 12, "Chromium 153"),
            new EpicItem("AIO-6", "Server-driven drag and drop", Priority.High, "ana", Today.AddDays(9), false, 4, 6),
            new TaskItem("AIO-7", "Search index migration", Priority.Medium, "dev", Today.AddDays(1), true, "Platform"),
        ]),
        new("review", "In review", 2,
        [
            new TaskItem("AIO-8", "Two-factor sign-in", Priority.High, "kim", Today, false, "Security"),
        ]),
        new("done", "Done", 0,
        [
            new EpicItem("AIO-9", "Plans without Avalonia", Priority.High, "edsger", Today.AddDays(-14), false, 9, 9),
            new BugItem("AIO-10", "Hover style lost after morph", Priority.Low, "lee", Today.AddDays(-30), false, 1, "Firefox 140"),
        ]),
    ]);
}
