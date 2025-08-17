using Avalonia.Controls;
using System.Collections.ObjectModel;
using TinyTaskPlus.Core;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS
using TinyTaskPlus.Win;
#endif

namespace TinyTaskPlus.App;

public partial class MainWindow : Window
{
	private readonly ObservableCollection<Macro> _macros = new();
	private readonly IMacroStorage _storage = new JsonMacroStorage();
#if WINDOWS
	private IMacroRecorder? _recorder;
	private readonly IMacroPlayer _player = new WindowsPostMessageMacroPlayer();
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
		this.Opened += async (_, __) => await LoadAsync();
	}

	private async Task LoadAsync()
	{
		var loaded = await _storage.LoadAsync(StorePath);
		foreach (var m in loaded) _macros.Add(m);
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
}