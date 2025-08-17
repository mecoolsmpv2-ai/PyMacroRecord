using System.Diagnostics;
using TinyTaskPlus.Core;

namespace TinyTaskPlus.Win;

public sealed class WindowsProcessService : IProcessService
{
	public Task<IReadOnlyList<ProcessInfo>> GetRunningProcessesAsync(CancellationToken cancellationToken = default)
	{
		var list = new List<ProcessInfo>();
		foreach (var p in Process.GetProcesses())
		{
			try
			{
				var info = new ProcessInfo
				{
					ProcessId = p.Id,
					ProcessName = p.ProcessName,
					FileName = SafeMainModuleFileName(p),
					MainWindowTitle = p.MainWindowTitle,
					MainWindowHandleHex = p.MainWindowHandle != IntPtr.Zero ? ((nint)p.MainWindowHandle).ToString("X") : null
				};
				list.Add(info);
			}
			catch { }
		}
		// Order by title, then name
		list.Sort((a,b) => string.Compare(a.MainWindowTitle ?? string.Empty, b.MainWindowTitle ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		return Task.FromResult((IReadOnlyList<ProcessInfo>)list);
	}

	private static string? SafeMainModuleFileName(Process p)
	{
		try { return p.MainModule?.FileName; } catch { return null; }
	}
}