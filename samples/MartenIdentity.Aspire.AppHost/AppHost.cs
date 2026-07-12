var builder = DistributedApplication.CreateBuilder(args);

// The E2E test harness boots this AppHost with "E2E=true". In that mode Postgres runs
// without its persistent volume (and without the pgAdmin companion) so every suite run
// starts from a throwaway, empty database instead of the developer's local data.
var isE2E = string.Equals(builder.Configuration["E2E"], "true", StringComparison.OrdinalIgnoreCase);

// A PostgreSQL container that backs Marten's event store. WithDataVolume keeps the
// data across runs (so the first administrator you create in the Setup page survives
// restarts) and WithPgAdmin adds a browser-based admin UI as a companion container.
var postgres = builder.AddPostgres("postgres");
if (!isE2E)
{
    postgres = postgres.WithDataVolume("marten-identity-data").WithPgAdmin();
}

// The logical database. Aspire hands its connection string to the web app below as the
// "identitydb" connection string, which the web app passes straight to Marten.
var identityDb = postgres.AddDatabase("identitydb");

var web = builder
    .AddProject<Projects.MartenIdentity_Aspire_Web>("web")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithExternalHttpEndpoints();

if (isE2E)
{
    // Tells the web app to swap in the capturing email sender and expose /e2e/emails so the
    // browser tests can read the confirmation/reset links it would otherwise only log.
    web.WithEnvironment("E2E", "true");
}

builder.Build().Run();
