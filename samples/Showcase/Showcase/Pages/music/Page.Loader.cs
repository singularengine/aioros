using Aioros.Generated;
using Aioros.Pages;
using Showcase.Models;

namespace Showcase.Pages.Music;

public static class MusicLoader
{
    public static LoaderResult<MusicData> Load(string? playing, int track = 1) =>
        LoaderResult.With(
            new MusicData(track, playing == "1", AiorosSources.Text("Pages/music/Page.axaml") ?? string.Empty),
            "Music · Aioros Showcase",
            new PageMetadata("A music player designed in XAML: an animated equalizer, gradients and transitions, served as HTML and CSS."));
}
