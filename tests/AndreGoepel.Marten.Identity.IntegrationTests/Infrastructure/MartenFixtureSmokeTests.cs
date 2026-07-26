namespace AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;

[Collection(IntegrationCollection.Name)]
public class MartenFixtureSmokeTests(MartenFixture fixture)
{
    [Fact]
    public async Task MartenFixture_ContainerStarted_SavesSessionSuccessfully()
    {
        // Arrange / Act
        await using var session = fixture.Store.LightweightSession();
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(fixture.Store);
    }
}
