using Xunit;

namespace DashboardApi.Tests;

public class SmokeTests
{
    [Fact]
    public void Backend_Project_Compiles_And_Tests_Run()
    {
        // Trivial assertion to verify the test infrastructure works
        Assert.True(true);
    }

    [Fact]
    public void ConfigStore_Can_Be_Instantiated()
    {
        // Verify the main service type is accessible from the test project
        var type = typeof(DashboardApi.Services.ConfigStore);
        Assert.NotNull(type);
    }
}
