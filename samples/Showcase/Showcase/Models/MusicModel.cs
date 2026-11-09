using System.Globalization;

namespace Showcase.Models;

public sealed record Album(string Title, string Artist, string Color, string Shade);

public sealed record Song(int Number, string Title, Album Album, int Seconds);

public sealed record SongRow(string Number, string Title, string Artist, string Length, string Href, bool Current, bool Other);

public sealed record AlbumCard(string Title, string Artist, string Color, string Shade, string Href);

public sealed record MusicData(int Track, bool Playing, string Xaml);

public sealed class MusicModel
{
    private static readonly Album[] Albums =
    [
        new("Neon Tides", "Lumen Drift", "#5B5BD6", "#1E1B4B"),
        new("Paper Suns", "Ola Verde", "#E8590C", "#3B1405"),
        new("Glass Gardens", "Mira Sol", "#0CA678", "#062F24"),
        new("Night Signals", "The Northbound", "#D6336C", "#3A0A1C"),
    ];

    private static readonly Song[] Songs =
    [
        new(1, "Afterglow", Albums[0], 214), new(2, "Signal Lines", Albums[0], 187), new(3, "Low Orbit", Albums[0], 243),
        new(4, "Copper Light", Albums[1], 201), new(5, "Siesta", Albums[1], 176), new(6, "Paper Suns", Albums[1], 228),
        new(7, "Greenhouse", Albums[2], 195), new(8, "Fern", Albums[2], 168), new(9, "Morning Glass", Albums[2], 252),
        new(10, "Northbound", Albums[3], 233), new(11, "Static Hearts", Albums[3], 189), new(12, "Last Train", Albums[3], 261),
    ];

    private readonly Song _song;

    public string Xaml { get; }

    public string Song { get; } = string.Empty;

    public string Artist { get; } = string.Empty;

    public string AlbumTitle { get; } = string.Empty;

    public string Color { get; } = string.Empty;

    public string Shade { get; } = string.Empty;

    public string Initial { get; } = string.Empty;

    public bool Playing { get; }

    public bool Paused => !Playing;

    public string PlayHref { get; } = string.Empty;

    public string PreviousHref { get; } = string.Empty;

    public string NextHref { get; } = string.Empty;

    public string Elapsed { get; } = string.Empty;

    public string Length { get; } = string.Empty;

    public double Progress { get; }

    public IReadOnlyList<SongRow> Queue { get; } = [];

    public IReadOnlyList<AlbumCard> Recommended { get; } =
        [.. Albums.Select(album => new AlbumCard(album.Title, album.Artist, album.Color, album.Shade, Href(Array.FindIndex(Songs, song => song.Album == album) + 1, true)))];

    public MusicModel(MusicData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Xaml = data.Xaml;
        var number = Math.Clamp(data.Track, 1, Songs.Length);
        Playing = data.Playing;
        _song = Songs[number - 1];
        Song = _song.Title;
        Artist = _song.Album.Artist;
        AlbumTitle = _song.Album.Title;
        Color = _song.Album.Color;
        Shade = _song.Album.Shade;
        Initial = _song.Album.Title[..1];
        PlayHref = Href(number, !Playing);
        PreviousHref = Href(number == 1 ? Songs.Length : number - 1, Playing);
        NextHref = Href(number == Songs.Length ? 1 : number + 1, Playing);
        var elapsed = (int)(_song.Seconds * 0.38);
        Elapsed = Time(elapsed);
        Length = Time(_song.Seconds);
        Progress = Math.Round(elapsed * 100.0 / _song.Seconds, 1);
        Queue = [.. Songs.Select(song => new SongRow(song.Number.ToString("00", CultureInfo.InvariantCulture), song.Title, song.Album.Artist, Time(song.Seconds), Href(song.Number, true), song == _song, song != _song))];
    }

    private static string Href(int number, bool playing) =>
        "/music?track=" + number.ToString(CultureInfo.InvariantCulture) + (playing ? "&playing=1" : string.Empty);

    private static string Time(int seconds) => string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
}
