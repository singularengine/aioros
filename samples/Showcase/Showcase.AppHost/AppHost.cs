var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var games = postgres.AddDatabase("games");
var signIn = postgres.AddDatabase("signin");

var web = builder.AddProject<Projects.Showcase>("web")
    .WithReference(games)
    .WithReference(signIn)
    .WaitFor(games)
    .WaitFor(signIn)
    .WithHttpHealthCheck("/health");

Port(builder.AddProject<Projects.Kartova>("kartova"));
Port(builder.AddProject<Projects.AdvancedToDoList>("advancedtodolist"));
Port(builder.AddProject<Projects.StyletSamples>("stylet"));
Port(builder.AddProject<Projects.ReactiveRouting>("reactiverouting"));

builder.Build().Run();

void Port(IResourceBuilder<ProjectResource> port) => web.WithEnvironment($"Ports__{port.Resource.Name}", port.WithHttpHealthCheck("/").GetEndpoint("http"));
