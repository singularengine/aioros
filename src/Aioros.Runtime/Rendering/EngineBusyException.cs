namespace Aioros.Runtime.Rendering;

/// <summary>
/// The engine refused a request's job because as many already wait on its thread as it queues: the request is answered
/// <c>503</c> with <c>Retry-After</c>, as ASP.NET Core's concurrency limiter answers a full queue.
/// </summary>
public sealed class EngineBusyException() : Exception("The page engine's queue is full; retry shortly.");
