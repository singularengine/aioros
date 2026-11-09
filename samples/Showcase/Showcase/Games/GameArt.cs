using System.Globalization;
using SkiaSharp;

namespace Showcase.Games;

/// <summary>
/// Original art for every game, drawn from its id: a scene that fits its genre (space, mountains, a city, a forest, a
/// road or hills) in its colors, with the title on the cover. Real store art belongs to its publishers, so the demo
/// draws its own.
/// </summary>
public static class GameArt
{
    public const string Prefix = "/games/art/";

    public static string Url(int id, string kind) => Prefix + id.ToString(CultureInfo.InvariantCulture) + "/" + kind + ".png";

    public static bool TryParse(string path, out int id, out string kind)
    {
        id = 0;
        kind = string.Empty;
        if (!path.StartsWith(Prefix, StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = path[Prefix.Length..^4].Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out id) || parts[1] is not ("cover" or "shot1" or "shot2" or "shot3"))
        {
            return false;
        }

        kind = parts[1];
        return id is >= 1 and <= GameSeeder.GameCount;
    }

    public static byte[] Render(int id, string kind)
    {
        var game = GameCatalog.Game(id);
        var cover = kind == "cover";
        var (width, height) = cover ? (920, 430) : (1280, 720);
        var random = new Random(id * 997 + kind.GetHashCode(StringComparison.Ordinal) % 97);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        var dark = Shade(SKColor.Parse(game.Cover), 0.25f);
        var light = SKColor.Parse(game.Accent);
        Sky(canvas, width, height, dark, light, random);
        switch (Scene(game))
        {
            case "space":
                Space(canvas, width, height, light, random);
                break;
            case "city":
                City(canvas, width, height, light, random);
                break;
            case "forest":
                Forest(canvas, width, height, light, random);
                break;
            case "road":
                Road(canvas, width, height, light, random);
                break;
            case "hills":
                Hills(canvas, width, height, SKColor.Parse(game.Cover), random);
                break;
            default:
                Mountains(canvas, width, height, SKColor.Parse(game.Cover), light, random);
                break;
        }

        Vignette(canvas, width, height);
        if (cover)
        {
            Title(canvas, width, height, game.Title);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static string Scene(Game game) => game.Genre switch
    {
        "Shooter" or "Strategy" when game.Tags.Contains("Space") || game.Tags.Contains("Sci-fi") => "space",
        _ when game.Tags.Contains("Space") => "space",
        _ when game.Tags.Contains("Cyberpunk") => "city",
        "Horror" or "Survival" => "forest",
        "Racing" or "Sports" => "road",
        "Simulation" or "Puzzle" or "Indie" => "hills",
        "Action" or "Shooter" => "city",
        _ => "mountains",
    };

    private static void Sky(SKCanvas canvas, int width, int height, SKColor top, SKColor bottom, Random random)
    {
        using var paint = new SKPaint { IsAntialias = true };
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width * 0.3f, height), [top, Mix(top, bottom, 0.55f), Shade(bottom, 1.1f)], [0, 0.6f, 1], SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, paint);
        paint.Shader = SKShader.CreateRadialGradient(new SKPoint(width * (0.2f + (float)random.NextDouble() * 0.6f), height * 0.35f), width * 0.5f,
            [bottom.WithAlpha(110), bottom.WithAlpha(0)], SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, paint);
    }

    private static void Space(SKCanvas canvas, int width, int height, SKColor glow, Random random)
    {
        Stars(canvas, width, height, random, 260);
        using var paint = new SKPaint { IsAntialias = true };
        var center = new SKPoint(width * (0.55f + (float)random.NextDouble() * 0.25f), height * (0.45f + (float)random.NextDouble() * 0.2f));
        var radius = height * (0.32f + (float)random.NextDouble() * 0.15f);
        paint.Shader = SKShader.CreateRadialGradient(new SKPoint(center.X - radius * 0.4f, center.Y - radius * 0.4f), radius * 1.6f,
            [Shade(glow, 1.5f), glow, Shade(glow, 0.2f)], [0, 0.45f, 1], SKShaderTileMode.Clamp);
        canvas.DrawCircle(center, radius, paint);
        paint.Shader = null;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = radius * 0.05f;
        paint.Color = SKColors.White.WithAlpha(120);
        canvas.Save();
        canvas.RotateDegrees(-18, center.X, center.Y);
        canvas.DrawOval(center, new SKSize(radius * 1.8f, radius * 0.35f), paint);
        canvas.Restore();
        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.White.WithAlpha(200);
        var moon = new SKPoint(width * 0.18f, height * 0.25f);
        canvas.DrawCircle(moon, radius * 0.18f, paint);
    }

    private static void City(SKCanvas canvas, int width, int height, SKColor neon, Random random)
    {
        Stars(canvas, width, height / 2, random, 80);
        using var paint = new SKPaint { IsAntialias = true };
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, height * 0.25f), new SKPoint(0, height * 0.7f), [Shade(neon, 1.6f), neon.WithAlpha(0)], SKShaderTileMode.Clamp);
        canvas.DrawCircle(width * 0.5f, height * 0.55f, height * 0.33f, paint);
        paint.Shader = null;
        for (var layer = 0; layer < 3; layer++)
        {
            var baseColor = Shade(neon, 0.12f + layer * 0.06f);
            var x = -20f;
            while (x < width)
            {
                var w = 30 + random.Next(70);
                var h = height * (0.25f + (float)random.NextDouble() * (0.45f - layer * 0.1f));
                paint.Color = baseColor;
                canvas.DrawRect(x, height - h, w, h, paint);
                paint.Color = Shade(neon, 1.4f).WithAlpha((byte)(90 + random.Next(120)));
                for (var wy = height - h + 10; wy < height - 10; wy += 14)
                {
                    for (var wx = x + 6; wx < x + w - 6; wx += 10)
                    {
                        if (random.Next(5) == 0)
                        {
                            canvas.DrawRect(wx, wy, 4, 6, paint);
                        }
                    }
                }

                x += w + random.Next(8);
            }
        }
    }

    private static void Forest(SKCanvas canvas, int width, int height, SKColor moonColor, Random random)
    {
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = Shade(moonColor, 1.7f).WithAlpha(230);
        canvas.DrawCircle(width * (0.25f + (float)random.NextDouble() * 0.5f), height * 0.3f, height * 0.13f, paint);
        for (var layer = 0; layer < 4; layer++)
        {
            paint.Color = new SKColor(5, 8, 12, (byte)(110 + layer * 45));
            var ground = height * (0.62f + layer * 0.1f);
            for (var x = -40f; x < width + 40; x += 18 + random.Next(30))
            {
                var tree = height * (0.18f + (float)random.NextDouble() * 0.25f) * (1 + layer * 0.3f);
                using var path = new SKPath();
                path.MoveTo(x, ground - tree);
                path.LineTo(x - tree * 0.22f, ground);
                path.LineTo(x + tree * 0.22f, ground);
                path.Close();
                canvas.DrawPath(path, paint);
            }

            canvas.DrawRect(0, ground, width, height - ground, paint);
        }

        Fog(canvas, width, height);
    }

    private static void Road(SKCanvas canvas, int width, int height, SKColor neon, Random random)
    {
        Stars(canvas, width, height / 2, random, 60);
        using var paint = new SKPaint { IsAntialias = true };
        var horizon = height * 0.55f;
        var sun = new SKPoint(width * 0.5f, horizon);
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, horizon - height * 0.3f), new SKPoint(0, horizon), [new SKColor(255, 214, 102), Shade(neon, 1.3f)], SKShaderTileMode.Clamp);
        canvas.DrawCircle(sun, height * 0.28f, paint);
        paint.Shader = null;
        paint.Color = Shade(neon, 0.15f);
        canvas.DrawRect(0, horizon, width, height - horizon, paint);
        for (var stripe = 0; stripe < 5; stripe++)
        {
            canvas.DrawRect(sun.X - height * 0.3f, horizon - 12 - stripe * 16, height * 0.6f, 4 + stripe, paint);
        }

        paint.Color = Shade(neon, 1.5f).WithAlpha(170);
        paint.StrokeWidth = 2;
        for (var line = -12; line <= 12; line++)
        {
            canvas.DrawLine(sun.X + line * 12, horizon, sun.X + line * width * 0.12f, height, paint);
        }

        for (var row = 1; row < 10; row++)
        {
            var y = horizon + (height - horizon) * MathF.Pow(row / 10f, 2);
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    private static void Hills(SKCanvas canvas, int width, int height, SKColor color, Random random)
    {
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = new SKColor(255, 236, 179, 235);
        canvas.DrawCircle(width * (0.65f + (float)random.NextDouble() * 0.2f), height * 0.28f, height * 0.11f, paint);
        for (var cloud = 0; cloud < 5; cloud++)
        {
            paint.Color = SKColors.White.WithAlpha(60);
            var cx = (float)random.NextDouble() * width;
            var cy = height * (0.1f + (float)random.NextDouble() * 0.3f);
            canvas.DrawOval(cx, cy, 60 + random.Next(60), 14 + random.Next(10), paint);
        }

        for (var layer = 0; layer < 4; layer++)
        {
            paint.Color = Mix(Shade(color, 1.3f - layer * 0.25f), new SKColor(20, 40, 30), layer * 0.2f);
            using var path = new SKPath();
            var baseline = height * (0.5f + layer * 0.12f);
            path.MoveTo(0, height);
            path.LineTo(0, baseline);
            for (var x = 0; x <= width; x += 40)
            {
                path.LineTo(x, baseline - (float)(Math.Sin((x + layer * 130 + random.Next(20)) / (120.0 + layer * 30)) * height * 0.06));
            }

            path.LineTo(width, height);
            path.Close();
            canvas.DrawPath(path, paint);
        }
    }

    private static void Mountains(SKCanvas canvas, int width, int height, SKColor color, SKColor light, Random random)
    {
        Stars(canvas, width, height / 2, random, 120);
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = Shade(light, 1.8f).WithAlpha(220);
        canvas.DrawCircle(width * (0.2f + (float)random.NextDouble() * 0.6f), height * 0.27f, height * 0.09f, paint);
        for (var layer = 0; layer < 4; layer++)
        {
            paint.Color = Mix(Shade(color, 0.9f - layer * 0.18f), new SKColor(8, 12, 20), layer * 0.22f);
            using var path = new SKPath();
            var baseline = height * (0.55f + layer * 0.12f);
            path.MoveTo(-10, height);
            var x = -10f;
            path.LineTo(x, baseline);
            while (x < width + 80)
            {
                var step = 60 + random.Next(120);
                path.LineTo(x + step * 0.5f, baseline - height * (0.12f + (float)random.NextDouble() * (0.3f - layer * 0.05f)));
                x += step;
                path.LineTo(x, baseline);
            }

            path.LineTo(width + 10, height);
            path.Close();
            canvas.DrawPath(path, paint);
        }

        Fog(canvas, width, height);
    }

    private static void Stars(SKCanvas canvas, int width, int height, Random random, int count)
    {
        using var paint = new SKPaint { IsAntialias = true };
        for (var star = 0; star < count; star++)
        {
            paint.Color = SKColors.White.WithAlpha((byte)(60 + random.Next(190)));
            canvas.DrawCircle((float)random.NextDouble() * width, (float)random.NextDouble() * height, 0.6f + (float)random.NextDouble() * 1.4f, paint);
        }
    }

    private static void Fog(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint();
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, height * 0.55f), new SKPoint(0, height), [SKColors.White.WithAlpha(0), SKColors.White.WithAlpha(38)], SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, paint);
    }

    private static void Vignette(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint();
        paint.Shader = SKShader.CreateRadialGradient(new SKPoint(width / 2f, height / 2f), width * 0.75f, [SKColors.Black.WithAlpha(0), SKColors.Black.WithAlpha(150)], [0.55f, 1], SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, paint);
    }

    private static void Title(SKCanvas canvas, int width, int height, string title)
    {
        using var typeface = SKTypeface.FromFamilyName("Inter", SKFontStyleWeight.ExtraBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, 62);
        var lines = title.Split(": ", 2);
        using var paint = new SKPaint { IsAntialias = true };
        var y = height - 58f - (lines.Length - 1) * 40;
        var text = lines[0].ToUpperInvariant();
        while (font.Size > 28 && font.MeasureText(text) > width - 88)
        {
            font.Size -= 2;
        }

        paint.ImageFilter = SKImageFilter.CreateDropShadow(0, 4, 10, 10, SKColors.Black.WithAlpha(200));
        paint.Color = SKColors.White;
        canvas.DrawText(text, 44, y, SKTextAlign.Left, font, paint);
        if (lines.Length > 1)
        {
            font.Size = 30;
            paint.Color = SKColors.White.WithAlpha(220);
            canvas.DrawText(lines[1].ToUpperInvariant(), 46, y + 44, SKTextAlign.Left, font, paint);
        }
    }

    private static SKColor Shade(SKColor color, float factor) => new(
        (byte)Math.Clamp(color.Red * factor, 0, 255), (byte)Math.Clamp(color.Green * factor, 0, 255), (byte)Math.Clamp(color.Blue * factor, 0, 255), color.Alpha);

    private static SKColor Mix(SKColor a, SKColor b, float t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t), (byte)(a.Blue + (b.Blue - a.Blue) * t));
}
