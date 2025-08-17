using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TinyTaskPlus.Core;
#if WINDOWS
using TinyTaskPlus.Win;
#endif

namespace TinyTaskPlus.App;

public partial class ProcessPickerDialog : Window
{
	private readonly ObservableCollection<ProcessInfo> _all = new();
	private readonly ObservableCollection<ProcessInfo> _filtered = new();
#if WINDOWS
	private readonly IProcessService _service = new WindowsProcessService();
#endif

	public ProcessPickerDialog()
	{
		InitializeComponent();
		ProcessList.ItemsSource = _filtered;
		FilterBox.TextChanged += (_, __) => ApplyFilter();
		OkButton.Click += (_, __) => Close(ProcessList.SelectedItem as ProcessInfo);
		CancelButton.Click += (_, __) => Close(null);
		RefreshButton.Click += async (_, __) => await LoadAsync();
		this.Opened += async (_, __) => await LoadAsync();
	}

	private async Task LoadAsync()
	{
		_all.Clear();
		_filtered.Clear();
#if WINDOWS
		var items = await _service.GetRunningProcessesAsync();
		foreach (var p in items)
		{
			_all.Add(p);
		}
		ApplyFilter();
#else
		await Task.CompletedTask;
#endif
	}

	private void ApplyFilter()
	{
		var query = FilterBox.Text ?? string.Empty;
		_filtered.Clear();
		foreach (var p in _all)
		{
			if (string.IsNullOrEmpty(query) ||
				(p.ProcessName?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
				(p.MainWindowTitle?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
				(p.FileName?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false))
			{
				_filtered.Add(p);
			}
		}
	}
}