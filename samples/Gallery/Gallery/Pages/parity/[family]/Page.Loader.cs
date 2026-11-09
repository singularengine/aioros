using Aioros.Pages;
using Gallery.Models;
using Gallery.Parity;
using Microsoft.AspNetCore.Hosting;

namespace Gallery.Pages.Parity;

public sealed class ParityFamilyLoader(IWebHostEnvironment environment)
{
    public async Task<LoaderResult<ParityFamily>> LoadAsync(string family, CancellationToken cancellationToken)
    {
        var live = "/controls/" + family.ToLowerInvariant();
        var data = await ParityFile.ReadAsync(environment.WebRootFileProvider, cancellationToken);
        if (data?.Routes.FirstOrDefault(route => route.Route == live) is not { } route || !ShellLoader.Names.TryGetValue(live, out var title))
        {
            return LoaderResult.NotFound();
        }

        return LoaderResult.With(
            new ParityFamily(
                title,
                live,
                data.Measured,
                ParityFile.Image(route.Slug, "avalonia"),
                ParityFile.Image(route.Slug, "web"),
                ParityFile.Image(route.Slug, "diff"),
                [.. route.Variants.Select(variant => ParityScores.Of($"{variant.Theme}, {variant.State}", live, [variant]))]),
            title + " parity");
    }
}
