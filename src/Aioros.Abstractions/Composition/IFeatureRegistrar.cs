namespace Aioros.Composition;

public interface IFeatureRegistrar
{
    TargetKind Target { get; }

    void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;
}
