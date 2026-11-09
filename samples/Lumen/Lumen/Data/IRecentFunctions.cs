namespace Lumen.Data;

public interface IRecentFunctions
{
    Task<Issue[]> RecentAsync();

    Task<int> UnreadAsync();

    Task MarkReadAsync();

    Task<IssueActivity[]> ActivityAsync(string[] ids);

    Task WatchAsync(string id);
}
