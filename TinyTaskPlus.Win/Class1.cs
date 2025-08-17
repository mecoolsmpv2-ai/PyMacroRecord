using System.Runtime.InteropServices;
using System.Text;
using TinyTaskPlus.Core;

namespace TinyTaskPlus.Win;

internal static class User32
{
	public const int WH_KEYBOARD_LL = 13;
	public const int WH_MOUSE_LL = 14;
	public const int WM_KEYDOWN = 0x0100;
	public const int WM_KEYUP = 0x0101;
	public const int WM_CHAR = 0x0102;
	public const int WM_SYSKEYDOWN = 0x0104;
	public const int WM_SYSKEYUP = 0x0105;
	public const int WM_MOUSEMOVE = 0x0200;
	public const int WM_LBUTTONDOWN = 0x0201;
	public const int WM_LBUTTONUP = 0x0202;
	public const int WM_RBUTTONDOWN = 0x0204;
	public const int WM_RBUTTONUP = 0x0205;
	public const int WM_MBUTTONDOWN = 0x0207;
	public const int WM_MBUTTONUP = 0x0208;
	public const int WM_MOUSEWHEEL = 0x020A;

	public const int MK_LBUTTON = 0x0001;

	public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool UnhookWindowsHookEx(IntPtr hhk);
	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern IntPtr GetModuleHandle(string lpModuleName);

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();
	[DllImport("user32.dll", SetLastError = true)]
	public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
	[DllImport("user32.dll", SetLastError = true)]
	public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
	[DllImport("user32.dll", SetLastError = true)]
	public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
	[DllImport("user32.dll")]
	public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
	[DllImport("user32.dll")]
	public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
	[DllImport("user32.dll")]
	public static extern IntPtr WindowFromPoint(POINT Point);
	[DllImport("user32.dll")]
	public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
	public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
	[DllImport("user32.dll")]
	public static extern bool IsWindowVisible(IntPtr hWnd);
	[DllImport("user32.dll", SetLastError = true)]
	public static extern int GetWindowTextLength(IntPtr hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT { public int X; public int Y; }

	[StructLayout(LayoutKind.Sequential)]
	public struct KBDLLHOOKSTRUCT
	{
		public uint vkCode;
		public uint scanCode;
		public uint flags;
		public uint time;
		public nint dwExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MSLLHOOKSTRUCT
	{
		public POINT pt;
		public uint mouseData;
		public uint flags;
		public uint time;
		public nint dwExtraInfo;
	}
}

public sealed class WindowsMacroRecorder : IMacroRecorder
{
	private IntPtr _keyboardHook = IntPtr.Zero;
	private IntPtr _mouseHook = IntPtr.Zero;
	private readonly List<MacroEvent> _events = new();
	private long _startMs;
	private readonly User32.HookProc _keyboardProc;
	private readonly User32.HookProc _mouseProc;

	public WindowsMacroRecorder()
	{
		_keyboardProc = KeyboardProc;
		_mouseProc = MouseProc;
	}

	public bool IsRecording { get; private set; }

	public Task StartAsync()
	{
		if (IsRecording) return Task.CompletedTask;
		_events.Clear();
		_startMs = Environment.TickCount64;
		var hInstance = User32.GetModuleHandle(System.Diagnostics.Process.GetCurrentProcess().MainModule!.ModuleName!);
		_keyboardHook = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _keyboardProc, hInstance, 0);
		_mouseHook = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc, hInstance, 0);
		IsRecording = true;
		return Task.CompletedTask;
	}

	public Task<Macro> StopAsync(string name)
	{
		if (_keyboardHook != IntPtr.Zero) { User32.UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
		if (_mouseHook != IntPtr.Zero) { User32.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
		IsRecording = false;
		var macro = new Macro { Name = name, Events = new List<MacroEvent>(_events) };
		return Task.FromResult(macro);
	}

	private IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam)
	{
		if (code >= 0)
		{
			var k = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
			var evtType = (int)wParam switch
			{
				User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN => MacroEventType.KeyDown,
				User32.WM_KEYUP or User32.WM_SYSKEYUP => MacroEventType.KeyUp,
				User32.WM_CHAR => MacroEventType.Char,
				_ => (MacroEventType)(-1)
			};
			if ((int)evtType != -1)
			{
				var fg = User32.GetForegroundWindow();
				_events.Add(new MacroEvent
				{
					EventType = evtType,
					VirtualKey = (int)k.vkCode,
					ScanCode = (int)k.scanCode,
					TimestampMs = Environment.TickCount64 - _startMs,
					TargetWindowHandleHex = fg != IntPtr.Zero ? ((nint)fg).ToString("X") : null,
				});
			}
		}
		return User32.CallNextHookEx(_keyboardHook, code, wParam, lParam);
	}

	private IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
	{
		if (code >= 0)
		{
			var m = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
			var hwnd = User32.WindowFromPoint(m.pt);
			var evtType = (int)wParam switch
			{
				User32.WM_MOUSEMOVE => MacroEventType.MouseMove,
				User32.WM_LBUTTONDOWN => MacroEventType.MouseDown,
				User32.WM_LBUTTONUP => MacroEventType.MouseUp,
				User32.WM_MOUSEWHEEL => MacroEventType.MouseWheel,
				_ => (MacroEventType)(-1)
			};
			if ((int)evtType != -1)
			{
				_events.Add(new MacroEvent
				{
					EventType = evtType,
					MouseX = m.pt.X,
					MouseY = m.pt.Y,
					MouseDelta = evtType == MacroEventType.MouseWheel ? (short)((m.mouseData >> 16) & 0xFFFF) : 0,
					TimestampMs = Environment.TickCount64 - _startMs,
					TargetWindowHandleHex = hwnd != IntPtr.Zero ? ((nint)hwnd).ToString("X") : null,
				});
			}
		}
		return User32.CallNextHookEx(_mouseHook, code, wParam, lParam);
	}

	public ValueTask DisposeAsync()
	{
		if (_keyboardHook != IntPtr.Zero) { User32.UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
		if (_mouseHook != IntPtr.Zero) { User32.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
		return ValueTask.CompletedTask;
	}
}

public sealed class WindowsPostMessageMacroPlayer : IMacroPlayer
{
	public async Task PlayAsync(Macro macro, CancellationToken cancellationToken)
	{
		if (macro.Events.Count == 0) return;
		var start = Environment.TickCount64;
		for (int i = 0; i < macro.Events.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var ev = macro.Events[i];
			var delay = ev.TimestampMs - (Environment.TickCount64 - start);
			if (delay > 1) await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
			DispatchToAssignedWindow(macro, ev);
		}
	}

	private static void DispatchToAssignedWindow(Macro macro, MacroEvent ev)
	{
		var hwndHex = macro.Assignment?.TopLevelHwndHex ?? ev.TargetWindowHandleHex;
		if (string.IsNullOrEmpty(hwndHex)) return;
		var hwnd = (IntPtr)Convert.ToInt64(hwndHex, 16);

		switch (ev.EventType)
		{
			case MacroEventType.KeyDown:
				User32.PostMessage(hwnd, User32.WM_KEYDOWN, (IntPtr)ev.VirtualKey, IntPtr.Zero);
				break;
			case MacroEventType.KeyUp:
				User32.PostMessage(hwnd, User32.WM_KEYUP, (IntPtr)ev.VirtualKey, IntPtr.Zero);
				break;
			case MacroEventType.Char:
				User32.PostMessage(hwnd, User32.WM_CHAR, (IntPtr)ev.CharCode, IntPtr.Zero);
				break;
			case MacroEventType.MouseMove:
				PostMouse(hwnd, User32.WM_MOUSEMOVE, ev.MouseX, ev.MouseY, 0);
				break;
			case MacroEventType.MouseDown:
				PostMouse(hwnd, User32.WM_LBUTTONDOWN, ev.MouseX, ev.MouseY, User32.MK_LBUTTON);
				break;
			case MacroEventType.MouseUp:
				PostMouse(hwnd, User32.WM_LBUTTONUP, ev.MouseX, ev.MouseY, 0);
				break;
			case MacroEventType.MouseWheel:
				User32.PostMessage(hwnd, User32.WM_MOUSEWHEEL, (IntPtr)((ev.MouseDelta & 0xFFFF) << 16), IntPtr.Zero);
				break;
		}
	}

	private static void PostMouse(IntPtr hwnd, int msg, int screenX, int screenY, int wParam)
	{
		var pt = new User32.POINT { X = screenX, Y = screenY };
		User32.ScreenToClient(hwnd, ref pt);
		int lParam = (pt.Y << 16) | (pt.X & 0xFFFF);
		User32.PostMessage(hwnd, (uint)msg, (IntPtr)wParam, (IntPtr)lParam);
	}
}
