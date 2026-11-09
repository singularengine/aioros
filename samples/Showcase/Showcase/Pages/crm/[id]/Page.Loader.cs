using Aioros.Generated;
using Aioros.Pages;
using Showcase.Models;

namespace Showcase.Pages.Crm;

public sealed class CustomerLoader(CustomerStore store)
{
    public LoaderResult<CustomerData> Load(int id) =>
        store.Find(id) is { } customer
            ? LoaderResult.With(new CustomerData(customer, AiorosSources.Text("Pages/crm/[id]/Page.axaml") ?? string.Empty), customer.Name + " · Aioros Showcase")
            : LoaderResult.NotFound();
}
