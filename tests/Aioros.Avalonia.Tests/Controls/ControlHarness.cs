using System.Collections.Immutable;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Runtime.Rendering;

namespace Aioros.Avalonia.Tests.Controls;

public static class ControlHarness
{
    public static string Xaml(string body, string? dataType = null) => $"""
        <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     xmlns:a="https://aioros.dev" xmlns:loader="clr-namespace:AsyncImageLoader;assembly=AsyncImageLoader.Avalonia" xmlns:l="using:Aioros.Markup.Library" xmlns:m="using:Aioros.Avalonia.Tests.Models" xmlns:s="using:Avalonia.Platform.Storage" xmlns:sys="clr-namespace:System;assembly=System.Runtime"{(dataType is null ? string.Empty : $" x:DataType=\"m:{dataType}\"")}>
          {body}
        </UserControl>
        """;

    public static async Task<CompiledPage> CompileAsync(this EngineFixture fixture, string body, object? model = null, string theme = "light") =>
        await fixture.Engine.RenderXamlAsync(Xaml(body, model?.GetType().Name), [model], theme, TestContext.Current.CancellationToken);

    /// <summary>Compiles the page for the first model and checks that its plan, and the plan read back from a plans file, write what Avalonia renders for every model.</summary>
    public static async Task<CompiledPage> PlansLikeAvaloniaAsync(this EngineFixture fixture, string body, params object[] models)
    {
        var planned = await fixture.CompileAsync(body, models[0]);
        Assert.True(planned.Reusable, planned.Failure);
        var saved = PlansFile.Read(PlansFile.Write([new PlannedPage("/", "light", planned)]).Json, new Plans.AdapterSlotResolver()).Single().Page;
        foreach (var model in models)
        {
            var expected = EngineFixture.Body(await fixture.CompileAsync(body, model), model);
            Assert.Equal(expected, EngineFixture.Body(planned, model));
            Assert.Equal(expected, EngineFixture.Body(saved, model));
        }

        return planned;
    }

    public static async Task<string> HtmlAsync(this EngineFixture fixture, string body, object? model = null, string theme = "light") =>
        EngineFixture.Body(await fixture.CompileAsync(body, model, theme), model);

    public static async Task<byte[]> CaptureAsync(this EngineFixture fixture, string body, object? model = null, int width = 800, int height = 600, string theme = "light") =>
        (await fixture.CaptureInAsync(body, new CaptureOptions(theme, width, height), model)).Png;

    public static async Task<PageCapture> CaptureInAsync(this EngineFixture fixture, string body, CaptureOptions options, object? model = null) =>
        await fixture.Engine.CaptureXamlAsync(Xaml(body, model?.GetType().Name), [model], options, TestContext.Current.CancellationToken);

    public static async Task<PostOutcome> PostAsync(this EngineFixture fixture, string body, object model, params (string Key, string Value)[] fields) =>
        await fixture.Engine.PostXamlAsync(
            Xaml(body, model.GetType().Name),
            [model],
            fields.ToImmutableDictionary(field => field.Key, field => field.Value),
            [],
            "light",
            TestContext.Current.CancellationToken);

    public static async Task<PostOutcome> PostFilesAsync(this EngineFixture fixture, string body, object model, ImmutableArray<PostedFile> files, params (string Key, string Value)[] fields) =>
        await fixture.Engine.PostXamlAsync(
            Xaml(body, model.GetType().Name),
            [model],
            fields.ToImmutableDictionary(field => field.Key, field => field.Value),
            files,
            "light",
            TestContext.Current.CancellationToken);

    public static async Task<PostedFile> FileAsync(string field, string name, string content)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("aioros-test-").FullName, name);
        await System.IO.File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return new(field, name, "text/plain", new FileInfo(path).Length, path);
    }
}
