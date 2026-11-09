using System.Globalization;

namespace LumenBlazor.Data;

public sealed record Person(string Name, string Initials, string Color);
public sealed record Label(string Name, string Color);

public sealed record Issue(string Id, string Title, string Description, string Status, string Priority,
    Person Assignee, Label[] Labels, string Team, DateTime Updated)
{
    public string Href => "/issues/" + Id;
    public string StatusIcon => "status-" + Status;
    public string StatusColor => Store.Statuses[Status].Color;
    public string StatusName => Store.Statuses[Status].Name;
    public string PriorityIcon => "prio-" + Priority;
    public string PriorityColor => Priority == "urgent" ? "#F2994A" : "#8B8F98";
    public string PriorityName => Store.Priorities[Priority];
    public string UpdatedText => Store.Ago(Updated);
}

public sealed record Status(string Key, string Name, string Color);

public sealed record Notification(string Icon, string Color, Person Who, string Text, Issue Issue, DateTime At, bool Unread)
{
    public string Href => Issue.Href;
    public string Time => Store.Ago(At);
}

/// In-memory data, deterministic (seeded) so every implementation of the app shows the same content.
/// Reads are lock-free snapshots; writers publish a new array.
public sealed class Store
{
    public static readonly Dictionary<string, Status> Statuses = new[]
    {
        new Status("progress", "In progress", "#F2C94C"),
        new Status("review", "In review", "#4CB782"),
        new Status("todo", "Todo", "#B0B3BC"),
        new Status("backlog", "Backlog", "#8B8F98"),
        new Status("done", "Done", "#5E6AD2"),
        new Status("canceled", "Canceled", "#8B8F98"),
    }.ToDictionary(s => s.Key);

    public static readonly Dictionary<string, string> Priorities = new()
    {
        ["urgent"] = "Urgent", ["high"] = "High", ["medium"] = "Medium", ["low"] = "Low", ["none"] = "No priority",
    };

    public static readonly Person[] People =
    [
        new("Edsger Dijkstra", "ED", "#5E6AD2"), new("Ada Lovelace", "AL", "#E5484D"), new("Alan Turing", "AT", "#30A46C"),
        new("Grace Hopper", "GH", "#F76B15"), new("Linus Torvalds", "LT", "#0090FF"), new("Margaret Hamilton", "MH", "#8E4EC6"),
        new("Barbara Liskov", "BL", "#D6409F"), new("Dennis Ritchie", "DR", "#12A594"),
    ];

    public static readonly Label[] Labels =
    [
        new("Bug", "#EB5757"), new("Feature", "#BB87FC"), new("Improvement", "#4EA7FC"),
        new("Design", "#F2994A"), new("Docs", "#4CB782"), new("Performance", "#F2C94C"),
    ];

    private static readonly string[] Titles =
    [
        "Server-side rendering of XAML pages", "Dark mode across the app", "Collapsible sidebar on mobile",
        "Entrance animations for cards", "Pagination in the issue list", "Export report to CSV",
        "Keyboard shortcuts in the editor", "Real-time notifications", "Filter by label and priority",
        "Migrate authentication to OIDC", "Optimize dashboard queries", "Kanban view per team",
        "Comments with mentions", "Issue change history", "GitHub integration",
        "Issue templates", "Global search with ⌘K", "Form accessibility",
        "Avatar image cache", "Onboarding for new members", "API rate limits",
        "Document the render plan", "Visual regression tests", "Drag and drop on the board",
        "Per-user time zone", "Event webhooks", "Compact mode for lists",
        "Reduce the JS bundle", "Usage metrics per team", "Import from Jira",
        "Calendar view", "Custom labels", "Archive inactive projects",
        "Enterprise SSO", "Improve light theme contrast", "Email reminders",
    ];

    private static readonly string[] Teams = ["Engineering", "Design", "Product"];

    private volatile Issue[] _issues;
    private readonly Lock _lock = new();
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0);

    public Store()
    {
        var rng = new Random(7);
        string[] statuses = ["progress", "progress", "review", "todo", "todo", "todo", "backlog", "backlog", "done", "done", "canceled"];
        string[] priorities = ["urgent", "high", "high", "medium", "medium", "medium", "low", "low", "none"];
        _issues = Titles.Select((title, i) =>
        {
            // Every issue has at least one label (lists learn their item template from the first item).
            var labels = Labels.OrderBy(_ => rng.Next()).Take(1 + rng.Next(2)).ToArray();
            return new Issue($"LUM-{101 + i}", title,
                $"Context and acceptance criteria for \"{title.ToLowerInvariant()}\". Review with the team before closing.",
                i < 2 ? (i == 0 ? "progress" : "review") : statuses[rng.Next(statuses.Length)],
                priorities[rng.Next(priorities.Length)], People[rng.Next(People.Length)], labels,
                Teams[rng.Next(Teams.Length)], Now.AddMinutes(-rng.Next(10, 60 * 24 * 9)));
        }).ToArray();
    }

    public Issue[] Issues => _issues;
    public Issue? Find(string id) => Array.Find(_issues, i => i.Id == id);

    public void Update(Issue issue)
    {
        lock (_lock)
        {
            var copy = (Issue[])_issues.Clone();
            var i = Array.FindIndex(copy, x => x.Id == issue.Id);
            if (i >= 0) copy[i] = issue with { Updated = DateTime.Now };
            _issues = copy;
        }
    }

    public Notification[] Notifications()
    {
        var issues = _issues;
        (string Icon, string Color, string Text)[] kinds =
        [
            ("issues", "#5E6AD2", "assigned you"), ("activity", "#4CB782", "changed the status of"),
            ("bell", "#F2994A", "mentioned you in"), ("check", "#4CB782", "closed"),
        ];
        return Enumerable.Range(0, 9).Select(i =>
        {
            var k = kinds[i % kinds.Length];
            return new Notification(k.Icon, k.Color, People[(i * 3 + 1) % People.Length], k.Text, issues[(i * 5) % issues.Length],
                Now.AddMinutes(-(i * 47 + 6)), i < 3);
        }).ToArray();
    }

    public static string Ago(DateTime t)
    {
        var d = Now - t;
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        return d.TotalMinutes < 60 ? $"{(int)d.TotalMinutes}m ago"
            : d.TotalHours < 24 ? $"{(int)d.TotalHours}h ago"
            : $"{(int)d.TotalDays}d ago";
    }

    public static string Display(DateTime t) => t.ToString("MMM d", CultureInfo.InvariantCulture);
}
