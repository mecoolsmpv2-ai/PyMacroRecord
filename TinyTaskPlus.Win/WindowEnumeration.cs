using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TinyTaskPlus.Core;

namespace TinyTaskPlus.Win;

public sealed class WindowsWindowAssignmentService : IWindowAssignmentService
{
	public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken cancellationToken = default)
	{
		var result = new List<WindowInfo>();
		User32.EnumWindows((hWnd, lParam) =>
		{
			if (cancellationToken.IsCancellationRequested) return false;
			if (!User32.IsWindowVisible(hWnd)) return true;
			int textLen = User32.GetWindowTextLength(hWnd);
			var titleBuilder = new StringBuilder(textLen + 1);
			User32.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
			var clsBuilder = new StringBuilder(256);
			User32.GetClassName(hWnd, clsBuilder, clsBuilder.Capacity);
			User32.GetWindowThreadProcessId(hWnd, out uint pid);
			string procName = string.Empty;
			string? procPath = null;
			try
			{
				using var proc = Process.GetProcessById((int)pid);
				procName = proc.ProcessName;
				procPath = SafeGetMainModuleFileName(proc);
			}
			catch { }
			var info = new WindowInfo
			{
				HandleHex = ((nint)hWnd).ToString("X"),
				Title = titleBuilder.ToString(),
				ClassName = clsBuilder.ToString(),
				ProcessId = (int)pid,
				ProcessName = procName,
				ProcessPath = procPath,
			};
			// Filter out tool windows with no title and no process name
			if (!string.IsNullOrWhiteSpace(info.Title) || !string.IsNullOrWhiteSpace(info.ProcessName))
			{
				result.Add(info);
			}
			return true;
		}, IntPtr.Zero);
		return Task.FromResult((IReadOnlyList<WindowInfo>)result);
	}

	private static string? SafeGetMainModuleFileName(Process process)
	{
		try { return process.MainModule?.FileName; } catch { return null; }
	}
}