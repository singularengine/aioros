using Aioros.Cli.Commands;

namespace Aioros.Cli.Tests.Commands;

public sealed class BuildOutputTests
{
    [Fact]
    [Trait("AC", "CLI-11")]
    public void CompileErrorsAreReadFromTheWatchOutput()
    {
        var output = new BuildOutput();
        string[] lines =
        [
            "dotnet watch : Waiting for changes",
            "dotnet watch : File updated: ./Models/HomeModel.cs",
            "dotnet watch : Unable to apply changes due to compilation errors.",
            "dotnet watch : /app/Models/HomeModel.cs(11,19): error CS1002: ; expected",
            "dotnet watch : [Shop (net10.0)] /app/obj/Debug/net10.0/AiorosPages.g.cs(12,5): warning ENC0118: Changing 'auto-property' might not have any effect",
            "dotnet watch : [Shop (net10.0)] error ENC0033: Deleting field 'Shop.HomeModel.countCommand' requires restarting the application.",
            "/app/Pages/Home.cs(3,1): error CS0246: The type or namespace name 'Nope' could not be found [/app/Shop.csproj]",
            "dotnet watch : File updated: ./Models/HomeModel.cs",
            "\u001b[31mdotnet watch : /app/Models/HomeModel.cs(12,2): error CS1513: } expected\u001b[39m",
            "dotnet watch : File updated: ./Models/HomeModel.cs",
            "dotnet watch : C# and Razor changes applied in 75ms.",
            "dotnet watch : File updated: ./Models/HomeModel.cs",
            "dotnet watch : No managed code changes to apply.",
        ];

        var reports = lines.Select(output.Read).ToList();

        Assert.All(reports.Take(3).Concat(reports.Skip(4).Take(2)).Concat(reports.Skip(7).Take(1)).Concat(reports.Skip(9).Take(1)).Concat(reports.Skip(11)), Assert.Null);
        Assert.Equal([new BuildError("/app/Models/HomeModel.cs", 11, 19, "CS1002", "; expected")], reports[3]);
        Assert.Equal(
            [new BuildError("/app/Models/HomeModel.cs", 11, 19, "CS1002", "; expected"), new BuildError("/app/Pages/Home.cs", 3, 1, "CS0246", "The type or namespace name 'Nope' could not be found")],
            reports[6]);
        Assert.Equal([new BuildError("/app/Models/HomeModel.cs", 12, 2, "CS1513", "} expected")], reports[8]);
        Assert.Empty(reports[10]!);
    }
}
