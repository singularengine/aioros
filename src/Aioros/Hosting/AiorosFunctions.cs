using Aioros.Hosting;
using Aioros.Targets.Ssr.Functions;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

public static class AiorosFunctionRegistration
{
    public const string ServerSetting = "Aioros:Server";

    /// <summary>
    /// Declares a service interface as server functions and registers it with its implementation. The generator finds
    /// this call at build time and emits the interface's routes, <c>/_sf/{group}/{name}</c>, and its client. The
    /// website and the desktop app running its server in process call the implementation; a desktop app configured
    /// with <c>Aioros:Server</c> calls that server through the generated client. Who may call each function is
    /// <c>[Authorize]</c> on <typeparamref name="TImplementation"/> or its methods, checked on every call.
    /// </summary>
    /// <param name="builder">The app's builder.</param>
    /// <param name="group">
    /// The stable route segment, a constant; by default the interface's name without its <c>I</c> prefix and
    /// <c>Functions</c> suffix, in camel case (<c>IBoardFunctions</c> gives <c>board</c>), which changes if the
    /// interface is renamed.
    /// </param>
    public static WebApplicationBuilder AddServerFunction<TInterface, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this WebApplicationBuilder builder, string? group = null)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddServerFunction<TInterface, TImplementation>(AiorosHost.App(builder).Functions, AiorosHost.Mode(builder).Server, group);
        return builder;
    }
}
