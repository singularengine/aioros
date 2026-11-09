using System.Collections.Immutable;

namespace Lumen.Data;

public sealed record IssueChanges(string Id, string Title, string Description, string Status, string Priority, string Assignee);

public interface IIssueFunctions
{
    Task<ImmutableArray<Issue>> ListAsync();

    Task<Issue?> FindAsync(string id);

    Task SaveAsync(IssueChanges changes);
}
