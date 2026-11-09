using System.Collections.Immutable;
using Aioros.Functions;
using Aioros.Navigation;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Pages;

/// <summary>
/// The services of one loader: its own scope's, so loaders running in parallel never share a scoped instance, except the
/// page's navigator, which belongs to the page the loader loads for. It is also the loader's <see cref="IDataKeys"/>: the
/// keys it reads, itself or through the in-process callers it takes, are its own (SvelteKit's dependency tracking per
/// load). Until a key is read it holds only its scope, so a loader that reads none pays nothing for keys. Its in-process
/// callers' <c>[Memo]</c> calls go through the memo of the load it belongs to, shared by the load's loaders, and their
/// <c>[Cached]</c> calls through the app's data cache, which keeps data per user apart by its <see cref="User"/>.
/// </summary>
internal sealed class LoaderServices(IServiceProvider page, IServiceProvider scope, PageLoad load, System.Security.Claims.ClaimsPrincipal user) : IServiceProvider, IDataKeys, ICallMemo, ICallerIdentity
{
    private object _scope = scope;

    /// <summary>The keys the loader read, in the order first read.</summary>
    public ImmutableArray<string> Keys => Volatile.Read(ref _scope) is ReadScope read ? read.Keys : [];

    /// <summary>The user the load runs for, which keeps data cached per user apart.</summary>
    public System.Security.Claims.ClaimsPrincipal User { get; } = user;

    /// <summary>Whether the loader read a key.</summary>
    public bool ReadAny => Volatile.Read(ref _scope) is ReadScope;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(INavigator) || serviceType == typeof(PageNavigator))
        {
            return page.GetService(serviceType);
        }

        if (serviceType == typeof(IDataKeys))
        {
            return this;
        }

        var service = Scope.GetService(serviceType);
        if (service is IKeyedCalls keyed)
        {
            keyed.Keys = this;
        }

        return service;
    }

    void IDataKeys.Read(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ImmutableInterlocked.Update(ref _scope, static (state, key) => state switch
        {
            ReadScope read when read.Keys.Contains(key) => read,
            ReadScope read => read with { Keys = read.Keys.Add(key) },
            _ => new ReadScope((IServiceProvider)state, [key]),
        }, key);
    }

    public void Invalidate(string key)
    {
    }

    public Task<TResult> CallAsync<TResult>(string function, object arguments, Func<Task<TResult>> call) => load.CallAsync(function, arguments, call);

    private IServiceProvider Scope => Volatile.Read(ref _scope) is ReadScope read ? read.Scope : (IServiceProvider)_scope;

    private sealed record ReadScope(IServiceProvider Scope, ImmutableArray<string> Keys);
}
