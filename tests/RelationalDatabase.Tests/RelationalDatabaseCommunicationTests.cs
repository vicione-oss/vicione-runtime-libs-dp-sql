using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class RelationalDatabaseCommunication_MinimumPublishIntervalInMs
{
    [Fact]
    public void Defaults_to_1000_ms()
        => new RelationalDatabase_.TestCommunication().MinimumPublishIntervalInMs.Should().Be(1_000);
}
