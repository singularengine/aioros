namespace Showcase.Models;

/// <summary>Changes to the customers, which live on the server.</summary>
public interface ICustomerFunctions
{
    Task SaveAsync(Customer customer);
}
