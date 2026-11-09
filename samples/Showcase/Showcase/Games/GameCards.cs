using System.Diagnostics;
using System.Globalization;

namespace Showcase.Games;

public sealed record GameCard(
    int Id, string Title, string Studio, string Genre, string Tags, string Price, string Original, bool OnSale, string Off,
    string Score, string Rating, string RatingColor, string Art, string Href, string Owners);

public sealed record ChipLink(string Label, string Href, bool Current, string Count = "");

/// <summary>Several queries run at once, each on its own pooled context; the page shows how long they took together and on which threads.</summary>
public sealed record ParallelReport(string Text)
{
    public static async Task<(T1, T2, T3, T4, ParallelReport)> RunAsync<T1, T2, T3, T4>(Func<Task<T1>> first, Func<Task<T2>> second, Func<Task<T3>> third, Func<Task<T4>> fourth)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(third);
        ArgumentNullException.ThrowIfNull(fourth);
        var clock = Stopwatch.StartNew();
        var threads = new int[4];
        var a = Task.Run(async () => { var result = await first(); threads[0] = Environment.CurrentManagedThreadId; return result; });
        var b = Task.Run(async () => { var result = await second(); threads[1] = Environment.CurrentManagedThreadId; return result; });
        var c = Task.Run(async () => { var result = await third(); threads[2] = Environment.CurrentManagedThreadId; return result; });
        var d = Task.Run(async () => { var result = await fourth(); threads[3] = Environment.CurrentManagedThreadId; return result; });
        await Task.WhenAll(a, b, c, d);
        var text = string.Create(CultureInfo.InvariantCulture,
            $"4 queries ran in parallel in {clock.Elapsed.TotalMilliseconds:0.0} ms on threads {string.Join(", ", threads.Distinct().Order())}");
        return (await a, await b, await c, await d, new ParallelReport(text));
    }
}

public static class GameCards
{
    public static GameCard From(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var total = game.Positive + game.Negative;
        var share = total == 0 ? 0 : (double)game.Positive / total;
        var price = game.Price * (100 - game.Discount) / 100;
        var (rating, color) = Rating(share, total);
        return new GameCard(
            game.Id,
            game.Title,
            game.Studio,
            game.Genre,
            string.Join(" · ", game.Tags.Take(3)),
            Money(price),
            Money(game.Price),
            game.Discount > 0,
            "-" + game.Discount.ToString(CultureInfo.InvariantCulture) + "%",
            total == 0 ? "–" : share.ToString("0%", CultureInfo.InvariantCulture),
            rating,
            color,
            GameArt.Url(game.Id, "cover"),
            "/games/" + game.Id.ToString(CultureInfo.InvariantCulture),
            game.Owners.ToString("N0", CultureInfo.InvariantCulture) + " owners");
    }

    public static string Money(decimal price) => price == 0 ? "Free to play" : price.ToString("$0.00", CultureInfo.InvariantCulture);

    public static (string Rating, string Color) Rating(double share, int total) => total switch
    {
        0 => ("No reviews yet", "#8F98A0"),
        _ when share >= 0.95 && total >= 500 => ("Overwhelmingly Positive", "#66C0F4"),
        _ when share >= 0.8 => ("Very Positive", "#66C0F4"),
        _ when share >= 0.7 => ("Mostly Positive", "#66C0F4"),
        _ when share >= 0.4 => ("Mixed", "#B9A074"),
        _ => ("Mostly Negative", "#C35C2C"),
    };
}
