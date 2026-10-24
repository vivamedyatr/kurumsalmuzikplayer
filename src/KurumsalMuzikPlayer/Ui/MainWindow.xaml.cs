using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
    string _screen = "player";
    string? _coverPath;
    string _bandKey = "";
    int _bandStartMinute;
    int _minute = -1;
    string _historyKey = "";
    string _annKey = "";

    public sealed class HistoryRow
    {
        public string At { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Title { get; set; } = "";
        public string Sub { get; set; } = "";
    }

    public sealed class PlanRow
    {
        public string Label { get; set; } = "";
        public List<string> Items { get; set; } = new();
    }

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"{AppPaths.ProductName} {AppInfo.Version}";
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => Clock.Text = DateTime.Now.ToString("HH:mm");
        _clock.Start();
        Clock.Text = DateTime.Now.ToString("HH:mm");
        ProgressTrack.SizeChanged += (_, _) => DrawProgress();
        DayBand.SizeChanged += (_, _) => DrawNowLine();

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

    // ---------- durum ----------

    void ApplyState(JObject s)
    {
        var screen = (string?)s["screen"] ?? "player";
        var notice = (string?)s["notice"];
        ShowScreen(screen);
        var account = (string?)s["account"];
        var branch = (string?)s["branch"];
        BranchText.Text = account != null ? $"{account} · {branch}" : "";
        BranchText.ToolTip = (string?)s["server"];
        var gate = (string?)s["gate"];
        var online = (bool?)s["online"] != false;
        LiveText.Text = screen switch
        {
            "login" => "GİRİŞ BEKLENİYOR",
            "expired" => "YAYIN DURDU",
            _ when gate != null => "YAYIN DURDU",
            _ => online ? "YAYINDA" : "YAYINDA · İNTERNETSİZ",
        };
        LiveDot.Fill = screen == "player" && gate == null ? (Brush)FindResource("Red") : Brushes.Gray;
        LoginNotice.Text = notice ?? "";
        LoginNotice.Visibility = screen == "login" && notice != null ? Visibility.Visible : Visibility.Collapsed;
        NoticeText.Text = notice ?? "";
        NoticeText.Visibility = screen == "player" && notice != null ? Visibility.Visible : Visibility.Collapsed;
        ExpiredText.Text = notice ?? "Hizmet süreniz doldu. Uzatmak için Viva Medya ile iletişime geçin.";
        if (screen != "player") return;

        ApplyNowPlaying(s);
        _updateReady = (string?)s["updateReady"];
        ApplyLibrary(s["library"] as JObject);
        var days = (int?)s["offlineDaysLeft"] ?? 0;
        var status = (string?)s["status"] ?? "";
        StatusText.Text = !online && gate == null ? $"{status} · İnternet yok: {days} gün daha çalar" : status;
        StatusText.Foreground = gate != null || !online ? (Brush)FindResource("Warn") : (Brush)FindResource("Muted");
        var exp = (string?)s["exp"];
        ExperimentText.Visibility = exp != null ? Visibility.Visible : Visibility.Collapsed;
        ExperimentText.Text = exp != null ? "Dayanıklılık deneyi · " + exp : "";
        if (s["slots"] is JArray slots) BuildBand(slots);
        _minute = (int?)s["minute"] ?? -1;
        DrawNowLine();
        if (s["history"] is JArray history) ApplyHistory(history);
        ApplySchedule(s["schedule"] as JObject);
    }

    void ApplyNowPlaying(JObject s)
    {
        var breakTitle = (string?)s["breakTitle"];
        var colors = s["genreColors"] as JObject;
        // Tarz adları çoğunlukla İngilizce: Türkçe büyük harf "HİGH" yapıyordu; olduğu gibi yazılır.
        GenreText.Text = breakTitle != null ? "Anons" : (string?)s["genre"] ?? "";
        VinylLabel.Fill = ParseBrush((string?)colors?["from"]) ?? (Brush)FindResource("Red");
        TitleText.Text = breakTitle ?? (string?)s["title"] ?? "—";
        SubText.Text = breakTitle != null ? "Müzik anons bitince kaldığı yerden sürer" : (string?)s["sub"] ?? "";
        SetCover(breakTitle == null ? (string?)s["cover"] : null);

        _pos = breakTitle != null ? 0 : (double?)s["pos"] ?? 0;
        _dur = breakTitle != null ? 0 : (double?)s["dur"] ?? 0;
        TimesText.Text = $"{Format(_pos)} / {Format(_dur)}";
        DrawProgress();

        var wait = (int?)s["skipWaitMs"] ?? 0;
        NextButton.IsEnabled = breakTitle == null && wait == 0 && (string?)s["gate"] == null;
        NextButton.Content = wait > 0 ? $"Sonraki ({(wait + 999) / 1000})" : "Sonraki ›";

        var vol = (int?)s["vol"] ?? 0;
        if (DateTime.Now - _lastUserVolume > TimeSpan.FromSeconds(2) && Math.Abs(VolumeSlider.Value - vol) > 0.5)
        {
            _applyingState = true;
            VolumeSlider.Value = vol;
            _applyingState = false;
        }
        VolumeText.Text = ((int)VolumeSlider.Value).ToString();
    }

    void SetCover(string? path)
    {
        if (path == _coverPath) return;
        _coverPath = path;
        if (path == null || !File.Exists(path))
        {
            CoverBox.Visibility = Visibility.Collapsed;
            CoverImage.Source = null;
            return;
        }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // dosyayı kilitlemesin
            bmp.DecodePixelWidth = 280;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            CoverImage.Source = bmp;
            CoverBox.Visibility = Visibility.Visible;
        }
        catch (Exception e)
        {
            Log.Warn("Kapak açılamadı: " + e.Message);
            CoverBox.Visibility = Visibility.Collapsed;
        }
    }

    void ApplyLibrary(JObject? lib)
    {
        LibraryText.Visibility = lib != null ? Visibility.Visible : Visibility.Collapsed;
        if (lib == null) return;
        var total = (int?)lib["total"] ?? 0;
        var done = (int?)lib["done"] ?? 0;
        var percent = total > 0 ? 100 * done / total : 0;
        var message = (string?)lib["message"];
        LibraryText.Text = (string?)lib["state"] switch
        {
            "ready" => $"Arşiv hazır · son kontrol {(string?)lib["checkedAt"] ?? "—"}",
            "downloading" => $"Yeni müzikler iniyor %{percent}; yayın etkilenmez",
            "starting" => "Arşiv açılıyor…",
            _ => $"Arşiv %{percent} · {message}",
        };
        LibraryText.Foreground = (string?)lib["state"] is "offline" or "disk-full" ? (Brush)FindResource("Warn") : (Brush)FindResource("Muted");
        var updates = (bool?)lib["updates"] ?? true;
        if (DateTime.Now - _lastUpdatesClick > TimeSpan.FromSeconds(3)) UpdatesBox.IsChecked = updates;
        var next = (string?)lib["nextUpdate"];
        UpdatesText.Text = updates ? (next != null ? $"Sıradaki yeni müzik: {next}" : "") : "Albüm listesi sabit (aylık yeni müzik kapalı)";
        if (_updateReady != null) UpdatesText.Text = $"Yeni sürüm hazır ({_updateReady}); program yeniden açılınca kurulur";
    }

    // ---------- gün bandı ----------

    void BuildBand(JArray slots)
    {
        var key = slots.ToString(Newtonsoft.Json.Formatting.None);
        if (key == _bandKey) return;
        _bandKey = key;
        DayBand.Children.Clear();
        DayBand.ColumnDefinitions.Clear();
        DayHours.Children.Clear();
        DayHours.ColumnDefinitions.Clear();
        var list = slots.OfType<JObject>().OrderBy(o => (int?)o["hour"] ?? 0).ToList();
        if (list.Count == 0) return;
        _bandStartMinute = ((int?)list[0]["hour"] ?? 0) * 60;
        for (var i = 0; i < list.Count; i++)
        {
            var from = (int?)list[i]["hour"] ?? 0;
            var to = i + 1 < list.Count ? (int?)list[i + 1]["hour"] ?? 24 : 24;
            if (to <= from) continue;
            DayBand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(to - from, GridUnitType.Star) });
            DayHours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(to - from, GridUnitType.Star) });
            var col = DayBand.ColumnDefinitions.Count - 1;
            var c1 = ParseColor((string?)list[i]["from"]) ?? Color.FromRgb(0x2A, 0x2A, 0x30);
            var c2 = ParseColor((string?)list[i]["to"]) ?? c1;
            var cell = new Border
            {
                Background = new LinearGradientBrush(c1, c2, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x0D)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                ToolTip = $"{from:00}:00–{to:00}:00 · {(string?)list[i]["name"]}",
                Child = new TextBlock
                {
                    Text = (string?)list[i]["name"] ?? "",
                    FontSize = 10,
                    Foreground = ParseBrush((string?)list[i]["ink"]) ?? Brushes.White,
                    Margin = new Thickness(5, 0, 3, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            Grid.SetColumn(cell, col);
            DayBand.Children.Add(cell);
            var label = new TextBlock { Text = $"{from:00}", FontSize = 9, FontFamily = new FontFamily("Consolas"), Foreground = (Brush)FindResource("Muted") };
            Grid.SetColumn(label, col);
            DayHours.Children.Add(label);
        }
    }

    void DrawNowLine()
    {
        var span = 1440 - _bandStartMinute;
        if (_minute < _bandStartMinute || span <= 0 || DayBand.ActualWidth <= 0)
        {
            NowLine.Visibility = Visibility.Collapsed;
            return;
        }
        NowLine.Visibility = Visibility.Visible;
        Canvas.SetLeft(NowLine, Math.Min(DayBand.ActualWidth - 2, DayBand.ActualWidth * (_minute - _bandStartMinute) / span));
    }

    // ---------- geçmiş ve anonslar ----------

    void ApplyHistory(JArray history)
    {
        var key = history.Count + "|" + (history.FirstOrDefault()?.ToString(Newtonsoft.Json.Formatting.None) ?? "");
        if (key == _historyKey) return;
        _historyKey = key;
        HistoryList.ItemsSource = history.OfType<JObject>().Select(h => new HistoryRow
        {
            At = (string?)h["at"] ?? "",
            Tag = (string?)h["kind"] == "anons" ? "ANONS  " : "",
            Title = (string?)h["title"] ?? "",
            Sub = (string?)h["sub"] ?? "",
        }).ToList();
    }

    void ApplySchedule(JObject? sch)
    {
        var key = sch?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
        if (key == _annKey) return;
        _annKey = key;
        if (sch == null)
        {
            AnnNext.Text = "Anons listesi bekleniyor";
            AnnPlans.ItemsSource = null;
            AnnUpcoming.Text = "";
            return;
        }
        var next = (string?)sch["next"];
        AnnNext.Text = next != null ? $"Sıradaki anons kuşağı {next}" : "Yayında anons yok";
        AnnPlans.ItemsSource = (sch["plans"] as JArray ?? new JArray()).OfType<JObject>().Select(p => new PlanRow
        {
            Label = ((string?)p["label"] ?? "").ToUpper(Turkish),
            Items = (p["items"] as JArray ?? new JArray()).OfType<JObject>().Select(i =>
                (string?)i["title"] + ((string?)i["ends"] is { } e ? $" · {e} tarihine kadar" : "") + ((bool?)i["ready"] == false ? " · iniyor" : "")).ToList(),
        }).ToList();
        var upcoming = (sch["upcoming"] as JArray ?? new JArray()).OfType<JObject>()
            .Select(u => $"{(string?)u["starts"]} · {(string?)u["title"]} ({(string?)u["plan"]})").ToList();
        AnnUpcoming.Text = upcoming.Count > 0 ? "Başlayacak: " + string.Join("; ", upcoming) : "";
    }

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        ShowScreen(_screen);
    }

    void ShowScreen(string screen)
    {
        var wasLogin = LoginView.Visibility == Visibility.Visible;
        _screen = screen;
        var player = screen == "player";
        Tabs.Visibility = player ? Visibility.Visible : Visibility.Collapsed;
        DayBandBox.Visibility = player ? Visibility.Visible : Visibility.Collapsed;
        PlayerView.Visibility = player && TabNow.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        HistoryView.Visibility = player && TabHistory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AnnView.Visibility = player && TabAnn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        LoginView.Visibility = screen == "login" ? Visibility.Visible : Visibility.Collapsed;
        ExpiredView.Visibility = screen == "expired" ? Visibility.Visible : Visibility.Collapsed;
        if (screen == "login" && !wasLogin)
        {
            LoginButton.IsEnabled = true;
            (UserBox.Text.Length == 0 ? (IInputElement)UserBox : PassBox).Focus();
        }
    }

    // ---------- giriş ----------

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

    // ---------- yardımcılar ----------

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

    static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return null; }
    }

    static Brush? ParseBrush(string? hex) => ParseColor(hex) is Color c ? new SolidColorBrush(c) : null;

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

    DateTime _lastUpdatesClick = DateTime.MinValue;
    string? _updateReady;

    void Updates_Click(object sender, RoutedEventArgs e)
    {
        _lastUpdatesClick = DateTime.Now;
        _link.Send(new JObject { ["t"] = "updates", ["on"] = UpdatesBox.IsChecked == true });
    }

    /// <summary>Akış programda düzenlenmez (karar 9 Eki 2026): web sitesinde açılır; program paketi erken yeniden sorar.</summary>
    void FlowEdit_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(ServerApi.BaseUrl + "/giris")?.Dispose(); }
        catch (Exception ex) { Log.Warn($"Tarayıcı açılamadı: {ex.Message}"); }
        _link.Send(new JObject { ["t"] = "flow-edit" });
    }

    void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(e.Uri.AbsoluteUri)?.Dispose(); }
        catch (Exception ex) { Log.Warn($"Bağlantı açılamadı: {ex.Message}"); }
        e.Handled = true;
    }
}
