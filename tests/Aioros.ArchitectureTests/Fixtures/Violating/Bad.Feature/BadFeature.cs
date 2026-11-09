using Avalonia;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Bad.Feature;

public class BadFeature
{
    private readonly object _gate = new();

    public void Touch()
    {
        // a comment the rules forbid
        lock (_gate)
        {
        }
    }
}

public record Settings(string Name);
