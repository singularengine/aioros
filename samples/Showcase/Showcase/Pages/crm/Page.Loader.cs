using Aioros.Generated;
using Aioros.Pages;
using Showcase.Models;

namespace Showcase.Pages.Crm;

public sealed class CustomersLoader(CustomerStore store)
{
    private static readonly string[] Sorts = ["name", "company", "revenue", "since"];

    public LoaderResult<CrmData> Load(string? q, string? sort, string? dir, string? saved, int page = 1)
    {
        var customers = new CustomerQuery(q, sort is not null && Sorts.Contains(sort) ? sort : "name", dir == "desc", page);
        return LoaderResult.With(
            new CrmData(customers, store.Query(customers), saved is not null, AiorosSources.Text("Pages/crm/Page.axaml") ?? string.Empty),
            "Customers · Aioros Showcase",
            new PageMetadata("100,000 customers searched, sorted and paged on the server, in XAML."));
    }
}
