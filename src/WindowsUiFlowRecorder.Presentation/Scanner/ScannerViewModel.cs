namespace WindowsUiFlowRecorder.Presentation.Scanner;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Application.Scanning;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Presentation.Shared;

public class ScannerViewModel : ViewModelBase
{
    private readonly IUiScanService _scanService;
    private readonly IExportService _exportService;
    private readonly ILogger<ScannerViewModel> _logger;

    private ProcessItem? _selectedProcess;
    private string _searchText = string.Empty;
    private string _statusMessage = "Select a running application and click Scan";
    private string _elementDetails = string.Empty;
    private bool _isScanning;
    private bool _hasScanResult;
    private bool _hasSelection;
    private BoundingRectangle? _highlightBounds;
    private WindowSnapshot? _lastScan;

    public ObservableCollection<ProcessItem> Processes { get; } = [];
    public ObservableCollection<ElementTreeNode> TreeRoots { get; } = [];

    public ProcessItem? SelectedProcess
    {
        get => _selectedProcess;
        set { SetProperty(ref _selectedProcess, value); RefreshCommands(); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            ApplyFilter();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ElementDetails
    {
        get => _elementDetails;
        private set => SetProperty(ref _elementDetails, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetProperty(ref _isScanning, value);
    }

    public bool HasScanResult
    {
        get => _hasScanResult;
        private set => SetProperty(ref _hasScanResult, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    public BoundingRectangle? HighlightBounds
    {
        get => _highlightBounds;
        set => SetProperty(ref _highlightBounds, value);
    }

    public AsyncRelayCommand RefreshProcessesCommand { get; }
    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public RelayCommand SelectElementCommand { get; }

    public ScannerViewModel(
        IUiScanService scanService,
        IExportService exportService,
        ILogger<ScannerViewModel> logger)
    {
        _scanService = scanService;
        _exportService = exportService;
        _logger = logger;

        RefreshProcessesCommand = new AsyncRelayCommand(OnRefreshProcessesAsync);
        ScanCommand = new AsyncRelayCommand(OnScanAsync, () => SelectedProcess != null && !IsScanning);
        ExportCommand = new AsyncRelayCommand(OnExportAsync, () => HasScanResult && !IsScanning);
        SelectElementCommand = new RelayCommand(OnSelectElement);
    }

    public async Task InitializeAsync()
    {
        await OnRefreshProcessesAsync();
    }

    private async Task OnRefreshProcessesAsync()
    {
        Processes.Clear();
        StatusMessage = "Enumerating processes...";

        try
        {
            var processes = Process.GetProcesses()
                .Where(p =>
                {
                    try { return p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle); }
                    catch { return false; }
                })
                .OrderBy(p =>
                {
                    try { return p.ProcessName; }
                    catch { return ""; }
                })
                .ToList();

            foreach (var p in processes)
            {
                try
                {
                    Processes.Add(new ProcessItem(
                        p.Id,
                        p.ProcessName,
                        p.MainWindowTitle,
                        p.MainWindowHandle));
                }
                catch { }
            }

            StatusMessage = $"{Processes.Count} running processes with windows found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate processes");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task OnScanAsync()
    {
        if (SelectedProcess == null) return;

        IsScanning = true;
        HasScanResult = false;
        TreeRoots.Clear();
        ElementDetails = string.Empty;
        HasSelection = false;
        HighlightBounds = null;
        StatusMessage = $"Scanning {SelectedProcess.Name}...";

        try
        {
            var result = await _scanService.ScanWindowAsync(
                SelectedProcess.WindowHandle, 5000, CancellationToken.None);

            if (!result.IsSuccess)
            {
                StatusMessage = $"Scan failed: {result.ErrorMessage}";
                return;
            }

            _lastScan = result.Value;

            var rootNode = new ElementTreeNode(_lastScan.RootElement, 0, null);
            rootNode.IsExpanded = true;
            TreeRoots.Add(rootNode);

            HasScanResult = true;
            StatusMessage = $"Scan complete — {CountNodes(_lastScan.RootElement)} elements found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            RefreshCommands();
        }
    }

    private async Task OnExportAsync()
    {
        if (_lastScan == null) return;

        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Export Destination for Scan",
                DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dialog.ShowDialog() != true)
                return;

            var exportDir = dialog.FolderName;
            if (string.IsNullOrWhiteSpace(exportDir))
                return;

            var result = await _exportService.ExportStandaloneScanAsync(_lastScan, exportDir, CancellationToken.None);

            if (result.IsSuccess)
                StatusMessage = $"Scan exported to {exportDir}";
            else
                StatusMessage = $"Export failed: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            StatusMessage = $"Export error: {ex.Message}";
        }
    }

    private void OnSelectElement(object? parameter)
    {
        if (parameter is not ElementTreeNode node) return;

        HasSelection = true;
        HighlightBounds = node.Element.BoundingRectangle;

        var el = node.Element;
        ElementDetails =
            $"AutomationId: {el.AutomationId ?? "(none)"}\n" +
            $"Name: {el.Name ?? "(none)"}\n" +
            $"ControlType: {el.ControlType}\n" +
            $"ClassName: {el.ClassName ?? "(none)"}\n" +
            $"LocalizedControlType: {el.LocalizedControlType ?? "(none)"}\n" +
            $"HelpText: {el.HelpText ?? "(none)"}\n" +
            $"Value/Text: {el.ValueOrText ?? "(none)"}\n" +
            $"BoundingRect: ({el.BoundingRectangle.X}, {el.BoundingRectangle.Y}) " +
            $"{el.BoundingRectangle.Width}x{el.BoundingRectangle.Height}\n" +
            $"Enabled: {el.IsEnabled}\n" +
            $"Offscreen: {el.IsOffscreen}\n" +
            $"KeyboardFocusable: {el.IsKeyboardFocusable}\n" +
            $"Depth: {el.DepthInTree}\n" +
            $"Patterns: {(el.SupportedPatterns.Count > 0 ? string.Join(", ", el.SupportedPatterns) : "(none)")}\n" +
            $"Children: {el.Children.Count}";
    }

    private void ApplyFilter()
    {
        foreach (var root in TreeRoots)
            root.ApplyFilter(SearchText);
    }

    private static int CountNodes(ElementInfo element)
    {
        var count = 1;
        foreach (var child in element.Children)
            count += CountNodes(child);
        return count;
    }

    private void RefreshCommands()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

public record ProcessItem(
    int Id,
    string Name,
    string WindowTitle,
    IntPtr WindowHandle
)
{
    public string DisplayText => $"{Name} (PID {Id}) — {WindowTitle}";
}