namespace Aioros.Development;

public sealed record XamlReloaded(string Source);

public sealed record XamlReloadFailed(string Source, string Message);
