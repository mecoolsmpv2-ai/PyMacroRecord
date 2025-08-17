namespace TinyTaskPlus.Core;

public sealed class WindowInfo
{
	public string HandleHex { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public int ProcessId { get; set; }
	public string ProcessName { get; set; } = string.Empty;
	public string? ProcessPath { get; set; }

	public override string ToString() => string.IsNullOrWhiteSpace(Title)
		? $"{ProcessName} (0x{HandleHex})"
		: $"{Title} — {ProcessName}";
}

public interface IWindowAssignmentService
{
	Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken cancellationToken = default);
}