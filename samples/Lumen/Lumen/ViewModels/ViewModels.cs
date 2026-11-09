using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Data;

namespace Lumen.ViewModels;

public sealed record IssueList(Issue[] Issues);

public sealed record IssueGroup(string Name, string Icon, string Color, int Count, Issue[] Issues);

public sealed class IssuesViewModel(IssueList data)
{
    public IssueGroup[] Groups { get; } = Store.Statuses.Values
        .Select(s => new IssueGroup(s.Name, "status-" + s.Key, s.Color, 0,
            data.Issues.Where(i => i.Status == s.Key).OrderByDescending(i => i.Updated).ToArray()))
        .Where(g => g.Issues.Length > 0)
        .Select(g => g with { Count = g.Issues.Length })
        .ToArray();

    public string Summary { get; } = $"{data.Issues.Length} issues · {data.Issues.Count(i => i.Status is "progress" or "review")} active";
}

public sealed record BoardColumn(string Name, string Icon, string Color, int Count, Issue[] Issues);

public sealed class ProjectsViewModel(IssueList data)
{
    public BoardColumn[] Columns { get; } = new[] { "backlog", "todo", "progress", "review", "done" }
        .Select(k => Store.Statuses[k])
        .Select(s =>
        {
            var items = data.Issues.Where(i => i.Status == s.Key).OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
            return new BoardColumn(s.Name, "status-" + s.Key, s.Color, items.Length, items);
        })
        .ToArray();
}

public sealed record IssueData(Issue Issue);

public sealed partial class IssueViewModel : ObservableValidator
{
    private readonly IIssueFunctions _issues;
    private Issue _issue;

    public IssueViewModel(IssueData data, IIssueFunctions issues)
    {
        _issues = issues;
        _issue = data.Issue;
        Show(_issue);
    }

    public static readonly string[] StatusOptions = Store.Statuses.Values.Select(s => s.Name).ToArray();
    public static readonly string[] PriorityOptions = Store.Priorities.Values.ToArray();
    public static readonly string[] PeopleOptions = Store.People.Select(p => p.Name).ToArray();

    public string Id => _issue.Id;
    public string StatusIcon => _issue.StatusIcon;
    public string StatusColor => _issue.StatusColor;
    public string PriorityIcon => _issue.PriorityIcon;
    public string PriorityColor => _issue.PriorityColor;
    public Label[] Labels => _issue.Labels;
    public string Team => _issue.Team;
    public string Updated => _issue.UpdatedText;
    public string AssigneeInitials => _issue.Assignee.Initials;
    public string AssigneeColor => _issue.Assignee.Color;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Title is required.")]
    private string _issueTitle = "";

    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _priority = "";
    [ObservableProperty] private string _assignee = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSaved))] private string _saved = "";

    public bool HasSaved => Saved.Length > 0;

    public void Receive(IssueData data)
    {
        _issue = data.Issue;
        Show(_issue);
        OnPropertyChanged(string.Empty);
    }

    private void Show(Issue issue) =>
        (IssueTitle, Description, Status, Priority, Assignee) = (issue.Title, issue.Description, issue.StatusName, issue.PriorityName, issue.Assignee.Name);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;
        await _issues.SaveAsync(new IssueChanges(_issue.Id, IssueTitle.Trim(), Description,
            Store.Statuses.Values.First(s => s.Name == Status).Key,
            Store.Priorities.First(p => p.Value == Priority).Key,
            Assignee));
        Saved = "Changes saved.";
    }
}

public sealed record InboxData(Notification[] Items);

public sealed class InboxViewModel(InboxData data)
{
    public Notification[] Items { get; } = data.Items;
    public string Summary { get; } = $"{data.Items.Count(n => n.Unread)} unread";
}

public sealed partial class SettingsViewModel : ObservableValidator
{
    private readonly IProfileFunctions _profiles;

    public SettingsViewModel(Profile profile, IProfileFunctions profiles)
    {
        _profiles = profiles;
        Show(profile);
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    private string _name = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "The email is not valid.")]
    [RegularExpression(".*@.*", ErrorMessage = "The email is not valid.")]
    private string _email = "";

    [ObservableProperty] private bool _weeklyDigest;
    [ObservableProperty] private bool _desktopAlerts;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSaved))] private string _saved = "";

    public bool HasSaved => Saved.Length > 0;

    public void Receive(Profile profile) => Show(profile);

    private void Show(Profile profile) =>
        (Name, Email, WeeklyDigest, DesktopAlerts) = (profile.Name, profile.Email, profile.WeeklyDigest, profile.DesktopAlerts);

    [RelayCommand]
    private async Task SaveAsync()
    {
        Saved = "";
        ValidateAllProperties();
        if (HasErrors) return;
        await _profiles.SaveAsync(new Profile(Name.Trim(), Email.Trim(), WeeklyDigest, DesktopAlerts));
        Saved = "Changes saved.";
    }
}

public sealed record RecentIssuesData(Issue[] Issues, int Unread);

/// A view with its own loader and command: "Mark all read" runs a server function that invalidates "recent", the read
/// state only this view reads, so this view alone refreshes and the board, which reads "issues", does not.
public sealed partial class RecentIssuesViewModel : ObservableObject
{
    private readonly IRecentFunctions _recent;

    public RecentIssuesViewModel(RecentIssuesData data, IRecentFunctions recent)
    {
        _recent = recent;
        Receive(data);
    }

    [ObservableProperty] private Issue[] _issues = [];
    [ObservableProperty] private string _summary = "";

    public void Receive(RecentIssuesData data) =>
        (Issues, Summary) = (data.Issues, data.Unread == 0 ? "All caught up" : $"{data.Unread} unread of {data.Issues.Length} updated lately");

    [RelayCommand]
    private Task MarkReadAsync() => _recent.MarkReadAsync();
}

/// One issue's activity beside its row in Recent issues, an item view with a batch loader: the list's issues are loaded in
/// one call for every row, and "Watch" in one row refreshes that row alone, through the batch loader with its key.
public sealed partial class IssueWatchViewModel : ObservableObject
{
    private readonly IRecentFunctions _recent;
    private string _id = "";

    public IssueWatchViewModel(IssueActivity data, IRecentFunctions recent)
    {
        _recent = recent;
        Receive(data);
    }

    [ObservableProperty] private string _comments = "";
    [ObservableProperty] private string _watch = "";

    public void Receive(IssueActivity data) =>
        (_id, Comments, Watch) = (data.Id, data.Comments == 1 ? "1 comment" : $"{data.Comments} comments", data.Watching ? "Watching" : "Watch");

    [RelayCommand]
    private Task ToggleAsync() => _recent.WatchAsync(_id);
}
