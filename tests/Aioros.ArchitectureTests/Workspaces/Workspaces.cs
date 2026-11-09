namespace Aioros.ArchitectureTests.Workspaces;

public static class Workspaces
{
    public static string Root { get; } = FindRoot(AppContext.BaseDirectory);

    public static Task<Workspace> ProductionAsync() =>
        Workspace.LoadAsync(Path.Combine(Root, "src"), ProjectMaps.Production);

    public static Task<Workspace> CompliantAsync() =>
        Workspace.LoadAsync(Fixture("Compliant"), ProjectMaps.Compliant);

    public static Task<Workspace> ViolatingAsync() =>
        Workspace.LoadAsync(Fixture("Violating"), ProjectMaps.Violating);

    public static string Fixture(params string[] path) =>
        Path.Combine([Root, "tests", "Aioros.ArchitectureTests", "Fixtures", .. path]);

    private static string FindRoot(string directory) =>
        File.Exists(Path.Combine(directory, "Aioros.slnx"))
            ? directory
            : FindRoot(Directory.GetParent(directory)?.FullName ?? throw new DirectoryNotFoundException("Aioros.slnx not found"));
}
