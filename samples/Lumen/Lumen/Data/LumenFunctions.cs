using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Aioros.Functions;

namespace Lumen.Data;

public sealed class IssueFunctions(Store store) : IIssueFunctions
{
    [Cached(60, Shared = true)]
    [Reads("issues")]
    public Task<ImmutableArray<Issue>> ListAsync() => Task.FromResult(ImmutableCollectionsMarshal.AsImmutableArray(store.Issues));

    [Cached(60, Shared = true)]
    [Reads("issue:{id}")]
    public Task<Issue?> FindAsync(string id) => Task.FromResult(store.Find(id));

    [Invalidates("issues", "issue:{changes.Id}")]
    public Task SaveAsync(IssueChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentException.ThrowIfNullOrWhiteSpace(changes.Title);
        var issue = store.Find(changes.Id) ?? throw new KeyNotFoundException($"No issue {changes.Id}.");
        store.Update(issue with
        {
            Title = changes.Title.Trim(),
            Description = changes.Description,
            Status = Store.Statuses[changes.Status].Key,
            Priority = Store.Priorities.ContainsKey(changes.Priority) ? changes.Priority : throw new ArgumentException("Unknown priority.", nameof(changes)),
            Assignee = Store.People.First(person => person.Name == changes.Assignee),
        });
        return Task.CompletedTask;
    }
}

public sealed class ProfileFunctions(Store store) : IProfileFunctions
{
    [Invalidates("profile")]
    public Task SaveAsync(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);
        if (!profile.Email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("The email is not valid.", nameof(profile));
        }

        store.Update(profile);
        return Task.CompletedTask;
    }
}

public sealed class RecentFunctions(Store store) : IRecentFunctions
{
    [Reads("issues")]
    public Task<Issue[]> RecentAsync() => Task.FromResult(store.Recent());

    [Reads("recent")]
    public Task<int> UnreadAsync() => Task.FromResult(store.Recent().Count(issue => !store.IsRead(issue)));

    [Invalidates("recent")]
    public Task MarkReadAsync()
    {
        store.MarkRead(store.Recent());
        return Task.CompletedTask;
    }

    [Reads("activity")]
    public Task<IssueActivity[]> ActivityAsync(string[] ids) => Task.FromResult(store.Activity(ids));

    [Invalidates("activity:{id}")]
    public Task WatchAsync(string id)
    {
        store.ToggleWatch(id);
        return Task.CompletedTask;
    }
}
