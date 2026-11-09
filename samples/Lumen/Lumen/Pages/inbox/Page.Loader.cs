using Aioros.Pages;
using Lumen.Data;
using Lumen.ViewModels;

namespace Lumen.Pages.Inbox;

public sealed class InboxLoader(Store store)
{
    public LoaderResult<InboxData> Load() =>
        LoaderResult.With(new InboxData(store.Notifications()), "Inbox · Lumen", new PageMetadata("Assignments, mentions and status changes.", "/inbox"));
}
