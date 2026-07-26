global using AndreGoepel.Testing.E2E;
global using Microsoft.Playwright;
global using static Microsoft.Playwright.Assertions;
global using Xunit;
// Disambiguates against AndreGoepel.Testing.E2E.E2EAppFixture: every test constructs against this
// repo's own subclass (see Infrastructure/E2EAppFixture.cs), never the package's base type directly.
global using E2EAppFixture = AndreGoepel.Marten.Identity.E2ETests.Infrastructure.E2EAppFixture;
