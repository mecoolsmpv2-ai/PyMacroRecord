namespace TinyTaskPlus.Core;

public sealed class ProcessInfo
{
	public int ProcessId { get; set; }
	public string ProcessName { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? MainWindowTitle { get; set; }
	public string? MainWindowHandleHex { get; set; }
	public override string ToString() => string.IsNullOrWhiteSpace(MainWindowTitle)
		? $"{ProcessName} (PID {ProcessId})"
		: $"{MainWindowTitle} — {ProcessName} (PID {ProcessId})";
}

public interface IProcessService
{
	Task<IReadOnlyList<ProcessInfo>> GetRunningProcessesAsync(CancellationToken cancellationToken = default);
}