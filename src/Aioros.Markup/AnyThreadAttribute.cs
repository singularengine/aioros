namespace Aioros.Markup;

/// <summary>
/// Declares that a library's type runs on any thread: its members, the commands it exposes included, create and touch no
/// Avalonia object, dispatcher or platform service, as androidx's <c>@AnyThread</c> declares a method safe on any thread.
/// Aioros's generator reads it for an app's pages, so a button bound to such a command is run where the page's plan
/// runs on the web, even while Avalonia runs in the process; without it a library's command runs on Avalonia's thread.
/// It is a library's declaration: an app's own models need none, since the generator reads their code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AnyThreadAttribute : Attribute;
