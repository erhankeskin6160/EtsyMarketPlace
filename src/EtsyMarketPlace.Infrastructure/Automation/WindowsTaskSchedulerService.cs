namespace EtsyMarketPlace.Infrastructure.Automation;

using System.Diagnostics;

public sealed record WindowsTaskResult(bool Success, string Output);

public static class WindowsTaskSchedulerCommandBuilder
{
    public const string TaskName = "EtsyMarketPlace-Automation";

    public static IReadOnlyList<string> Create(string executablePath, int intervalHours)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("EXE yolu gerekli.", nameof(executablePath));
        if (intervalHours is < 1 or > 720)
            throw new ArgumentOutOfRangeException(nameof(intervalHours), "Gorev araligi 1-720 saat olmalidir.");

        var action = $"\"{Path.GetFullPath(executablePath)}\" --automation-run";
        return [
            "/Create", "/TN", TaskName, "/TR", action,
            "/SC", "HOURLY", "/MO", intervalHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "/F",
        ];
    }

    public static IReadOnlyList<string> Query() => ["/Query", "/TN", TaskName, "/V", "/FO", "LIST"];
    public static IReadOnlyList<string> Run() => ["/Run", "/TN", TaskName];
    public static IReadOnlyList<string> Delete() => ["/Delete", "/TN", TaskName, "/F"];
}

public sealed class WindowsTaskSchedulerService
{
    public Task<WindowsTaskResult> CreateAsync(
        string executablePath,
        int intervalHours,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(WindowsTaskSchedulerCommandBuilder.Create(executablePath, intervalHours), cancellationToken);

    public Task<WindowsTaskResult> QueryAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(WindowsTaskSchedulerCommandBuilder.Query(), cancellationToken);

    public Task<WindowsTaskResult> RunAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(WindowsTaskSchedulerCommandBuilder.Run(), cancellationToken);

    public Task<WindowsTaskResult> DeleteAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(WindowsTaskSchedulerCommandBuilder.Delete(), cancellationToken);

    private static async Task<WindowsTaskResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return new WindowsTaskResult(false, "Windows Gorev Zamanlayici yalnizca Windows uzerinde kullanilabilir.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("schtasks.exe baslatilamadi.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();
        var combined = string.Join(Environment.NewLine, new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return new WindowsTaskResult(process.ExitCode == 0, combined);
    }
}
