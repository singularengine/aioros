using Aioros.Pages;
using Lumen.Data;

namespace Lumen.Pages.Settings;

public sealed class SettingsLoader(Store store)
{
    public LoaderResult<Profile> Load() =>
        LoaderResult.With(store.Profile, "Settings · Lumen", new PageMetadata("Profile and notification settings.", "/settings"));
}
