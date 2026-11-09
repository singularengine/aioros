namespace Aioros.Composition;

public interface IFeature
{
    FeatureInfo Info { get; }

    void Register(IFeatureRegistrar registrar);
}
