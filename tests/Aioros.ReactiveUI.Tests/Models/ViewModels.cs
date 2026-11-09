using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace Aioros.ReactiveUI.Tests.Models;

public sealed class SearchViewModel(IScreen screen) : ReactiveObject, IRoutableViewModel
{
    public string UrlPathSegment => "search";

    public IScreen HostScreen { get; } = screen;
}

public sealed class AlbumViewModel(int id, IScreen screen) : ReactiveObject, IRoutableViewModel
{
    public int Id { get; } = id;

    public string UrlPathSegment => "album";

    public IScreen HostScreen { get; } = screen;
}

public sealed class LostViewModel(IScreen screen) : ReactiveObject, IRoutableViewModel
{
    public string? UrlPathSegment => null;

    public IScreen HostScreen { get; } = screen;
}

public sealed class StoreViewModel : ReactiveObject, IRoutableViewModel
{
    public StoreViewModel(IScreen screen)
    {
        HostScreen = screen;
        OpenAlbum = ReactiveCommand.CreateFromObservable<int, IRoutableViewModel>(id => HostScreen.Router.Navigate.Execute(new AlbumViewModel(id, HostScreen)));
        Search = ReactiveCommand.CreateFromObservable(() => HostScreen.Router.NavigateAndReset.Execute(new SearchViewModel(HostScreen)));
        Lose = ReactiveCommand.CreateFromObservable(() => HostScreen.Router.Navigate.Execute(new LostViewModel(HostScreen)));
    }

    public string UrlPathSegment => "store";

    public IScreen HostScreen { get; }

    public ReactiveCommand<int, IRoutableViewModel> OpenAlbum { get; }

    public ReactiveCommand<RxVoid, IRoutableViewModel> Search { get; }

    public ReactiveCommand<RxVoid, IRoutableViewModel> Lose { get; }
}

public sealed class ShellViewModel : ReactiveObject, IScreen
{
    public ShellViewModel()
    {
        Router.Navigate.Execute(new SearchViewModel(this)).Subscribe();
        OpenAlbum = ReactiveCommand.CreateFromObservable<int, IRoutableViewModel>(id => Router.Navigate.Execute(new AlbumViewModel(id, this)));
    }

    public RoutingState Router { get; } = new();

    public ReactiveCommand<int, IRoutableViewModel> OpenAlbum { get; }
}

public sealed class ActivatedViewModel : ReactiveObject, IActivatableViewModel
{
    public ActivatedViewModel(Activations activations)
    {
        this.WhenActivated(disposables =>
        {
            activations.Started++;
            disposables.Add(new ActionDisposable(() => activations.Ended++));
        });
    }

    public ViewModelActivator Activator { get; } = new();
}

public sealed class ShelfViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
{
    public ShelfViewModel(IScreen screen, Activations activations)
    {
        HostScreen = screen;
        Open = ReactiveCommand.CreateFromObservable(() => HostScreen.Router.Navigate.Execute(new AlbumViewModel(7, HostScreen)));
        this.WhenActivated(disposables =>
        {
            activations.Started++;
            disposables.Add(new ActionDisposable(() => activations.Ended++));
        });
    }

    public string UrlPathSegment => "shelf";

    public IScreen HostScreen { get; }

    public ViewModelActivator Activator { get; } = new();

    public ReactiveCommand<RxVoid, IRoutableViewModel> Open { get; }
}

public sealed class Activations
{
    public int Started { get; set; }

    public int Ended { get; set; }
}
