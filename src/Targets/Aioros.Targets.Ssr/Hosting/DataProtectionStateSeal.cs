using System.Security.Cryptography;
using Aioros.Runtime.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Hosting;

/// <summary>
/// Seals a library control's travelling state with ASP.NET Core Data Protection (purpose <c>Aioros.ControlState</c>), as
/// ASP.NET Web Forms signed its view state, so a post cannot choose a state the server did not write; the host keeps and
/// rotates the keys, as it does for antiforgery tokens (decision "Sealed control state").
/// </summary>
public sealed class DataProtectionStateSeal(IDataProtectionProvider provider) : IStateSeal
{
    public const string Purpose = "Aioros.ControlState";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Seal(string value) => _protector.Protect(value);

    public bool TryOpen(string sealedValue, out string value)
    {
        try
        {
            value = _protector.Unprotect(sealedValue);
            return true;
        }
        catch (CryptographicException)
        {
            value = string.Empty;
            return false;
        }
    }

    /// <summary>Seals the travelling states every answer of the app writes, the seal set for each request on its thread (<see cref="StateSeals"/>).</summary>
    public static IServiceCollection AddStateSeal(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDataProtection();
        services.AddSingleton<IStateSeal, DataProtectionStateSeal>();
        services.AddSingleton<Pages.DraftScopes>();
        services.AddTransient<IStartupFilter, SealFirst>();
        return services;
    }

    private sealed class SealFirst : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            var seal = app.ApplicationServices.GetService<IStateSeal>();
            app.Use(async (http, inner) =>
            {
                using (StateSeals.Use(seal))
                {
                    await inner(http);
                }
            });
            next(app);
        };
    }
}
