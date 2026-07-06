var builder = DistributedApplication.CreateBuilder(args);

// A PostgreSQL container that backs Marten's event store. WithDataVolume keeps the
// data across runs (so the first administrator you create in the Setup page survives
// restarts) and WithPgAdmin adds a browser-based admin UI as a companion container.
var postgres = builder.AddPostgres("postgres").WithDataVolume("marten-identity-data").WithPgAdmin();

// The logical database. Aspire hands its connection string to the web app below as the
// "identitydb" connection string, which the web app passes straight to Marten.
var identityDb = postgres.AddDatabase("identitydb");

builder
    .AddProject<Projects.MartenIdentity_Aspire_Web>("web")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
