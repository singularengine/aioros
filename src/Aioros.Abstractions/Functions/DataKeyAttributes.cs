namespace Aioros.Functions;

/// <summary>
/// Declares the data keys a server function reads, on its server implementation (never on the contract): a loader that
/// calls it reads those keys, and a command that invalidates one of them refreshes that loader's view, as Next.js's
/// <c>cacheTag</c> tags a cached read. A key may name an argument, <c>"issue:{id}"</c>, or an argument's member,
/// <c>"issue:{changes.Id}"</c>, written in the invariant culture.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ReadsAttribute(params string[] keys) : Attribute
{
    public IReadOnlyList<string> Keys { get; } = keys;
}

/// <summary>
/// Declares the data keys a server function changes, on its server implementation: after a command that called it
/// succeeds, the views whose loaders read one of those keys run again, as Next.js's <c>revalidateTag</c> and SolidStart's
/// <c>revalidate(key)</c>. A function with neither attribute has no key information, and a command that calls it
/// refreshes the whole page.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class InvalidatesAttribute(params string[] keys) : Attribute
{
    public IReadOnlyList<string> Keys { get; } = keys;
}

/// <summary>
/// The data keys of the work running now, for keys only known at run time: a loader that takes it records what it read
/// (SvelteKit's <c>depends</c>), and a command's services record what they changed (SvelteKit's <c>invalidate</c>).
/// </summary>
public interface IDataKeys
{
    void Read(string key);

    void Invalidate(string key);
}

/// <summary>
/// Shares a server function's result among the loaders of one page load, on its server implementation: two loaders that
/// call it with equal arguments in the same load run it once and get the same result, as React's <c>cache</c> shares a
/// call within one server request. Each loader keeps its own scope, so the result must be immutable data (records,
/// read-only collections) and never a tracked entity; a command's calls are never memoized, and the next load (another
/// request, a navigation or the revalidation after a command) calls it again.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MemoAttribute : Attribute
{
}

/// <summary>
/// Keeps a result across requests for a lifetime, as Next.js's <c>"use cache"</c> with <c>cacheLife</c> keeps a cached
/// read until its tags are revalidated or it expires. On a server implementation's method, its in-process caller serves
/// the loaders of later loads from the data cache, keyed by the function and its arguments, and the user unless
/// <see cref="Shared"/>; the entry goes when a function invalidates one of its <c>[Reads]</c> keys or when the lifetime
/// ends. On a loader, it declares that the loader's data is the same for every visitor for that long, so a page whose
/// loaders all declare it, and which has no access rules, is kept whole in the page cache. The result must be immutable.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class CachedAttribute(int seconds) : Attribute
{
    /// <summary>How long a kept result is served, in seconds.</summary>
    public int Seconds { get; } = seconds;

    /// <summary>
    /// Whether one result serves every user. Off by default: a result is kept per signed-in user and never for an
    /// anonymous visitor, since a function could read the request; a function that reads the request cannot be shared.
    /// </summary>
    public bool Shared { get; set; }
}
