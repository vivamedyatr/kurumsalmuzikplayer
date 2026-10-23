using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
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
    static readonly CultureInfo Turkish = new("tr-TR");
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
            case "login-result":
                Dispatcher.BeginInvoke(new Action(() => OnLoginResult(m)));
                break;
            case "close":
                _link.Close();
                Dispatcher.BeginInvoke(new Action(Close));
                break;
        }
    }

    void ApplyState(JObject s)
    {
        var screen = (string?)s["screen"] ?? "player";
        var notice = (string?)s["notice"];
        ShowScreen(screen);
        var account = (string?)s["account"];
        var branch = (string?)s["branch"];
        BranchText.Text = account != null ? $"{account} · {branch}".ToUpper(Turkish) : "";
        BranchText.ToolTip = (string?)s["server"];
        LiveText.Text = screen switch
        {
            "login" => "GİRİŞ BEKLENİYOR",
            "expired" => "YAYIN DURDU",
            _ => (bool?)s["online"] == false ? "YAYINDA · İNTERNETSİZ" : "YAYINDA",
        };
        LiveDot.Fill = screen == "player" ? (Brush)FindResource("Red") : Brushes.Gray;
        LoginNotice.Text = notice ?? "";
        LoginNotice.Visibility = screen == "login" && notice != null ? Visibility.Visible : Visibility.Collapsed;
        NoticeText.Text = notice ?? "";
        NoticeText.Visibility = screen == "player" && notice != null ? Visibility.Visible : Visibility.Collapsed;
        ExpiredText.Text = notice ?? "Hizmet süreniz doldu. Uzatmak için Viva Medya ile iletişime geçin.";

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

    void ShowScreen(string screen)
    {
        var wasLogin = LoginView.Visibility == Visibility.Visible;
        PlayerView.Visibility = screen == "player" ? Visibility.Visible : Visibility.Collapsed;
        LoginView.Visibility = screen == "login" ? Visibility.Visible : Visibility.Collapsed;
        ExpiredView.Visibility = screen == "expired" ? Visibility.Visible : Visibility.Collapsed;
        if (screen == "login" && !wasLogin)
        {
            LoginButton.IsEnabled = true;
            (UserBox.Text.Length == 0 ? (IInputElement)UserBox : PassBox).Focus();
        }
    }

    void Login_Click(object sender, RoutedEventArgs e)
    {
        var user = UserBox.Text.Trim();
        var branch = BranchBox.Text.Trim();
        if (user.Length == 0 || PassBox.Password.Length == 0 || branch.Length < 2)
        {
            LoginError.Text = "Kullanıcı adı, şifre ve şube adını (en az 2 harf) yazın.";
            return;
        }
        LoginError.Text = "Giriş yapılıyor…";
        LoginButton.IsEnabled = false;
        _link.Send(new JObject { ["t"] = "login", ["username"] = user, ["password"] = PassBox.Password, ["branch"] = branch });
        // Motor cevap vermezse düğme kilitli kalmasın.
        var guard = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        guard.Tick += (_, _) =>
        {
            guard.Stop();
            if (LoginButton.IsEnabled) return;
            LoginButton.IsEnabled = true;
            LoginError.Text = "Cevap alınamadı. Tekrar deneyin.";
        };
        guard.Start();
    }

    void OnLoginResult(JObject r)
    {
        LoginButton.IsEnabled = true;
        if ((bool?)r["ok"] == true)
        {
            PassBox.Clear();
            LoginError.Text = "";
            return;
        }
        LoginError.Text = (string?)r["error"] ?? "Giriş yapılamadı.";
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
        if (connected) return;
        StatusText.Text = "Motora bağlanılıyor…";
        ShowScreen("player");
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
