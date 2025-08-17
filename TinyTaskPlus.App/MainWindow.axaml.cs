using Avalonia.Controls;
using System.Collections.ObjectModel;
using TinyTaskPlus.Core;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using Avalonia.Threading;
#if WINDOWS
using TinyTaskPlus.Win;
using System.Runtime.InteropServices;
#endif

namespace TinyTaskPlus.App;

public partial class MainWindow : Window
{
	private readonly ObservableCollection<Macro> _macros = new();
	private readonly IMacroStorage _storage = new JsonMacroStorage();
#if WINDOWS
	private IMacroRecorder? _recorder;
	private readonly IMacroPlayer _player = new WindowsPostMessageMacroPlayer();
	private IntPtr _hotkeyHook;
	private User32.HookProc? _hotkeyProc;
#endif
	private CancellationTokenSource? _playCts;
	private const string StorePath = "macros.json";

	public MainWindow()
	{
		InitializeComponent();
		MacroList.ItemsSource = _macros;
		RecordButton.Click += async (_, __) => await StartRecordAsync();
		StopButton.Click += async (_, __) => await StopRecordAsync();
		PlayButton.Click += async (_, __) => await PlayAsync();
		AssignButton.Click += async (_, __) => await AssignAsync();
#if WINDOWS
		HotkeyToggle.IsCheckedChanged += (_, __) => ToggleHotkey(HotkeyToggle.IsChecked == true);
#endif
		this.Opened += async (_, __) => await LoadAsync();
		this.Closing += async (_, __) => await CleanupAsync();
	}

	private async Task LoadAsync()
	{
		var loaded = await _storage.LoadAsync(StorePath);
		foreach (var m in loaded) _macros.Add(m);
	}

	private async Task CleanupAsync()
	{
#if WINDOWS
		if (_hotkeyHook != IntPtr.Zero)
		{
			User32.UnhookWindowsHookEx(_hotkeyHook);
			_hotkeyHook = IntPtr.Zero;
		}
#endif
		await Task.CompletedTask;
	}

	private async Task SaveAsync()
	{
		await _storage.SaveAsync(_macros, StorePath);
	}

	private async Task StartRecordAsync()
	{
#if WINDOWS
		if (_recorder == null) _recorder = new WindowsMacroRecorder();
		await _recorder.StartAsync();
#else
		await Task.CompletedTask;
#endif
	}

	private async Task StopRecordAsync()
	{
#if WINDOWS
		if (_recorder is null) return;
		var macro = await _recorder.StopAsync($"Macro {_macros.Count + 1}");
		_macros.Add(macro);
		await SaveAsync();
#endif
	}

	private async Task PlayAsync()
	{
		if (MacroList.SelectedItem is not Macro macro) return;
#if WINDOWS
		_playCts?.Cancel();
		_playCts = new CancellationTokenSource();
		await _player.PlayAsync(macro, _playCts.Token);
#endif
	}

	private async Task AssignAsync()
	{
		if (MacroList.SelectedItem is not Macro macro) return;
#if WINDOWS
		var dialog = new AssignWindowDialog();
		var selected = await dialog.ShowDialog<WindowInfo?>(this);
		if (selected != null)
		{
			macro.Assignment = new MacroAssignment
			{
				TopLevelHwndHex = selected.HandleHex,
				ProcessId = selected.ProcessId,
				ProcessPath = selected.ProcessPath,
				WindowTitle = selected.Title,
			};
			await SaveAsync();
		}
#endif
	}

#if WINDOWS
	private void ToggleHotkey(bool enable)
	{
		if (enable && _hotkeyHook == IntPtr.Zero)
		{
			_hotkeyProc = HotkeyProc;
			var hInstance = User32.GetModuleHandle(System.Diagnostics.Process.GetCurrentProcess().MainModule!.ModuleName!);
			_hotkeyHook = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _hotkeyProc!, hInstance, 0);
		}
		else if (!enable && _hotkeyHook != IntPtr.Zero)
		{
			User32.UnhookWindowsHookEx(_hotkeyHook);
			_hotkeyHook = IntPtr.Zero;
		}
	}

	private IntPtr HotkeyProc(int code, IntPtr wParam, IntPtr lParam)
	{
		if (code >= 0)
		{
			var k = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
			bool ctrl = (User32.GetAsyncKeyState(User32.VK_CONTROL) & 0x8000) != 0;
			bool alt = (User32.GetAsyncKeyState(User32.VK_MENU) & 0x8000) != 0;
			if (ctrl && alt && k.vkCode == 120) // F9 = 120
			{
				_ = Dispatcher.UIThread.InvokeAsync(async () => await PlayAsync());
			}
		}
		return User32.CallNextHookEx(_hotkeyHook, code, wParam, lParam);
	}
#endif
}