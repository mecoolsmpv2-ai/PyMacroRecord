using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TinyTaskPlus.Core;
#if WINDOWS
using TinyTaskPlus.Win;
#endif

namespace TinyTaskPlus.App;

public partial class AssignWindowDialog : Window
{
	private readonly ObservableCollection<WindowInfo> _all = new();
	private readonly ObservableCollection<WindowInfo> _filtered = new();
#if WINDOWS
	private readonly IWindowAssignmentService _service = new WindowsWindowAssignmentService();
#endif
	public AssignWindowDialog()
	{
		InitializeComponent();
		WindowsList.ItemsSource = _filtered;
		FilterBox.TextChanged += (_, __) => ApplyFilter();
		OkButton.Click += (_, __) => Close(WindowsList.SelectedItem as WindowInfo);
		CancelButton.Click += (_, __) => Close(null);
		RefreshButton.Click += async (_, __) => await LoadAsync();
		this.Opened += async (_, __) => await LoadAsync();
	}

	private async Task LoadAsync()
	{
		_all.Clear();
		_filtered.Clear();
#if WINDOWS
		var items = await _service.GetOpenWindowsAsync();
		foreach (var w in items.OrderByDescending(w => string.IsNullOrWhiteSpace(w.Title) ? 0 : w.Title.Length))
		{
			_all.Add(w);
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
		foreach (var w in _all)
		{
			if (string.IsNullOrEmpty(query) ||
				(w.Title?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
				(w.ProcessName?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
				(w.ClassName?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false))
			{
				_filtered.Add(w);
			}
		}
	}
}