namespace TinyTaskPlus.Core;

public enum MacroEventType
{
	KeyDown,
	KeyUp,
	Char,
	MouseMove,
	MouseDown,
	MouseUp,
	MouseWheel,
}

public sealed class MacroEvent
{
	public MacroEventType EventType { get; set; }
	public int VirtualKey { get; set; }
	public int ScanCode { get; set; }
	public int CharCode { get; set; }
	public int MouseX { get; set; }
	public int MouseY { get; set; }
	public int MouseDelta { get; set; }
	public string? TargetWindowHandleHex { get; set; }
	public string? TargetChildHandleHex { get; set; }
	public long TimestampMs { get; set; }
}

public sealed class Macro
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = "New Macro";
	public List<MacroEvent> Events { get; set; } = new();
	public MacroAssignment? Assignment { get; set; }
}

public sealed class MacroAssignment
{
	public string? WindowTitle { get; set; }
	public string? ProcessPath { get; set; }
	public int? ProcessId { get; set; }
	public string? TopLevelHwndHex { get; set; }
	public string? ChildHwndHex { get; set; }
}

public interface IMacroRecorder : IAsyncDisposable
{
	Task StartAsync();
	Task<Macro> StopAsync(string name);
	bool IsRecording { get; }
}

public interface IMacroPlayer
{
	Task PlayAsync(Macro macro, CancellationToken cancellationToken);
}

public interface IMacroStorage
{
	Task SaveAsync(IEnumerable<Macro> macros, string path, CancellationToken cancellationToken = default);
	Task<List<Macro>> LoadAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class JsonMacroStorage : IMacroStorage
{
	public async Task SaveAsync(IEnumerable<Macro> macros, string path, CancellationToken cancellationToken = default)
	{
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
		await using var fs = File.Create(path);
		await System.Text.Json.JsonSerializer.SerializeAsync(fs, macros, cancellationToken: cancellationToken);
	}

	public async Task<List<Macro>> LoadAsync(string path, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(path)) return new List<Macro>();
		await using var fs = File.OpenRead(path);
		var loaded = await System.Text.Json.JsonSerializer.DeserializeAsync<List<Macro>>(fs, cancellationToken: cancellationToken);
		return loaded ?? new List<Macro>();
	}
}
