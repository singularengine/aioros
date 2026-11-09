using Aioros.Hosting;
using Aioros.Runtime.Pages;

namespace Microsoft.AspNetCore.Builder;

public static class AiorosNavigationRegistration
{
    /// <summary>
    /// Registers the app's own navigation interface, so view models navigate without referencing Aioros. The generator
    /// finds this call at build time and implements the interface over the page's navigator: every method takes the
    /// path as a <see cref="string"/>, optionally followed by a <see cref="CancellationToken"/>, and returns
    /// <see cref="Task"/>, <see cref="ValueTask"/> or <c>void</c>. On the web a navigation answers <c>303</c> after a
    /// command and <c>302</c> while the page loads; on the desktop it moves the page stack.
    /// </summary>
    /// <param name="builder">The app's builder.</param>
    public static WebApplicationBuilder AddNavigation<TInterface>(this WebApplicationBuilder builder)
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddNavigation<TInterface>(AiorosHost.App(builder).Navigators);
        return builder;
    }
}
