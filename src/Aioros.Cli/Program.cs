using System.Reflection;
using System.Security.Cryptography;
using Aioros.Cli.Commands;

var directory = Directory.GetCurrentDirectory();
var projects = Directory.EnumerateFiles(directory, "*.csproj")
    .Concat(Directory.EnumerateDirectories(directory).Where(folder => !Path.GetFileName(folder).StartsWith('.')).SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj")))
    .Select(project => Path.GetRelativePath(directory, project))
    .Order(StringComparer.Ordinal)
    .ToList();
var app = AppFolder.Find(projects, project => Directory.Exists(Path.Combine(directory, Path.GetDirectoryName(project) ?? string.Empty, "Pages")));
var root = Path.Combine(directory, Path.GetDirectoryName(app.Project) ?? string.Empty);
var pages = Path.Combine(root, "Pages");
var files = app.Project is not null && Directory.Exists(pages)
    ? Directory.EnumerateFiles(pages, "*.axaml", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(root, file)).ToList()
    : [];
var version = typeof(Planner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0-preview.1";
var launch = Path.Combine(root, "Properties", "launchSettings.json");
var port = app.Project is not null && File.Exists(launch) ? AppFolder.LaunchPort(await File.ReadAllTextAsync(launch)) : null;
var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
var plan = Planner.Plan(args, app with { Files = files, Port = port }, version, free: LocalPorts.Free, token: token, owner: PortOwners.Of);

if (plan.Output.Length > 0)
{
    await (plan.ExitCode == 0 ? Console.Out : Console.Error).WriteAsync(plan.Output);
}

return await StepRunner.RunAsync(plan, directory, token);
