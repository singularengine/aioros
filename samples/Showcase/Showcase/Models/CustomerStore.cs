using System.Collections.Immutable;
using System.Globalization;

namespace Showcase.Models;

public sealed record Customer(int Id, string Name, string Email, string Company, string Country, string Plan, decimal Revenue, DateOnly Since, bool Active, string Notes);

public sealed record CustomerQuery(string? Search, string Sort, bool Descending, int Page);

public sealed record CustomerPage(IReadOnlyList<Customer> Items, int Total, int Page, int Pages);

public sealed class CustomerStore
{
    public const int Count = 100_000;

    public const int PageSize = 50;

    public static readonly ImmutableArray<string> Plans = ["Free", "Starter", "Pro", "Enterprise"];

    private static readonly string[] First = ["Ada", "Alan", "Grace", "Linus", "Margaret", "Dennis", "Barbara", "Ken", "Radia", "Tim", "Frances", "John", "Katherine", "Edsger", "Hedy", "Donald", "Anita", "Bjarne", "Sophie", "Guido", "Yukihiro", "Mary", "James", "Carol", "Leslie", "Shafi", "Niklaus", "Jean", "Lynn", "Vint"];
    private static readonly string[] Last = ["Lovelace", "Turing", "Hopper", "Torvalds", "Hamilton", "Ritchie", "Liskov", "Thompson", "Perlman", "Berners-Lee", "Allen", "Backus", "Johnson", "Dijkstra", "Lamarr", "Knuth", "Borg", "Stroustrup", "Wilson", "van Rossum", "Matsumoto", "Shaw", "Gosling", "Shaw", "Lamport", "Goldwasser", "Wirth", "Sammet", "Conway", "Cerf"];
    private static readonly string[] Prefixes = ["North", "Blue", "Bright", "Iron", "Silver", "Quantum", "Green", "Swift", "Solar", "Atlas", "Nova", "Pine", "Cedar", "Harbor", "Summit"];
    private static readonly string[] Suffixes = ["Labs", "Systems", "Works", "Logistics", "Health", "Foods", "Capital", "Studio", "Robotics", "Energy", "Media", "Networks"];
    private static readonly string[] Countries = ["United States", "Mexico", "Spain", "Germany", "Japan", "Brazil", "Canada", "France", "India", "Argentina", "Chile", "United Kingdom", "Italy", "Colombia", "Sweden"];

    private State _state = State.Of(Generate());

    public Customer? Find(int id)
    {
        var all = Volatile.Read(ref _state).All;
        return id >= 1 && id <= all.Length ? all[id - 1] : null;
    }

    public CustomerPage Query(CustomerQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var state = Volatile.Read(ref _state);
        var order = query.Sort switch
        {
            "company" => state.ByCompany,
            "revenue" => state.ByRevenue,
            "since" => state.BySince,
            _ => state.ByName,
        };
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var allPages = (order.Length + PageSize - 1) / PageSize;
            var current = Math.Clamp(query.Page, 1, allPages);
            var start = (current - 1) * PageSize;
            var items = Enumerable.Range(start, Math.Min(PageSize, order.Length - start))
                .Select(position => state.All[order[query.Descending ? order.Length - 1 - position : position]])
                .ToList();
            return new CustomerPage(items, order.Length, current, allPages);
        }

        var search = query.Search.Trim();
        var indexes = (query.Descending ? order.Reverse() : order).Where(index => state.All[index].Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || state.All[index].Company.Contains(search, StringComparison.OrdinalIgnoreCase)
                || state.All[index].Email.Contains(search, StringComparison.OrdinalIgnoreCase));

        var matching = indexes.ToList();
        var pages = Math.Max(1, (matching.Count + PageSize - 1) / PageSize);
        var page = Math.Clamp(query.Page, 1, pages);
        return new CustomerPage([.. matching.Skip((page - 1) * PageSize).Take(PageSize).Select(index => state.All[index])], matching.Count, page, pages);
    }

    public void Update(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ImmutableInterlocked.Update(ref _state, state => State.Of(state.All.SetItem(customer.Id - 1, customer)));
    }

    private static ImmutableArray<Customer> Generate()
    {
        var random = new Random(2026);
        var customers = ImmutableArray.CreateBuilder<Customer>(Count);
        for (var id = 1; id <= Count; id++)
        {
            var first = First[random.Next(First.Length)];
            var last = Last[random.Next(Last.Length)];
            var company = Prefixes[random.Next(Prefixes.Length)] + " " + Suffixes[random.Next(Suffixes.Length)];
            var plan = Plans[random.Next(Plans.Length)];
            var revenue = plan == "Free" ? 0 : Math.Round((decimal)(random.NextDouble() * random.NextDouble() * 250_000), 2);
            var email = $"{first}.{last}".ToLowerInvariant().Replace(' ', '-') + "@" + company.ToLowerInvariant().Replace(' ', '-') + ".com";
            customers.Add(new Customer(id, first + " " + last, email, company, Countries[random.Next(Countries.Length)], plan, revenue, new DateOnly(2018, 1, 1).AddDays(random.Next(3000)), random.NextDouble() > 0.12, string.Empty));
        }

        return customers.MoveToImmutable();
    }

    private sealed record State(ImmutableArray<Customer> All, ImmutableArray<int> ByName, ImmutableArray<int> ByCompany, ImmutableArray<int> ByRevenue, ImmutableArray<int> BySince)
    {
        public static State Of(ImmutableArray<Customer> all) => new(
            all,
            Order(all, customer => customer.Name),
            Order(all, customer => customer.Company),
            [.. Enumerable.Range(0, all.Length).OrderBy(index => all[index].Revenue).ThenBy(index => index)],
            [.. Enumerable.Range(0, all.Length).OrderBy(index => all[index].Since).ThenBy(index => index)]);

        private static ImmutableArray<int> Order(ImmutableArray<Customer> all, Func<Customer, string> key) =>
            [.. Enumerable.Range(0, all.Length).OrderBy(index => key(all[index]), StringComparer.Create(CultureInfo.InvariantCulture, true)).ThenBy(index => index)];
    }
}
