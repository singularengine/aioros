using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Models;

public sealed record CustomerRow(string Name, string Email, string Company, string Country, string Plan, string Revenue, string Since, string Href, bool Active);

public sealed record SortLink(string Label, string Href, bool Current, string Mark);

public sealed record CrmData(CustomerQuery Query, CustomerPage Result, bool Saved, string Xaml);

public sealed partial class CrmModel : ObservableObject
{
    private static readonly (string Key, string Label)[] Sorts = [("name", "Name"), ("company", "Company"), ("revenue", "Revenue"), ("since", "Customer since")];

    private readonly IAppNavigator _navigator;
    private readonly CustomerQuery _query;

    public CrmModel(CrmData data, IAppNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(data);
        _navigator = navigator;
        _query = data.Query;
        Xaml = data.Xaml;
        Search = _query.Search ?? string.Empty;
        Saved = data.Saved;
        var result = data.Result;
        Rows = [.. result.Items.Select(Row)];
        Empty = result.Total == 0;
        var first = ((result.Page - 1) * CustomerStore.PageSize) + 1;
        Summary = result.Total == 0
            ? "No customers match."
            : string.Create(CultureInfo.InvariantCulture, $"Showing {first:N0}–{first + result.Items.Count - 1:N0} of {result.Total:N0} customers");
        PageText = string.Create(CultureInfo.InvariantCulture, $"Page {result.Page:N0} of {result.Pages:N0}");
        HasPrevious = result.Page > 1;
        HasNext = result.Page < result.Pages;
        PreviousHref = Url(_query with { Page = result.Page - 1 });
        NextHref = Url(_query with { Page = result.Page + 1 });
        SortLinks = [.. Sorts.Select(entry => Link(entry.Key, entry.Label))];
    }

    public string Xaml { get; }

    public IReadOnlyList<CustomerRow> Rows { get; }

    public IReadOnlyList<SortLink> SortLinks { get; }

    public string Summary { get; }

    public string PageText { get; }

    public string PreviousHref { get; }

    public string NextHref { get; }

    public bool HasPrevious { get; }

    public bool HasNext { get; }

    public bool Saved { get; }

    public bool Empty { get; }

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [RelayCommand]
    private Task FindAsync() => _navigator.GoToAsync(Url(_query with { Search = Search, Page = 1 }));

    private SortLink Link(string key, string label)
    {
        var current = _query.Sort == key;
        var descending = current ? !_query.Descending : key is "revenue" or "since";
        return new SortLink(label, Url(_query with { Sort = key, Descending = descending, Page = 1 }), current, current ? (_query.Descending ? "↓" : "↑") : string.Empty);
    }

    private static CustomerRow Row(Customer customer) => new(
        customer.Name,
        customer.Email,
        customer.Company,
        customer.Country,
        customer.Plan,
        customer.Revenue.ToString("$#,0", CultureInfo.InvariantCulture),
        customer.Since.ToString("MMM yyyy", CultureInfo.InvariantCulture),
        "/crm/" + customer.Id.ToString(CultureInfo.InvariantCulture),
        customer.Active);

    private static string Url(CustomerQuery query)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add("q=" + Uri.EscapeDataString(query.Search.Trim()));
        }

        if (query.Sort != "name")
        {
            parts.Add("sort=" + query.Sort);
        }

        if (query.Descending)
        {
            parts.Add("dir=desc");
        }

        if (query.Page > 1)
        {
            parts.Add("page=" + query.Page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/crm" : "/crm?" + string.Join('&', parts);
    }
}

public sealed record CustomerData(Customer Customer, string Xaml);

public sealed partial class CustomerModel : ObservableValidator
{
    private readonly ICustomerFunctions _customers;
    private readonly IAppNavigator _navigator;
    private readonly Customer _customer;
    private readonly Dictionary<string, string> _refused = new(StringComparer.Ordinal);

    public CustomerModel(CustomerData data, ICustomerFunctions customers, IAppNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(data);
        _customers = customers;
        _navigator = navigator;
        _customer = data.Customer;
        Xaml = data.Xaml;
        Name = _customer.Name;
        Email = _customer.Email;
        Company = _customer.Company;
        Plan = _customer.Plan;
        Revenue = _customer.Revenue;
        Active = _customer.Active;
        Notes = _customer.Notes;
        Heading = _customer.Name;
        Initials = string.Concat(_customer.Name.Split(' ').Select(part => part[0]));
        Subtitle = $"{_customer.Company} · {_customer.Country} · customer since {_customer.Since.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}";
        var random = new Random(_customer.Id);
        History = [.. Enumerable.Range(0, 12).Select(month => new MonthlyRevenue(new DateOnly(2025, 11, 1).AddMonths(month).ToString("MMM", CultureInfo.InvariantCulture), Math.Round((double)_customer.Revenue / 12 * (0.6 + random.NextDouble() * 0.8))))];
    }

    public string Xaml { get; }

    public IReadOnlyList<string> Plans { get; } = CustomerStore.Plans;

    public string Heading { get; }

    public string Initials { get; }

    public string Subtitle { get; }

    public IReadOnlyList<MonthlyRevenue> History { get; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A name is required.")]
    [MinLength(2, ErrorMessage = "The name needs at least two characters.")]
    [CustomValidation(typeof(CustomerModel), nameof(Refused))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "An email is required.")]
    [EmailAddress(ErrorMessage = "This is not a valid email address.")]
    [CustomValidation(typeof(CustomerModel), nameof(Refused))]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "A company is required.")]
    [CustomValidation(typeof(CustomerModel), nameof(Refused))]
    public partial string Company { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Plan { get; set; } = "Free";

    [ObservableProperty]
    public partial decimal? Revenue { get; set; }

    [ObservableProperty]
    public partial bool Active { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    /// <summary>A field the server refused on the last save, shown as that field's error.</summary>
    public static ValidationResult? Refused(string value, ValidationContext context) =>
        context?.ObjectInstance is CustomerModel model && context.MemberName is { } member && model._refused.TryGetValue(member, out var message)
            ? new ValidationResult(message, [member])
            : ValidationResult.Success;

    [RelayCommand]
    private async Task SaveAsync()
    {
        _refused.Clear();
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        try
        {
            await _customers.SaveAsync(_customer with { Name = Name.Trim(), Email = Email.Trim(), Company = Company.Trim(), Plan = Plan, Revenue = Revenue ?? 0, Active = Active, Notes = Notes });
        }
        catch (ValidationException refused)
        {
            foreach (var member in refused.ValidationResult.MemberNames)
            {
                _refused[member] = refused.ValidationResult.ErrorMessage ?? refused.Message;
            }

            ValidateAllProperties();
            return;
        }

        await _navigator.GoToAsync("/crm?saved=1&q=" + Uri.EscapeDataString(Name.Trim()));
    }
}

public sealed record MonthlyRevenue(string Month, double Amount);
