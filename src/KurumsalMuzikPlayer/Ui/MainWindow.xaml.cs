using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Kmp.Core;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>
/// Pencere sadece gösterir ve ses/sonraki komutunu motora iletir (BRIEF §4). Motorun "ping"ine arayüz iş
/// parçacığından cevap verir: cevap gelmezse motor pencereyi donmuş sayar.
/// </summary>
public partial class MainWindow : Window
{
    readonly EngineLink _link = new();
    readonly DispatcherTimer _clock;
    bool _applyingState;
    DateTime _lastUserVolume = DateTime.MinValue;
    double _pos;
    double _dur;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"{AppPaths.ProductName} {AppInfo.Version}";
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => Clock.Text = DateTime.Now.ToString("HH:mm");
        _clock.Start();
        Clock.Text = DateTime.Now.ToString("HH:mm");
        ProgressTrack.SizeChanged += (_, _) => DrawProgress();

        _link.MessageReceived += OnMessage;
        _link.ConnectionChanged += connected => Dispatcher.BeginInvoke(new Action(() => ShowConnection(connected)));
        _link.Start();
        ShowConnection(false);
        Closed += (_, _) => _link.Close();
    }

    void OnMessage(JObject m)
    {
        switch ((string?)m["t"])
        {
            case "ping":
                var n = (int?)m["n"] ?? 0;
                Dispatcher.BeginInvoke(new Action(() =>
                    _link.Send(new JObject { ["t"] = "pong", ["n"] = n, ["min"] = WindowState == WindowState.Minimized })));
                break;
            case "state":
                Dispatcher.BeginInvoke(new Action(() => ApplyState(m)));
                break;
            case "show":
                Dispatcher.BeginInvoke(new Action(BringToFront));
                break;
            case "close":
                _link.Close();
                Dispatcher.BeginInvoke(new Action(Close));
                break;
        }
    }

    void ApplyState(JObject s)
    {
        TitleText.Text = (string?)s["title"] ?? "—";
        SubText.Text = (string?)s["sub"] ?? "";
        StatusText.Text = (string?)s["status"] ?? "";
        var exp = (string?)s["exp"];
        ExperimentText.Visibility = exp != null ? Visibility.Visible : Visibility.Collapsed;
        ExperimentText.Text = exp != null ? "Dayanıklılık deneyi · " + exp : "";
        _pos = (double?)s["pos"] ?? 0;
        _dur = (double?)s["dur"] ?? 0;
        TimesText.Text = $"{Format(_pos)} / {Format(_dur)}";
        DrawProgress();
        var vol = (int?)s["vol"] ?? 0;
        if (DateTime.Now - _lastUserVolume > TimeSpan.FromSeconds(2) && Math.Abs(VolumeSlider.Value - vol) > 0.5)
        {
            _applyingState = true;
            VolumeSlider.Value = vol;
            _applyingState = false;
        }
        VolumeText.Text = ((int)VolumeSlider.Value).ToString();
    }

    void DrawProgress()
    {
        var ratio = _dur > 0 ? Math.Max(0, Math.Min(1, _pos / _dur)) : 0;
        ProgressFill.Width = ProgressTrack.ActualWidth * ratio;
    }

    void ShowConnection(bool connected)
    {
        LiveDot.Fill = connected ? (Brush)FindResource("Red") : Brushes.Gray;
        LiveText.Text = connected ? "YAYINDA" : "MOTOR BAĞLANTISI YOK";
        NextButton.IsEnabled = connected;
        if (!connected) StatusText.Text = "Motora bağlanılıyor…";
    }

    void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;
        Topmost = false;
    }

    static string Format(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds)) return "0:00";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText != null) VolumeText.Text = ((int)e.NewValue).ToString();
        if (_applyingState || !IsLoaded) return;
        _lastUserVolume = DateTime.Now;
        _link.Send(new JObject { ["t"] = "volume", ["v"] = (int)e.NewValue });
    }

    void Next_Click(object sender, RoutedEventArgs e) => _link.Send(new JObject { ["t"] = "next" });

    void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(e.Uri.AbsoluteUri)?.Dispose(); }
        catch (Exception ex) { Log.Warn($"Bağlantı açılamadı: {ex.Message}"); }
        e.Handled = true;
    }
}
