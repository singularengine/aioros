namespace Aioros.Routing.Templates;

internal ref struct PathSegments(ReadOnlySpan<char> path)
{
    private readonly ReadOnlySpan<char> _path = path;
    private int _next;

    public ReadOnlySpan<char> Current { get; private set; }

    public readonly ReadOnlySpan<char> Rest => _path[(_next - Current.Length)..].TrimEnd('/');

    public bool MoveNext()
    {
        while (_next < _path.Length && _path[_next] == '/')
        {
            _next++;
        }

        if (_next >= _path.Length)
        {
            return false;
        }

        var length = _path[_next..].IndexOf('/');
        var end = length < 0 ? _path.Length : _next + length;
        Current = _path[_next..end];
        _next = end;
        return true;
    }
}
