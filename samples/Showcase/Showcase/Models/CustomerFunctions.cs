using System.ComponentModel.DataAnnotations;

namespace Showcase.Models;

public sealed class CustomerFunctions(CustomerStore store) : ICustomerFunctions
{
    public Task SaveAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (store.Find(customer.Id) is null)
        {
            throw new KeyNotFoundException("That customer does not exist.");
        }

        string[] missing = [.. new[] { (customer.Name, nameof(Customer.Name)), (customer.Email, nameof(Customer.Email)), (customer.Company, nameof(Customer.Company)) }
            .Where(field => string.IsNullOrWhiteSpace(field.Item1))
            .Select(field => field.Item2)];
        if (missing.Length > 0)
        {
            throw new ValidationException(new ValidationResult("A customer needs a name, an email and a company.", missing), null, null);
        }

        store.Update(customer);
        return Task.CompletedTask;
    }
}
