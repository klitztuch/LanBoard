var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

builder.AddProject<Projects.LanBoard_Web>("web")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("DOTNET_MODIFIABLE_ASSEMBLIES", "debug");

await builder.Build().RunAsync();