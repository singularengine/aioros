using System.Collections;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Aioros.Avalonia.Tests.Models;

[TemplatePart("PART_Input", typeof(TextBox))]
public sealed class Finder : ItemsControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Finder, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SelectedValueProperty = AvaloniaProperty.Register<Finder, string?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderProperty = AvaloniaProperty.Register<Finder, string?>(nameof(Placeholder), "Type a command or search...");

    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Finder, object?>(nameof(Empty), "No results found.");

    public static readonly StyledProperty<bool> ShouldFilterProperty = AvaloniaProperty.Register<Finder, bool>(nameof(ShouldFilter), true);

    public static readonly StyledProperty<bool> IsLoopProperty = AvaloniaProperty.Register<Finder, bool>(nameof(IsLoop));

    public static readonly DirectProperty<Finder, bool> IsEmptyProperty = AvaloniaProperty.RegisterDirect<Finder, bool>(nameof(IsEmpty), command => command.IsEmpty);

    internal static readonly AttachedProperty<double> RankProperty = AvaloniaProperty.RegisterAttached<Finder, Control, double>("Rank");

    private bool _empty;
    private bool _refiltering;

    static Finder()
    {
        TextProperty.Changed.AddClassHandler<Finder>((command, _) => command.Refilter());
        ShouldFilterProperty.Changed.AddClassHandler<Finder>((command, _) => command.Refilter());
        SelectedValueProperty.Changed.AddClassHandler<Finder>((command, change) => command.Follow(change.GetNewValue<string?>()));
    }

    public Finder() => AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public object? Empty
    {
        get => GetValue(EmptyProperty);
        set => SetValue(EmptyProperty, value);
    }

    public bool ShouldFilter
    {
        get => GetValue(ShouldFilterProperty);
        set => SetValue(ShouldFilterProperty, value);
    }

    public bool IsLoop
    {
        get => GetValue(IsLoopProperty);
        set => SetValue(IsLoopProperty, value);
    }

    public bool IsEmpty
    {
        get => _empty;
        private set => SetAndRaise(IsEmptyProperty, ref _empty, value);
    }

    public Func<string, string, IReadOnlyList<string>?, double> Filter { get; set; } = FinderScore.Score;

    public FinderItem? SelectedItem => Visible().FirstOrDefault(item => item.IsSelected);

    public IReadOnlyList<FinderItem> Visible() => [.. Ordered(this).SelectMany(entry => entry switch
    {
        FinderGroup group when group.IsVisible => Ordered(group).OfType<FinderItem>().Where(item => item.IsVisible),
        FinderItem item when item.IsVisible => [item],
        _ => [],
    })];

    public void Select(FinderItem? item)
    {
        foreach (var each in All())
        {
            each.Select(each == item);
        }

        SetCurrentValue(SelectedValueProperty, item?.EffectiveValue);
    }

    internal void ItemAdded()
    {
        if (!_refiltering)
        {
            Refilter();
        }
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new FinderItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        item is FinderGroup or FinderSeparator ? NeedsContainer<Control>(item, out recycleKey) : NeedsContainer<FinderItem>(item, out recycleKey);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Refilter();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Refilter();
    }

    private static IEnumerable<Control> Ordered(ItemsControl owner) =>
        owner.GetRealizedContainers().Select((control, index) => (control, index)).OrderByDescending(pair => pair.control.GetValue(RankProperty)).ThenBy(pair => pair.index).Select(pair => pair.control);

    private IEnumerable<FinderItem> All() => GetRealizedContainers().SelectMany(entry => entry switch
    {
        FinderGroup group => group.GetRealizedContainers().OfType<FinderItem>(),
        FinderItem item => [item],
        _ => [],
    });

    private void Refilter()
    {
        _refiltering = true;
        var search = Text ?? string.Empty;
        var filtering = ShouldFilter && search.Length > 0;
        foreach (var item in All())
        {
            item.Score = filtering ? Filter(item.EffectiveValue, search, item.Keywords) : 1;
            item.IsVisible = item.Score > 0;
            item.SetValue(RankProperty, filtering ? item.Score : 0);
        }

        foreach (var entry in GetRealizedContainers())
        {
            Hide(entry, filtering);
        }

        var first = true;
        foreach (var group in Ordered(this).OfType<FinderGroup>().Where(group => group.IsVisible))
        {
            group.Follow(!first);
            first = false;
        }

        IsEmpty = !All().Any(item => item.IsVisible);
        Select(Visible().FirstOrDefault(item => item.IsEffectivelyEnabled));
        _refiltering = false;
    }

    private static void Hide(Control entry, bool filtering)
    {
        if (entry is FinderGroup group)
        {
            var scores = group.GetRealizedContainers().OfType<FinderItem>().Where(item => item.IsVisible).Select(item => item.Score).ToList();
            group.IsVisible = !filtering || scores.Count > 0;
            group.SetValue(RankProperty, filtering && scores.Count > 0 ? scores.Max() : 0);
        }
        else if (entry is FinderSeparator separator)
        {
            separator.IsVisible = !filtering;
        }
    }

    private void Follow(string? value)
    {
        if (_refiltering || SelectedItem?.EffectiveValue == value)
        {
            return;
        }

        var item = Visible().FirstOrDefault(each => each.EffectiveValue == value);
        foreach (var each in All())
        {
            each.Select(each == item);
        }
    }

    private void Move(int step)
    {
        var items = Visible().Where(item => item.IsEffectivelyEnabled).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var index = items.FindIndex(item => item.IsSelected) + step;
        if (IsLoop)
        {
            index = (index + items.Count) % items.Count;
        }

        if (index >= 0 && index < items.Count)
        {
            Select(items[index]);
        }
    }

    private void Edge(bool last)
    {
        var items = Visible().Where(item => item.IsEffectivelyEnabled).ToList();
        Select(last ? items.LastOrDefault() : items.FirstOrDefault());
    }

    private void Jump(int step)
    {
        var groups = Ordered(this).OfType<FinderGroup>().Where(group => group.IsVisible).ToList();
        var current = groups.FindIndex(group => group.GetRealizedContainers().OfType<FinderItem>().Any(item => item.IsSelected));
        for (var index = current + step; index >= 0 && index < groups.Count; index += step)
        {
            if (Ordered(groups[index]).OfType<FinderItem>().FirstOrDefault(item => item.IsVisible && item.IsEffectivelyEnabled) is { } first)
            {
                Select(first);
                return;
            }
        }

        Move(step);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var meta = (e.KeyModifiers & KeyModifiers.Meta) != 0;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        switch (e.Key)
        {
            case Key.Down or Key.N or Key.J when e.Key == Key.Down || control:
                Step(1, meta, alt);
                break;
            case Key.Up or Key.P or Key.K when e.Key == Key.Up || control:
                Step(-1, meta, alt);
                break;
            case Key.Home:
                Edge(false);
                break;
            case Key.End:
                Edge(true);
                break;
            case Key.Enter:
                SelectedItem?.Choose();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Step(int step, bool meta, bool alt)
    {
        if (meta)
        {
            Edge(step > 0);
        }
        else if (alt)
        {
            Jump(step);
        }
        else
        {
            Move(step);
        }
    }
}

public sealed class FinderPanel : StackPanel
{
    static FinderPanel() => AffectsParentMeasure<FinderPanel>(Finder.RankProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!Ranked())
        {
            return base.MeasureOverride(availableSize);
        }

        var width = 0d;
        var height = 0d;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!Ranked())
        {
            return base.ArrangeOverride(finalSize);
        }

        var top = 0d;
        foreach (var child in Children.Select((control, index) => (control, index)).OrderByDescending(pair => pair.control.GetValue(Finder.RankProperty)).ThenBy(pair => pair.index).Select(pair => pair.control))
        {
            child.Arrange(new Rect(0, top, finalSize.Width, child.DesiredSize.Height));
            top += child.DesiredSize.Height;
        }

        return finalSize;
    }

    private bool Ranked() => Children.Any(child => child.GetValue(Finder.RankProperty) != 0);
}

public sealed class FinderItem : Button
{
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<FinderItem, string?>(nameof(Value));

    public static readonly StyledProperty<IReadOnlyList<string>?> KeywordsProperty = AvaloniaProperty.Register<FinderItem, IReadOnlyList<string>?>(nameof(Keywords));

    public static readonly StyledProperty<string?> ShortcutProperty = AvaloniaProperty.Register<FinderItem, string?>(nameof(Shortcut));

    public static readonly StyledProperty<bool?> IsCheckedProperty = AvaloniaProperty.Register<FinderItem, bool?>(nameof(IsChecked));

    static FinderItem()
    {
        FocusableProperty.OverrideDefaultValue<FinderItem>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<FinderItem>(AutomationControlType.ListItem);
        IsCheckedProperty.Changed.AddClassHandler<FinderItem>((item, change) =>
        {
            item.PseudoClasses.Set(":checkable", change.GetNewValue<bool?>() is not null);
            item.PseudoClasses.Set(":checked", change.GetNewValue<bool?>() == true);
        });
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IReadOnlyList<string>? Keywords
    {
        get => GetValue(KeywordsProperty);
        set => SetValue(KeywordsProperty, value);
    }

    public string? Shortcut
    {
        get => GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public bool IsSelected { get; private set; }

    public string EffectiveValue => (Value ?? TextOf(this) + Shortcut).Trim();

    internal double Score { get; set; } = 1;

    protected override Type StyleKeyOverride => typeof(FinderItem);

    internal void Select(bool selected)
    {
        IsSelected = selected;
        PseudoClasses.Set(":selected", selected);
        if (selected)
        {
            this.BringIntoView();
        }
    }

    internal void Choose()
    {
        if (IsEffectivelyEnabled)
        {
            OnClick();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsEffectivelyEnabled && !IsSelected)
        {
            this.FindLogicalAncestorOfType<Finder>()?.Select(this);
        }
    }

    protected override void OnClick()
    {
        this.FindLogicalAncestorOfType<Finder>()?.Select(this);
        base.OnClick();
    }

    private static string TextOf(Control item)
    {
        if (item is ContentControl { Content: string text })
        {
            return text;
        }

        var visual = item.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Name != "PART_Shortcut").Select(block => block.Text);
        var logical = item.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text);
        var texts = visual.Any() ? visual : logical;
        return string.Concat(texts);
    }
}

public sealed class FinderGroup : HeaderedItemsControl
{
    protected override Type StyleKeyOverride => typeof(FinderGroup);

    internal void Follow(bool following) => PseudoClasses.Set(":following", following);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new FinderItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<FinderItem>(item, out recycleKey);

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        this.FindLogicalAncestorOfType<Finder>()?.ItemAdded();
    }
}

public sealed class FinderSeparator : Separator
{
    protected override Type StyleKeyOverride => typeof(FinderSeparator);
}

[TemplatePart("PART_Trigger", typeof(Button))]
public sealed class Picker : SelectingItemsControl
{
    public static readonly new DirectProperty<SelectingItemsControl, IList?> SelectedItemsProperty = SelectingItemsControl.SelectedItemsProperty;

    public static readonly new StyledProperty<SelectionMode> SelectionModeProperty = SelectingItemsControl.SelectionModeProperty;

    public static readonly StyledProperty<string?> PlaceholderTextProperty = AvaloniaProperty.Register<Picker, string?>(nameof(PlaceholderText), "Select...");

    public static readonly StyledProperty<string?> SearchPlaceholderProperty = AvaloniaProperty.Register<Picker, string?>(nameof(SearchPlaceholder), "Search...");

    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Picker, object?>(nameof(Empty), "No results found.");

    public static readonly StyledProperty<bool> IsDropDownOpenProperty = AvaloniaProperty.Register<Picker, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<Picker, IDataTemplate?> SelectionTemplateProperty =
        AvaloniaProperty.RegisterDirect<Picker, IDataTemplate?>(nameof(SelectionTemplate), combobox => combobox.SelectionTemplate);

    public static readonly DirectProperty<Picker, bool> IsMultipleProperty = AvaloniaProperty.RegisterDirect<Picker, bool>(nameof(IsMultiple), combobox => combobox.IsMultiple);

    private readonly global::Avalonia.Collections.AvaloniaList<object?> _items = [];
    private readonly Finder _command;
    private readonly FinderGroup _group;
    private readonly Flyout _flyout;
    private Button? _trigger;
    private IDataTemplate? _selectionTemplate;
    private bool _multiple;

    static Picker()
    {
        SelectionModeProperty.OverrideDefaultValue<Picker>(SelectionMode.Single | SelectionMode.Toggle);
        IsDropDownOpenProperty.Changed.AddClassHandler<Picker>((combobox, change) => combobox.Open(change.GetNewValue<bool>()));
        SelectionModeProperty.Changed.AddClassHandler<Picker>((combobox, _) =>
        {
            combobox.IsMultiple = (combobox.SelectionMode & SelectionMode.Multiple) != 0;
            Aioros.Markup.Web.SetChoice(combobox, combobox.IsMultiple ? null : nameof(SelectedIndex));
        });
        ItemsSourceProperty.Changed.AddClassHandler<Picker>((combobox, _) => combobox.Mirror());
        ItemTemplateProperty.Changed.AddClassHandler<Picker>((combobox, _) => combobox.Templates());
        DisplayMemberBindingProperty.Changed.AddClassHandler<Picker>((combobox, _) => combobox.Templates());
        SearchPlaceholderProperty.Changed.AddClassHandler<Picker>((combobox, change) => combobox._command.Placeholder = change.GetNewValue<string?>());
        EmptyProperty.Changed.AddClassHandler<Picker>((combobox, change) => combobox._command.Empty = change.GetNewValue<object?>());
    }

    public Picker()
    {
        _group = new FinderGroup { ItemsSource = _items };
        Items.CollectionChanged += (_, _) => Mirror();
        _group.ContainerPrepared += (_, e) =>
        {
            Aioros.Markup.Web.SetChooses(e.Container, e.Index);
            Check(e.Container);
        };
        _command = new Finder { Placeholder = SearchPlaceholder, Empty = Empty, Items = { _group }, Classes = { "combobox" } };
        _command.AddHandler(Button.ClickEvent, OnChosen);
        _flyout = new Flyout { Content = _command, Placement = PlacementMode.BottomEdgeAlignedLeft, FlyoutPresenterClasses = { "combobox" } };
        _flyout.Opened += (_, _) => Opened();
        _flyout.Closed += (_, _) => SetCurrentValue(IsDropDownOpenProperty, false);
        SelectionChanged += (_, _) => Refresh();
        Aioros.Markup.Web.SetChoice(this, nameof(SelectedIndex));
        Templates();
    }

    public new IList? SelectedItems
    {
        get => base.SelectedItems;
        set => base.SelectedItems = value;
    }

    public new SelectionMode SelectionMode
    {
        get => base.SelectionMode;
        set => base.SelectionMode = value;
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string? SearchPlaceholder
    {
        get => GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public object? Empty
    {
        get => GetValue(EmptyProperty);
        set => SetValue(EmptyProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public IDataTemplate? SelectionTemplate
    {
        get => _selectionTemplate;
        private set => SetAndRaise(SelectionTemplateProperty, ref _selectionTemplate, value);
    }

    public bool IsMultiple
    {
        get => _multiple;
        private set => SetAndRaise(IsMultipleProperty, ref _multiple, value);
    }

    public Finder List => _command;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Flyout = null;
        }

        _trigger = e.NameScope.Find<Button>("PART_Trigger");
        if (_trigger is not null)
        {
            _trigger.Flyout = _flyout;
        }
    }

    private void Mirror()
    {
        _items.Clear();
        _items.AddRange(Items);
    }

    private void Templates()
    {
        var template = ItemTemplate ?? (DisplayMemberBinding is { } binding ? new FuncDataTemplate<object?>((_, _) => new TextBlock { [!TextBlock.TextProperty] = binding }) : null);
        _group.ItemTemplate = template;
        SelectionTemplate = template;
    }

    private void Open(bool open)
    {
        if (open && !_flyout.IsOpen && _trigger is not null)
        {
            _flyout.ShowAt(_trigger);
        }
        else if (!open && _flyout.IsOpen)
        {
            _flyout.Hide();
        }
    }

    private void Opened()
    {
        SetCurrentValue(IsDropDownOpenProperty, true);
        _command.Text = string.Empty;
        if (_trigger is not null)
        {
            _command.Width = Math.Max(0, _trigger.Bounds.Width - 2);
        }

        Refresh();
    }

    private void OnChosen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not FinderItem item || item.FindLogicalAncestorOfType<Picker>() is { } nested && nested != this)
        {
            return;
        }

        var data = _group.ItemFromContainer(item) ?? item;
        if (IsMultiple)
        {
            Toggle(SelectedItems, data);
            return;
        }

        SelectedItem = Equals(SelectedItem, data) ? null : data;
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private static void Toggle(IList? selected, object data)
    {
        if (selected is null)
        {
            return;
        }

        if (selected.Contains(data))
        {
            selected.Remove(data);
        }
        else
        {
            selected.Add(data);
        }
    }

    private void Refresh()
    {
        PseudoClasses.Set(":multiple", IsMultiple);
        PseudoClasses.Set(":chosen", IsMultiple ? SelectedItems?.Count > 0 : SelectedItem is not null);
        foreach (var container in _group.GetRealizedContainers())
        {
            Check(container);
        }
    }

    private void Check(Control container)
    {
        if (container is FinderItem item)
        {
            var data = _group.ItemFromContainer(item) ?? item;
            item.IsChecked = IsMultiple ? SelectedItems?.Contains(data) == true : Equals(SelectedItem, data);
        }
    }
}

public static class FinderScore
{
    private const double ContinueMatch = 1;
    private const double SpaceWordJump = 0.9;
    private const double NonSpaceWordJump = 0.8;
    private const double CharacterJump = 0.17;
    private const double Transposition = 0.1;
    private const double PenaltySkipped = 0.999;
    private const double PenaltyCaseMismatch = 0.9999;
    private const double PenaltyNotComplete = 0.99;
    private const string Gaps = "\\/_+.#\"@[({&";

    public static double Score(string value, string abbreviation, IReadOnlyList<string>? keywords = null)
    {
        var text = keywords is { Count: > 0 } ? value + " " + string.Join(' ', keywords) : value;
        return Match(new Inputs(text, abbreviation, Format(text), Format(abbreviation)), 0, 0, []);
    }

    private static string Format(string input) => string.Concat(input.ToLowerInvariant().Select(character => IsSpace(character) ? ' ' : character));

    private static bool IsSpace(char character) => char.IsWhiteSpace(character) || character == '-';

    private static bool IsGap(char character) => Gaps.Contains(character, StringComparison.Ordinal);

    private static char? At(string text, int index) => index >= 0 && index < text.Length ? text[index] : null;

    private static double Match(Inputs inputs, int stringIndex, int abbreviationIndex, Dictionary<(int, int), double> memo)
    {
        if (abbreviationIndex == inputs.Abbreviation.Length)
        {
            return stringIndex == inputs.Text.Length ? ContinueMatch : PenaltyNotComplete;
        }

        if (memo.TryGetValue((stringIndex, abbreviationIndex), out var known))
        {
            return known;
        }

        var character = inputs.LowerAbbreviation[abbreviationIndex];
        var index = inputs.LowerText.IndexOf(character, stringIndex);
        var high = 0d;
        while (index >= 0)
        {
            var score = Match(inputs, index + 1, abbreviationIndex + 1, memo);
            if (score > high)
            {
                score *= Jump(inputs.Text, stringIndex, index);
                if (inputs.Text[index] != inputs.Abbreviation[abbreviationIndex])
                {
                    score *= PenaltyCaseMismatch;
                }
            }

            if (Transposes(inputs, index, abbreviationIndex, score))
            {
                var transposed = Match(inputs, index + 1, abbreviationIndex + 2, memo);
                score = Math.Max(score, transposed * Transposition);
            }

            high = Math.Max(high, score);
            index = inputs.LowerText.IndexOf(character, index + 1);
        }

        memo[(stringIndex, abbreviationIndex)] = high;
        return high;
    }

    private static double Jump(string text, int stringIndex, int index)
    {
        if (index == stringIndex)
        {
            return ContinueMatch;
        }

        var before = text[index - 1];
        var skipped = text[stringIndex..Math.Max(stringIndex, index - 1)];
        if (IsGap(before))
        {
            var breaks = skipped.Count(IsGap);
            return NonSpaceWordJump * (breaks > 0 && stringIndex > 0 ? Math.Pow(PenaltySkipped, breaks) : 1);
        }

        if (IsSpace(before))
        {
            var breaks = skipped.Count(IsSpace);
            return SpaceWordJump * (breaks > 0 && stringIndex > 0 ? Math.Pow(PenaltySkipped, breaks) : 1);
        }

        return CharacterJump * (stringIndex > 0 ? Math.Pow(PenaltySkipped, index - stringIndex) : 1);
    }

    private static bool Transposes(Inputs inputs, int index, int abbreviationIndex, double score)
    {
        var next = At(inputs.LowerAbbreviation, abbreviationIndex + 1);
        var previous = At(inputs.LowerText, index - 1);
        return (score < Transposition && previous == next) || (next == inputs.LowerAbbreviation[abbreviationIndex] && previous != inputs.LowerAbbreviation[abbreviationIndex]);
    }

    private sealed record Inputs(string Text, string Abbreviation, string LowerText, string LowerAbbreviation);
}

/// <summary>A library's own list, whose selection the library may act on.</summary>
public sealed class LibraryList : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);
}
