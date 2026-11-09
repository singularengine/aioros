using System.Collections.Immutable;
using Aioros.Composition;

namespace Aioros.Runtime.Composition;

internal sealed class FeatureRegistrar(TargetKind target) : IFeatureRegistrar
{
    public TargetKind Target => target;

    public ImmutableArray<ServiceRegistration> Registrations { get; private set; } = [];

    public void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService =>
        Registrations = Registrations.Add(new ServiceRegistration(typeof(TService), typeof(TImplementation)));
}
