using Aioros.Pages;
using Gallery.Models;
using Gallery.Parity;
using Microsoft.AspNetCore.Hosting;

namespace Gallery.Pages.Parity;

public sealed class ParityLoader(IWebHostEnvironment environment)
{
    public async Task<LoaderResult<ParityOverview>> LoadAsync(CancellationToken cancellationToken)
    {
        var data = await ParityFile.ReadAsync(environment.WebRootFileProvider, cancellationToken);
        var families = (data?.Routes ?? []).Where(route => ShellLoader.Names.ContainsKey(route.Route)).ToList();
        if (data is null || families.Count == 0)
        {
            return LoaderResult.NotFound();
        }

        return new ParityOverview(
            data.Measured,
            [.. families.Select(route => ParityScores.Of(ShellLoader.Names[route.Route], "/parity/" + route.Route.Split('/')[^1], route.Variants))],
            ParityScores.Of("All controls", "/parity", [.. families.SelectMany(route => route.Variants)]));
    }
}
