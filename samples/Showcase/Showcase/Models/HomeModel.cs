namespace Showcase.Models;

public sealed record HomeData(string Xaml);

public sealed class HomeModel(HomeData data)
{
    public string Xaml { get; } = data.Xaml;
}
