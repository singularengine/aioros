using System.Collections.Immutable;
using System.Text;

namespace Aioros.Runtime.Rendering;

public sealed class PlanBuilder
{
    private readonly Stack<ImmutableArray<IPlanNode>.Builder> _scopes = new([ImmutableArray.CreateBuilder<IPlanNode>()]);
    private readonly StringBuilder _literal = new();
    private string? _pending;
    private bool _apart;

    /// <summary>Adds literal text; a text alone between two nodes becomes its literal node as it is, without a copy.</summary>
    public void Literal(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_apart)
        {
            Flush();
        }

        if (_pending is null && _literal.Length == 0)
        {
            _pending = text;
            return;
        }

        Literal(text.AsSpan());
    }

    public void Literal(ReadOnlySpan<char> text)
    {
        if (_apart)
        {
            Flush();
        }

        if (_pending is not null)
        {
            _literal.Append(_pending);
            _pending = null;
        }

        _literal.Append(text);
    }

    /// <summary>
    /// Adds literal text as a node of its own, without copying it into the text before it: for a render no plan is kept
    /// from, where two literal nodes write what one would.
    /// </summary>
    public void LiteralApart(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Flush();
        (_pending, _apart) = (text, true);
    }

    public void Add(IPlanNode node)
    {
        Flush();
        _scopes.Peek().Add(node);
    }

    public void Begin()
    {
        Flush();
        _scopes.Push(ImmutableArray.CreateBuilder<IPlanNode>());
    }

    public ImmutableArray<IPlanNode> End()
    {
        Flush();
        return _scopes.Pop().ToImmutable();
    }

    public ImmutableArray<IPlanNode> Build()
    {
        Flush();
        return _scopes.Peek().ToImmutable();
    }

    private void Flush()
    {
        if (_pending is not null)
        {
            _scopes.Peek().Add(new LiteralNode(_pending));
            (_pending, _apart) = (null, false);
            return;
        }

        if (_literal.Length == 0)
        {
            return;
        }

        _scopes.Peek().Add(new LiteralNode(_literal.ToString()));
        _literal.Clear();
    }
}
