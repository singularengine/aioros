using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Aioros.Avalonia.Tests.Models;

public interface IDataTableRow : INotifyPropertyChanged
{
    bool IsSelected { get; set; }
}

public static class DataTable
{
    public static readonly AttachedProperty<bool> SelectsRowsProperty = AvaloniaProperty.RegisterAttached<TableView, bool>("SelectsRows", typeof(DataTable));

    private static readonly AttachedProperty<Mirror?> MirrorProperty = AvaloniaProperty.RegisterAttached<Control, Mirror?>("Mirror", typeof(DataTable));

    static DataTable() => SelectsRowsProperty.Changed.AddClassHandler<TableView>((table, change) =>
    {
        if (change.GetNewValue<bool>())
        {
            table.ContainerPrepared += OnPrepared;
            table.ContainerClearing += OnClearing;
        }
        else
        {
            table.ContainerPrepared -= OnPrepared;
            table.ContainerClearing -= OnClearing;
        }
    });

    public static bool GetSelectsRows(TableView table) => table.GetValue(SelectsRowsProperty);

    public static void SetSelectsRows(TableView table, bool value) => table.SetValue(SelectsRowsProperty, value);

    private static void OnPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is IDataTableRow row)
        {
            var mirror = new Mirror(e.Container, row);
            e.Container.SetValue(MirrorProperty, mirror);
            row.PropertyChanged += mirror.Changed;
            mirror.Apply();
        }
    }

    private static void OnClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container.GetValue(MirrorProperty) is { } mirror)
        {
            mirror.Row.PropertyChanged -= mirror.Changed;
            e.Container.ClearValue(MirrorProperty);
            e.Container.Classes.Remove("selected");
        }
    }

    private sealed class Mirror(Control container, IDataTableRow row)
    {
        public IDataTableRow Row => row;

        public void Apply() => container.Classes.Set("selected", row.IsSelected);

        public void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IDataTableRow.IsSelected))
            {
                Apply();
            }
        }
    }
}

public sealed class DataTableColumn(string key, string header) : INotifyPropertyChanged
{
    private bool _visible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; } = key;

    public string Header { get; } = header;

    public bool IsVisible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }
    }
}

public sealed class DataTableSource<T> : INotifyPropertyChanged where T : class, IDataTableRow
{
    private readonly Dictionary<string, Func<T, string>> _values = new(StringComparer.Ordinal);
    private readonly List<DataTableColumn> _columns = [];
    private List<T> _filtered = [];
    private string? _filter;
    private string? _sortColumn;
    private bool _descending;
    private int _pageIndex;
    private bool _paging;

    public DataTableSource(IEnumerable<T> items, int pageSize)
    {
        PageSize = pageSize;
        Items = [.. items];
        foreach (var item in Items)
        {
            item.PropertyChanged += OnRowChanged;
        }

        SortCommand = new Act(parameter => Sort(parameter as string));
        PreviousPageCommand = new Act(_ => Page(_pageIndex - 1), () => _pageIndex > 0);
        NextPageCommand = new Act(_ => Page(_pageIndex + 1), () => _pageIndex < PageCount - 1);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<T> Items { get; }

    public ObservableCollection<T> Rows { get; } = [];

    public IReadOnlyList<DataTableColumn> Columns => _columns;

    public ICommand SortCommand { get; }

    public ICommand PreviousPageCommand { get; }

    public ICommand NextPageCommand { get; }

    public string? FilterColumn { get; init; }

    public int PageSize { get; }

    public string? FilterText
    {
        get => _filter;
        set
        {
            if (_filter != value)
            {
                _filter = value;
                _pageIndex = 0;
                OnPropertyChanged();
                Refresh();
            }
        }
    }

    public int PageCount => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));

    public int FilteredCount => _filtered.Count;

    public int SelectedCount => _filtered.Count(row => row.IsSelected);

    public bool? AllPageRowsSelected
    {
        get => Rows.Count > 0 && Rows.All(row => row.IsSelected) ? true : Rows.Any(row => row.IsSelected) ? null : false;
        set
        {
            var select = AllPageRowsSelected is null || value == true;
            _paging = true;
            foreach (var row in Rows)
            {
                row.IsSelected = select;
            }

            _paging = false;
            Selection();
        }
    }

    public DataTableSource<T> Column(string key, string header, Func<T, string>? value = null)
    {
        if (value is not null)
        {
            _values[key] = value;
        }

        _columns.Add(new DataTableColumn(key, header));
        Refresh();
        return this;
    }

    private void Sort(string? key)
    {
        if (key is null || !_values.ContainsKey(key))
        {
            return;
        }

        _descending = _sortColumn == key && !_descending;
        _sortColumn = key;
        Refresh();
    }

    private void Page(int index)
    {
        _pageIndex = Math.Clamp(index, 0, PageCount - 1);
        Refresh();
    }

    private void Refresh()
    {
        IEnumerable<T> rows = Items;
        if (!string.IsNullOrEmpty(_filter) && FilterColumn is { } filtered && _values.TryGetValue(filtered, out var filterValue))
        {
            rows = rows.Where(row => filterValue(row).Contains(_filter, StringComparison.OrdinalIgnoreCase));
        }

        if (_sortColumn is { } sorted && _values.TryGetValue(sorted, out var sortValue))
        {
            rows = _descending ? rows.OrderByDescending(sortValue, StringComparer.OrdinalIgnoreCase) : rows.OrderBy(sortValue, StringComparer.OrdinalIgnoreCase);
        }

        _filtered = [.. rows];
        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        Rows.Clear();
        foreach (var row in _filtered.Skip(_pageIndex * PageSize).Take(PageSize))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(FilteredCount));
        ((Act)PreviousPageCommand).Changed();
        ((Act)NextPageCommand).Changed();
        Selection();
    }

    private void Selection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(AllPageRowsSelected));
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDataTableRow.IsSelected) && !_paging)
        {
            Selection();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class Act(Action<object?> run, Func<bool>? can = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => can?.Invoke() ?? true;

        public void Execute(object? parameter) => run(parameter);

        public void Changed() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class Payment : IDataTableRow
{
    private bool _selected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public int Changes { get; private set; }

    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected != value)
            {
                _selected = value;
                Changes++;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
}

public sealed class PaymentTable
{
    public static readonly IValueConverter Dollars = new FuncValueConverter<decimal, string>(amount => "$" + amount.ToString("#,0.00", CultureInfo.InvariantCulture));

    public PaymentTable()
        : this(Examples)
    {
    }

    public PaymentTable(IEnumerable<Payment> payments) =>
        Source = new DataTableSource<Payment>(payments, 10) { FilterColumn = "email" }
            .Column("select", string.Empty)
            .Column("status", "Status", payment => payment.Status)
            .Column("email", "Email", payment => payment.Email)
            .Column("amount", "Amount", payment => payment.Amount.ToString(CultureInfo.InvariantCulture));

    public static IEnumerable<Payment> Examples =>
    [
        new() { Id = "m5gr84i9", Amount = 316, Status = "success", Email = "ken99@example.com" },
        new() { Id = "3u1reuv4", Amount = 242, Status = "success", Email = "Abe45@example.com" },
        new() { Id = "derv1ws0", Amount = 837, Status = "processing", Email = "Monserrat44@example.com" },
        new() { Id = "5kma53ae", Amount = 874, Status = "success", Email = "Silas22@example.com" },
        new() { Id = "bhqecj4p", Amount = 721, Status = "failed", Email = "carmella@example.com" },
    ];

    public DataTableSource<Payment> Source { get; }

    public DataTableColumn StatusColumn => Source.Columns[1];

    public DataTableColumn EmailColumn => Source.Columns[2];

    public DataTableColumn AmountColumn => Source.Columns[3];
}
