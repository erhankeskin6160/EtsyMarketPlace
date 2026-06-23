namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Infrastructure.Automation;
using Xunit;

public sealed class WindowsTaskSchedulerCommandBuilderTests
{
    [Fact]
    public void Create_QuotesExecutablePathAndAddsAutomationSwitch()
    {
        var args = WindowsTaskSchedulerCommandBuilder.Create(
            @"C:\Program Files\Etsy Market Place\EtsyMarketPlace.exe",
            6);

        Assert.Contains("/Create", args);
        Assert.Contains("/TN", args);
        Assert.Contains(WindowsTaskSchedulerCommandBuilder.TaskName, args);
        Assert.Contains("/TR", args);
        Assert.Contains(@"""C:\Program Files\Etsy Market Place\EtsyMarketPlace.exe"" --automation-run", args);
        Assert.Contains("/SC", args);
        Assert.Contains("HOURLY", args);
        Assert.Contains("/MO", args);
        Assert.Contains("6", args);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(721)]
    public void Create_RejectsInvalidInterval(int intervalHours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowsTaskSchedulerCommandBuilder.Create(@"C:\app.exe", intervalHours));
    }

    [Fact]
    public void QueryRunAndDelete_TargetConfiguredTaskName()
    {
        Assert.Contains(WindowsTaskSchedulerCommandBuilder.TaskName, WindowsTaskSchedulerCommandBuilder.Query());
        Assert.Contains(WindowsTaskSchedulerCommandBuilder.TaskName, WindowsTaskSchedulerCommandBuilder.Run());
        Assert.Contains(WindowsTaskSchedulerCommandBuilder.TaskName, WindowsTaskSchedulerCommandBuilder.Delete());
    }
}
