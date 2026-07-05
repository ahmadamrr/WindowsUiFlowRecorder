namespace AutomationTestHarness;

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

public partial class MainWindow : Window
{
    private readonly Random _random = new();
    private int _generatedCount;
    private DispatcherTimer? _autoConnectTimer;

    public MainWindow()
    {
        InitializeComponent();
        ParseCommandLineArgs();
    }

    private void ParseCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--instance":
                    if (i + 1 < args.Length)
                        InstanceLabel.Text = $"Instance: {args[++i]}";
                    break;
                case "--status":
                    if (i + 1 < args.Length)
                        SetStatus(args[++i]);
                    break;
                case "--title":
                    if (i + 1 < args.Length)
                        TitleText.Text = args[++i];
                    break;
                case "--generate":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var count))
                        Dispatcher.InvokeAsync(() => GenerateControls(count));
                    break;
                case "--auto-connect":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var seconds))
                        StartAutoConnectTimer(seconds);
                    break;
                case "--help":
                case "-?":
                    ShowHelp();
                    break;
            }
        }
    }

    private void ShowHelp()
    {
        ArgsLabel.Text = "Args: --instance <name> --status <text> --title <text> --generate <N> --auto-connect <seconds>";
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("Connected");
        TitleText.Text = $"{TitleText.Text} [Connected]";
    }

    private void CrashButton_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(1);
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(ControlCountBox.Text, out var count) && count > 0 && count <= 5000)
            GenerateControls(count);
        else
            MessageBox.Show("Enter a number between 1 and 5000", "Invalid Input");
    }

    private void MutateButton_Click(object sender, RoutedEventArgs e)
    {
        GeneratedControlsPanel.Items.Clear();
        _generatedCount = 0;
        SetStatus("Mutated");
    }

    private void AutoConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_autoConnectTimer?.IsEnabled == true)
        {
            _autoConnectTimer.Stop();
            AutoConnectTimerBtn.Content = "Toggle Auto-Connect (5s)";
        }
        else
        {
            StartAutoConnectTimer(5);
            AutoConnectTimerBtn.Content = "Stop Auto-Connect";
        }
    }

    private void StartAutoConnectTimer(int seconds)
    {
        _autoConnectTimer?.Stop();
        _autoConnectTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        _autoConnectTimer.Tick += (_, _) =>
        {
            SetStatus("Connected");
            _autoConnectTimer.Stop();
        };
        _autoConnectTimer.Start();
    }

    public void SetStatus(string status)
    {
        StatusLabel.Text = status;
        StatusLabel.Foreground = status == "Connected"
            ? new SolidColorBrush(Color.FromRgb(0, 0x80, 0))
            : new SolidColorBrush(Color.FromRgb(0xCC, 0, 0));
    }

    private void GenerateControls(int count)
    {
        GeneratedControlsPanel.Items.Clear();
        _generatedCount = 0;

        for (int i = 0; i < count; i++)
        {
            var btn = new Button
            {
                Content = $"Ctrl_{i + 1}",
                Width = 70,
                Height = 28,
                Margin = new Thickness(2),
                Tag = $"GeneratedControl_{i}",
            };
            AutomationProperties.SetAutomationId(btn, $"btnGenerated_{i}");
            GeneratedControlsPanel.Items.Add(btn);
            _generatedCount++;
        }
    }
}