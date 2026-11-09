using Aioros.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

public static class AiorosHostRole
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// What this host is: <see cref="AiorosRole.Server"/> for the website, <see cref="AiorosRole.InProcess"/> for the
        /// desktop running the server's services itself, <see cref="AiorosRole.Client"/> for the installed app working
        /// against a remote server (<c>Aioros:Server</c>).
        /// </summary>
        public AiorosRole Role => AiorosHost.Mode(builder).Role;

        /// <summary>
        /// Registers what only the server runs: databases, seeders, background work. The website and the desktop
        /// running in process register it; the installed client skips it, since its loaders and server functions run
        /// on the remote server. Registrations outside this call run on every host.
        /// </summary>
        /// <param name="configure">Adds the server's services.</param>
        public WebApplicationBuilder Server(Action<IServiceCollection> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            if (builder.Role != AiorosRole.Client)
            {
                configure(builder.Services);
            }

            return builder;
        }
    }
}
