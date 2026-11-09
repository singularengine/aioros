using System.Collections.Immutable;
using System.Net;

namespace Aioros.Runtime.Rendering;

/// <summary>Seals a library control's travelling state for the browser and opens what a post brings back (decision "Sealed control state").</summary>
public interface IStateSeal
{
    string Seal(string value);

    bool TryOpen(string sealedValue, out string value);
}

/// <summary>
/// The seal of the request being answered: the web target sets it for each request, so a state value node written on
/// that request's thread is sealed, in a document as in dynamics; without one (the adapter's renders, the desktop), a
/// state is written as it is.
/// </summary>
public static class StateSeals
{
    private const char NameEnd = ' ';

    private static readonly AsyncLocal<IStateSeal?> Ambient = new();

    public static IStateSeal? Current => Ambient.Value;

    public static IDisposable Use(IStateSeal? seal)
    {
        var outer = Ambient.Value;
        Ambient.Value = seal;
        return new Restore(outer);
    }

    /// <summary>A post's form with every travelling state (<c>_s{n}</c>) opened, or nothing when one is not this server's seal; unchanged without a seal.</summary>
    public static ImmutableDictionary<string, string>? Open(ImmutableDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (Current is not { } seal)
        {
            return form;
        }

        var opened = form;
        foreach (var (name, value) in form)
        {
            if (!PlanForms.IsSealedState(name))
            {
                continue;
            }

            if (!seal.TryOpen(value, out var plain) || !plain.StartsWith(name + NameEnd, StringComparison.Ordinal))
            {
                return null;
            }

            opened = opened.SetItem(name, plain[(name.Length + 1)..]);
        }

        return opened;
    }

    internal static string Sealed(string name, string value) => Current is { } seal ? seal.Seal(name + NameEnd + value) : value;

    internal static string Decoded(string html) => WebUtility.HtmlDecode(html);

    private sealed class Restore(IStateSeal? outer) : IDisposable
    {
        public void Dispose() => Ambient.Value = outer;
    }
}

/// <summary>
/// A library control's travelling state value in its hidden input: written as its body writes it without a seal, and as
/// the seal of that value when the request has one (<see cref="StateSeals"/>).
/// </summary>
public sealed record StateValueNode(string Name, ImmutableArray<IPlanNode> Body) : IPlanNode
{
    public void Write(ref HtmlOutput output, RenderScope scope)
    {
        if (StateSeals.Current is null)
        {
            PlanValues.WriteAll(ref output, Body.AsSpan(), scope);
            return;
        }

        var scratch = new System.Buffers.ArrayBufferWriter<byte>();
        var inner = new HtmlOutput(scratch);
        PlanValues.WriteAll(ref inner, Body.AsSpan(), scope);
        output.WriteText(StateSeals.Sealed(Name + ItemKeys.Suffix(output.ItemKey), StateSeals.Decoded(System.Text.Encoding.UTF8.GetString(scratch.WrittenSpan))));
    }
}
