using Aioros.Composition;
using Aioros.Runtime.Composition;

namespace Aioros.Runtime.Tests.Composition;

public sealed class AiorosBuilderTests
{
    private interface IClock;

    private sealed class SystemClock : IClock;

    private sealed class WebOnlyFeature : IFeature
    {
        public FeatureInfo Info { get; } = new("WebOnly", [TargetKind.Ssr]);

        public void Register(IFeatureRegistrar registrar) => registrar.AddSingleton<IClock, SystemClock>();
    }

    [Fact]
    [Trait("AC", "COMPOSITION-01")]
    public void AFeatureRegistersItsServicesForATargetItSupports()
    {
        var result = new AiorosBuilder(TargetKind.Ssr).Add(new WebOnlyFeature()).Build();

        Assert.True(result.IsSuccess);
        Assert.Equal(TargetKind.Ssr, result.Composition.Target);
        Assert.Equal(new ServiceRegistration(typeof(IClock), typeof(SystemClock)), Assert.Single(result.Composition.Services));
    }

    [Fact]
    [Trait("AC", "COMPOSITION-02")]
    public void AFeatureIsRejectedOnATargetItDoesNotSupport()
    {
        var result = new AiorosBuilder(TargetKind.Desktop).Add(new WebOnlyFeature()).Build();

        Assert.False(result.IsSuccess);
        Assert.Equal("WebOnly does not support Desktop", Assert.Single(result.Errors));
    }
}
