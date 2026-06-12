using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace ViciOne.Suite.DataPort;

public class TaskPipeline_ExecuteAsync
{
    [Fact]
    public async Task Returns_predefined_result_on_failure_Async()
    {
        var taskPipeline = new TaskPipeline<bool>(
            1,
            count => System.TimeSpan.Zero,
            false);

        var result = await taskPipeline.ExecuteAsync((cancellation) => new ValueTask<bool>(false), TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_result_on_success_Async()
    {
        var taskPipeline = new TaskPipeline<bool>(
            1,
            count => System.TimeSpan.Zero,
            false);

        var result = await taskPipeline.ExecuteAsync((cancellation) => new ValueTask<bool>(true), TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }
}
