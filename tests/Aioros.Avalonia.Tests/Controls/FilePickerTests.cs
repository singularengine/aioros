using Aioros.Runtime.Rendering;
using Aioros.Markup;
using System.Text.RegularExpressions;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Avalonia.Tests.Models;
using Avalonia.Data;
using Avalonia.Platform.Storage;

namespace Aioros.Avalonia.Tests.Controls;

public sealed class FilePickerTests(EngineFixture fixture)
{
    private static readonly string[] Names = ["photo.png", "a.pdf", "b.pdf"];

    private const string Reader = """
        <StackPanel>
          <a:FilePicker File="{Binding Photo}" Content="Photo" />
          <Button Content="Read" Command="{Binding ReadCommand}" />
        </StackPanel>
        """;

    [Fact]
    [Trait("AC", "BUTTONS-63")]
    public async Task FilePickersAreFileInputsInALabelAsync()
    {
        const string Body = """
            <StackPanel>
              <a:FilePicker File="{Binding Photo}" Content="Choose a photo">
                <a:FilePicker.FileTypes>
                  <x:Static Member="s:FilePickerFileTypes.ImageAll" />
                  <x:Static Member="m:Upload.Pdf" />
                </a:FilePicker.FileTypes>
              </a:FilePicker>
              <a:FilePicker File="{Binding Photo}" Content="Off" IsEnabled="False" />
            </StackPanel>
            """;

        var page = await fixture.CompileAsync(Body, new Upload());
        var html = EngineFixture.Body(page, new Upload());
        var accept = Regex.Match(html, "accept=\"([^\"]*)\"").Groups[1].Value;

        Assert.True(page.Reusable, page.Failure);
        Assert.True(page.Uploads);
        Assert.Matches("<label class=\"[^\"]+\">Choose a photo<input type=\"file\" name=\"f0\" class=\"vh\" accept=\"[^\"]+\"></label>", html);
        Assert.Matches("<label class=\"[^\"]+\">Off<input type=\"file\" name=\"f1\" class=\"vh\" disabled></label>", html);
        Assert.DoesNotContain("multiple", html, StringComparison.Ordinal);
        Assert.Contains(".png", accept.Split(','));
        Assert.Contains(".pdf", accept.Split(','));
        Assert.Contains("image/*", accept.Split(','));
        Assert.Single(accept.Split(','), type => type == "application/pdf");
        Assert.DoesNotContain("*.", accept, StringComparison.Ordinal);
        Assert.False((await fixture.CompileAsync("""<TextBox Text="{Binding Name}" />""", new Profile())).Uploads);
    }

    [Fact]
    [Trait("AC", "BUTTONS-64")]
    public async Task FilePickersOfAListTakeSeveralFilesAsync()
    {
        const string Body = """
            <a:FilePicker Files="{Binding Documents}" Content="Documents">
              <a:FilePicker.FileTypes>
                <x:Static Member="m:Upload.Pdf" />
                <x:Static Member="m:Upload.Everything" />
              </a:FilePicker.FileTypes>
            </a:FilePicker>
            """;

        var html = await fixture.HtmlAsync(Body, new Upload());

        Assert.Matches("<input type=\"file\" name=\"f0\" class=\"vh\" multiple>", html);
        Assert.DoesNotContain("accept=", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "BUTTONS-65")]
    public async Task APostedFileIsAStorageFileAsync()
    {
        var model = new Upload();
        await fixture.PostFilesAsync(Reader, model, [await ControlHarness.FileAsync("f0", "notes.txt", "hello")], ("_btn", "0|"));
        var uploaded = model.Photo!;
        var size = (await uploaded.GetBasicPropertiesAsync()).Size;
        await fixture.PostFilesAsync(Reader, model, [], ("_btn", "0|"));

        Assert.Equal("hello", model.Read);
        Assert.Equal("notes.txt", uploaded.Name);
        Assert.Equal(5UL, size);
        Assert.Same(uploaded, model.Photo);
    }

    [Fact]
    [Trait("AC", "BUTTONS-66")]
    public async Task SeveralPostedFilesAreAListAsync()
    {
        var model = new Upload();
        await fixture.PostFilesAsync(
            """<a:FilePicker Files="{Binding Documents}" Content="Documents" />""",
            model,
            [await ControlHarness.FileAsync("f0", "a.txt", "first"), await ControlHarness.FileAsync("f0", "b.txt", "second")]);

        Assert.Equal(["a.txt", "b.txt"], model.Documents!.Select(file => file.Name));
        Assert.Equal(["first", "second"], await Task.WhenAll(model.Documents!.Select(ReadAsync)));
    }

    [Fact]
    [Trait("AC", "BUTTONS-67")]
    public async Task ClickingOpensTheStorageProviderAsync()
    {
        var paths = await Task.WhenAll(Names.Select(async name => (await ControlHarness.FileAsync("f0", name, name)).Path));
        var opened = new List<FilePickerOpenOptions>();
        var page = new Aioros.Pages.PageDefinition("/", "Pages/Page.axaml", _ => null, []);
        var (one, many, kept) = await fixture.Engine.LoadAsync(
            page,
            async () =>
            {
                var storage = new global::Avalonia.Controls.Window().StorageProvider;
                var files = new List<IStorageFile>();
                foreach (var path in paths)
                {
                    files.Add((await storage.TryGetFileFromPathAsync(path))!);
                }

                Func<FilePickerOpenOptions, Task<IReadOnlyList<IStorageFile>>> Choose(params IStorageFile[] chosen) => options =>
                {
                    opened.Add(options);
                    return Task.FromResult<IReadOnlyList<IStorageFile>>(chosen);
                };
                var model = new Upload();
                var picker = new FilePicker { DataContext = model, Content = "Choose a photo" };
                picker.FileTypes.Add(Upload.Pdf);
                picker.Bind(FilePicker.FileProperty, new ReflectionBinding(nameof(Upload.Photo)) { Mode = BindingMode.TwoWay });
                var list = new FilePicker { DataContext = model, Content = "Documents" };
                list.Bind(FilePicker.FilesProperty, new ReflectionBinding(nameof(Upload.Documents)) { Mode = BindingMode.TwoWay });
                await picker.PickAsync(Choose(files[0]));
                var photo = model.Photo;
                await list.PickAsync(Choose(files[1], files[2]));
                await picker.PickAsync(Choose());
                return (photo, model.Documents, model.Photo);
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(one);
        Assert.NotNull(many);
        Assert.Equal("photo.png", one.Name);
        Assert.Equal(["a.pdf", "b.pdf"], many.Select(file => file.Name));
        Assert.Same(one, kept);
        Assert.Equal("Choose a photo", opened[0].Title);
        Assert.False(opened[0].AllowMultiple);
        Assert.Equal([Upload.Pdf], opened[0].FileTypeFilter!);
        Assert.True(opened[1].AllowMultiple);
        Assert.Null(opened[1].FileTypeFilter);
    }

    [Fact]
    [Trait("AC", "BUTTONS-118")]
    public async Task AThemedFilePickerNamesItsChoiceOnTheDesktopAsync()
    {
        var paths = await Task.WhenAll(Names.Select(async name => (await ControlHarness.FileAsync("f0", name, name)).Path));
        var page = new Aioros.Pages.PageDefinition("/", "Pages/Page.axaml", _ => null, []);
        var shown = await fixture.Engine.LoadAsync(
            page,
            async () =>
            {
                var storage = new global::Avalonia.Controls.Window().StorageProvider;
                var files = new List<IStorageFile>();
                foreach (var path in paths)
                {
                    files.Add((await storage.TryGetFileFromPathAsync(path))!);
                }

                static FilePicker Themed(Upload model, global::Avalonia.AvaloniaProperty property, string path, out global::Avalonia.Controls.TextBlock name)
                {
                    var text = new global::Avalonia.Controls.TextBlock { Name = "PART_FileName", Text = "No file chosen" };
                    name = text;
                    var picker = new FilePicker
                    {
                        DataContext = model,
                        Template = new global::Avalonia.Controls.Templates.FuncControlTemplate<FilePicker>((_, scope) =>
                        {
                            scope.Register(text.Name, text);
                            return text;
                        }),
                    };
                    picker.Bind(property, new ReflectionBinding(path) { Mode = BindingMode.TwoWay });
                    picker.ApplyTemplate();
                    return picker;
                }

                Func<FilePickerOpenOptions, Task<IReadOnlyList<IStorageFile>>> Choose(params IStorageFile[] chosen) => _ => Task.FromResult<IReadOnlyList<IStorageFile>>(chosen);
                var model = new Upload();
                var picker = Themed(model, FilePicker.FileProperty, nameof(Upload.Photo), out var name);
                var list = Themed(model, FilePicker.FilesProperty, nameof(Upload.Documents), out var names);
                var before = name.Text;
                await picker.PickAsync(Choose(files[0]));
                var one = name.Text;
                await list.PickAsync(Choose(files[1], files[2]));
                var many = names.Text;
                model.Photo = null;
                return (before, one, many, cleared: name.Text);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(("No file chosen", "photo.png", "a.pdf, b.pdf", "No file chosen"), shown);
    }

    [Fact]
    [Trait("AC", "BUTTONS-119")]
    public async Task AnImplicitFilePickerThemeIsTakenByItsStyleKeyAsync()
    {
        var page = new Aioros.Pages.PageDefinition("/", "Pages/Page.axaml", _ => null, []);
        var parts = await fixture.Engine.LoadAsync(
            page,
            () =>
            {
                static global::Avalonia.Styling.ControlTheme Theme(Type type, string part) => new(type)
                {
                    Setters =
                    {
                        new global::Avalonia.Styling.Setter(global::Avalonia.Controls.Primitives.TemplatedControl.TemplateProperty, new global::Avalonia.Controls.Templates.FuncControlTemplate((_, scope) =>
                        {
                            var text = new global::Avalonia.Controls.TextBlock { Name = part, Text = part };
                            scope.Register(part, text);
                            return text;
                        })),
                    },
                };

                string? Shown(bool file)
                {
                    var picker = new FilePicker();
                    var window = new global::Avalonia.Controls.Window();
                    window.Resources[typeof(global::Avalonia.Controls.Button)] = Theme(typeof(global::Avalonia.Controls.Button), "PART_Button");
                    if (file)
                    {
                        window.Resources[typeof(FilePicker)] = Theme(typeof(FilePicker), "PART_FileName");
                    }

                    window.Content = picker;
                    window.Show();
                    picker.ApplyTemplate();
                    var name = global::Avalonia.VisualTree.VisualExtensions.GetVisualChildren(picker).OfType<global::Avalonia.Controls.TextBlock>().SingleOrDefault()?.Name;
                    window.Close();
                    return name;
                }

                return ValueTask.FromResult((Shown(true), Shown(false)));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(("PART_FileName", "PART_Button"), parts);
    }

    private static async Task<string> ReadAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    [Trait("AC", "BUTTONS-117")]
    public async Task AThemedFilePickerIsWrittenThroughItsTemplateAsync()
    {
        const string Body = """
            <StackPanel>
              <StackPanel.Resources>
                <ControlTheme x:Key="FileInput" TargetType="a:FilePicker">
                  <Setter Property="Height" Value="36" />
                  <Setter Property="Padding" Value="12,4" />
                  <Setter Property="BorderBrush" Value="#e5e5e5" />
                  <Setter Property="BorderThickness" Value="1" />
                  <Setter Property="CornerRadius" Value="6" />
                  <Setter Property="HorizontalContentAlignment" Value="Left" />
                  <Setter Property="VerticalContentAlignment" Value="Center" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Name="PART_Root" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}">
                          <TextBlock Text="Choose File" FontSize="14" FontWeight="Medium" />
                          <TextBlock Name="PART_FileName" Text="No file chosen" FontSize="14" />
                        </StackPanel>
                      </Border>
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </StackPanel.Resources>
              <a:FilePicker Theme="{StaticResource FileInput}" File="{Binding Photo}" Content="Picture" />
            </StackPanel>
            """;
        var page = await fixture.CompileAsync(Body, new Upload());
        var html = EngineFixture.Body(page, new Upload());

        Assert.Matches("<label class=\"[^\"]+\">.*Choose File.*<div data-aioros-file-name class=\"[^\"]*\">No file chosen</div>.*<input type=\"file\" name=\"f0\" class=\"vh\"></label>", html);
        var label = Regex.Match(html, "<label class=\"([^\"]+)\"").Groups[1].Value.Split(' ');
        var rule = page.Css.Split('\n').First(line => label.Any(name => line.StartsWith("." + name + "{", StringComparison.Ordinal) && line.Contains("border", StringComparison.Ordinal)));
        Assert.Contains("border-radius:6px", rule, StringComparison.Ordinal);
        Assert.Contains("padding:4px 12px 4px 12px", rule, StringComparison.Ordinal);
        static (double X, double Y, double Gap, double NameY) Placed(IEnumerable<LaidOutText> texts)
        {
            var list = texts.OrderBy(text => text.X).ToList();
            return (Math.Round(list[0].X), Math.Round(list[0].Y), list[1].X - list[0].X - list[0].Width, Math.Round(list[1].Y));
        }

        var desktop = Placed((await fixture.CaptureInAsync(Body, new CaptureOptions("light", 400, 200), new Upload())).Texts);
        var web = Placed(await BrowserLayout.TextsAsync(page, html, 400, 200));
        Assert.Equal((desktop.X, desktop.Y, desktop.NameY), (web.X, web.Y, web.NameY));
        Assert.InRange(web.Gap, desktop.Gap - 1, desktop.Gap + 1);
    }

    [Fact]
    [Trait("AC", "BUTTONS-127")]
    public async Task AThemedEmptyTextFollowsItsBindingAsync()
    {
        var path = (await ControlHarness.FileAsync("f0", "photo.png", "photo.png")).Path;
        var page = new Aioros.Pages.PageDefinition("/", "Pages/Page.axaml", _ => null, []);
        var shown = await fixture.Engine.LoadAsync(
            page,
            async () =>
            {
                var file = (await new global::Avalonia.Controls.Window().StorageProvider.TryGetFileFromPathAsync(path))!;
                var picker = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Parse<FilePicker>("""
                    <a:FilePicker xmlns="https://github.com/avaloniaui" xmlns:a="https://aioros.dev" Tag="A">
                      <a:FilePicker.Template>
                        <ControlTemplate><TextBlock Name="PART_FileName" Text="{TemplateBinding Tag}" /></ControlTemplate>
                      </a:FilePicker.Template>
                    </a:FilePicker>
                    """, typeof(FilePicker).Assembly);
                picker.ApplyTemplate();
                var name = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(picker).OfType<global::Avalonia.Controls.TextBlock>().Single();
                picker.Tag = "B";
                var changed = name.Text;
                await picker.PickAsync(_ => Task.FromResult<IReadOnlyList<IStorageFile>>([file]));
                var chosen = name.Text;
                picker.Tag = "C";
                picker.File = null;
                return (changed, chosen, cleared: name.Text);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(("B", "photo.png", "C"), shown);
    }
}
