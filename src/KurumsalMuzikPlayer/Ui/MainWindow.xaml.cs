using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kmp.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Ui;

/// <summary>
/// Pencere (tasarım: docs/tasarim/taslak-calar.png; web yayın ekranının dili). Sadece gösterir ve ses/sonraki/akış
/// komutlarını motora iletir (BRIEF §4). Motorun "ping"ine arayüz iş parçacığından cevap verir.
/// </summary>
public partial class MainWindow : Window
{
    static readonly CultureInfo Turkish = new("tr-TR");
    readonly EngineLink _link = new();
    bool _applyingState;
    DateTime _lastUserVolume = DateTime.MinValue;
    DateTime _lastUpdatesClick = DateTime.MinValue;
    double _pos;
    double _dur;
    string _page = "now";
    JObject? _state;
    string _stripKey = "";
    string _recentKey = "";
    string _pageKey = "";
    int _stripMinute;
    readonly List<(int From, int To)> _stripSlots = new();

    public MainWindow()
    {
        InitializeComponent();
        WordmarkSub.Text = Visuals.Tracked("MÜZİK · BY VIVA MEDYA");
        LoginSub.Text = Visuals.Tracked("MÜZİK · BY VIVA MEDYA");
        StoreEyebrow.Text = Visuals.Tracked("MAĞAZA");
        AnnCardEyebrow.Text = Visuals.Tracked("SIRADAKİ ANONS");
        RecentEyebrow.Text = Visuals.Tracked("SON ÇALANLAR");
        LoginEyebrow.Text = Visuals.Tracked("KURUMSAL MÜZİK PLAYER");
        LiveTagText.Text = Visuals.Tracked("CANLI");
        CoverGrain.Fill = Visuals.Grain;
        LoginGrain.Fill = Visuals.Grain;
        VersionText.Text = $"Sürüm {AppInfo.Version}";
        LoginVersion.Text = $"{AppPaths.ProductName} {AppInfo.Version}";
        DateText.Text = DateTime.Now.ToString("dddd, d MMMM", Turkish);

        // Boyut: içerik tasarım boyutunda, pencere oranı sabit ölçeklenir (WindowFrame).
        var scale = WindowPlacement.Restore(this);
        Scaler.Width = WindowFrame.DesignWidth * scale;
        Scaler.Height = WindowFrame.DesignHeight * scale;
        SourceInitialized += (_, _) => WindowFrame.Attach(this);
        ContentRendered += (_, _) =>
        {
            if (SizeToContent == SizeToContent.Manual) return;
            SizeToContent = SizeToContent.Manual;
            Scaler.Width = Scaler.Height = double.NaN;
        };
        var placement = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        placement.Tick += (_, _) =>
        {
            placement.Stop();
            WindowPlacement.Save(this, Scaler.ActualWidth / WindowFrame.DesignWidth);
        };
        LocationChanged += (_, _) => { placement.Stop(); placement.Start(); };
        SizeChanged += (_, _) => { placement.Stop(); placement.Start(); };
        Closing += (_, _) => WindowPlacement.Save(this, Scaler.ActualWidth / WindowFrame.DesignWidth);

        Strip.SizeChanged += (_, _) => DrawNowMark();
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

    // ===================== durum =====================

    void ApplyState(JObject s)
    {
        _state = s;
        var screen = (string?)s["screen"] ?? "player";
        ShowScreen(screen);
        var notice = (string?)s["notice"];
        LoginNotice.Text = notice ?? "";
        LoginNoticeBox.Visibility = screen == "login" && notice != null ? Visibility.Visible : Visibility.Collapsed;
        ExpiredText.Text = notice ?? "Hizmet süreniz doldu. Uzatmak için Viva Medya ile iletişime geçin.";
        if (screen != "player") return;

        DateText.Text = DateTime.Now.ToString("dddd, d MMMM", Turkish);
        ApplyStore(s);
        ApplyNow(s);
        ApplyStrip(s);
        ApplyCards(s);
        if (_page != "now") RenderPage(false);
    }

    void ApplyStore(JObject s)
    {
        AccountText.Text = (string?)s["account"] ?? "";
        BranchRun.Text = (string?)s["branch"] ?? "";
        CodeRun.Text = s["code"] is { Type: JTokenType.String } c ? " · " + (string?)c : "";
        var gate = (string?)s["gate"];
        var online = (bool?)s["online"] != false;
        var days = (int?)s["offlineDaysLeft"] ?? 0;
        if (gate != null) SetPill("Yayın durdu", (Brush)FindResource("Vm"));
        else if (online) SetPill($"Çevrimiçi · {(string?)s["checkedAt"] ?? "—"}", (Brush)FindResource("Ok"));
        else SetPill($"İnternetsiz · {days} gün daha", (Brush)FindResource("Warn"));

        var ends = (string?)s["serviceEnds"];
        ServiceChip.Visibility = ends != null ? Visibility.Visible : Visibility.Collapsed;
        ServiceText.Text = $"Hizmet {ends} tarihine kadar";

        var lib = s["library"] as JObject;
        var total = (int?)lib?["total"] ?? 0;
        var done = (int?)lib?["done"] ?? 0;
        var pct = total > 0 ? 100 * done / total : 0;
        var libState = (string?)lib?["state"];
        DownloadChip.Visibility = libState is "downloading" or "offline" or "disk-full" ? Visibility.Visible : Visibility.Collapsed;
        DownloadText.Text = libState switch
        {
            "downloading" => $"Yeni müzikler iniyor %{pct}",
            "disk-full" => "Diskte yer yok",
            _ => $"İndirme bekliyor %{pct}",
        };
        DownloadText.Foreground = libState == "downloading" ? (Brush)FindResource("Fg") : (Brush)FindResource("Warn");
        DownloadFill.Width = 46.0 * pct / 100;
        NavArchiveBadge.Text = libState == "downloading" ? $"%{pct}" : "";

        var notice = (string?)s["notice"];
        var update = (string?)s["updateReady"];
        var noticeText = notice ?? (update != null ? $"Yeni sürüm hazır ({update}); program yeniden açılınca kurulur" : null);
        NoticeChip.Visibility = noticeText != null ? Visibility.Visible : Visibility.Collapsed;
        NoticeText.Text = noticeText ?? "";
        NoticeText.ToolTip = noticeText;

        if (DateTime.Now - _lastUpdatesClick > TimeSpan.FromSeconds(3)) UpdatesBox.IsChecked = (bool?)lib?["updates"] ?? true;
    }

    void SetPill(string text, Brush color)
    {
        StatusPillText.Text = text;
        StatusPillText.Foreground = color;
        StatusDot.Fill = color;
        StatusPill.BorderBrush = color is SolidColorBrush sc ? new SolidColorBrush(Color.FromArgb(0x4D, sc.Color.R, sc.Color.G, sc.Color.B)) : color;
    }

    void ApplyNow(JObject s)
    {
        var breakTitle = (string?)s["breakTitle"];
        var gate = (string?)s["gate"];
        var genre = (string?)s["genre"];
        var colors = s["genreColors"] as JObject;
        var slotHour = (int?)s["slotHour"];
        var title = (string?)s["title"] ?? "—";
        var album = (string?)s["sub"] ?? "";
        var status = (string?)s["status"] ?? "";

        string eyebrow;
        if (gate != null) eyebrow = "YAYIN DURDU";
        else if (breakTitle != null) eyebrow = "ANONS ÇALIYOR";
        else if (genre == null) eyebrow = "YAYIN HAZIRLANIYOR";
        else eyebrow = $"ŞU AN ÇALIYOR · {slotHour:00}:00 DİLİMİ";
        LiveEyebrow.Text = Visuals.Tracked(eyebrow);
        var red = gate == null;
        LiveEyebrow.Foreground = red ? (Brush)FindResource("Vm") : (Brush)FindResource("Fg3");
        LiveDot.Fill = red ? (Brush)FindResource("Vm") : (Brush)FindResource("Fg3");
        LiveHalo.Visibility = red ? Visibility.Visible : Visibility.Collapsed;

        GenreTitle.Text = breakTitle != null ? "ANONS" : Visuals.Upper(genre ?? "Kurumsal Müzik");
        SongRun.Text = breakTitle ?? title;
        AlbumRun.Text = breakTitle != null ? " · müzik anons bitince kaldığı yerden sürer" : album.Length > 0 ? " · " + album : "";

        if (gate != null)
        {
            NextText.Text = gate;
            NextText.Foreground = (Brush)FindResource("Warn");
        }
        else if (status != "Çalıyor")
        {
            NextText.Text = status;
            NextText.Foreground = (Brush)FindResource("Warn");
        }
        else
        {
            NextText.Text = NextSlotText(s);
            NextText.Foreground = (Brush)FindResource("Fg2");
        }

        // Kapak: albüm kapağı; yoksa ya da açılamazsa tarzın renkli kapağı.
        var cover = breakTitle == null ? (string?)s["cover"] : null;
        var img = Visuals.Image(cover, 400);
        CoverImage.Source = img;
        CoverFallback.Background = Visuals.Gradient((string?)colors?["from"], (string?)colors?["to"]);
        CoverFallbackText.Text = img == null ? Visuals.Upper(breakTitle != null ? "Anons" : genre) : "";
        CoverFallbackText.Foreground = Visuals.Solid((string?)colors?["ink"]) ?? Brushes.White;
        Ambient.Source = Visuals.Ambient(cover);
        BarThumb.Source = Visuals.Image(cover, 96);
        BarThumbFallback.Background = Visuals.Gradient((string?)colors?["from"], (string?)colors?["to"]);

        BarTitle.Text = breakTitle != null ? "Anons: " + breakTitle : title;
        BarSub.Text = gate ?? (genre != null ? $"{genre} · {slotHour:00}:00 dilimi" : status);

        _pos = breakTitle != null ? 0 : (double?)s["pos"] ?? 0;
        _dur = breakTitle != null ? 0 : (double?)s["dur"] ?? 0;
        PosText.Text = Format(_pos);
        DurText.Text = Format(_dur);
        DrawProgress();

        var wait = (int?)s["skipWaitMs"] ?? 0;
        NextButton.IsEnabled = breakTitle == null && wait == 0 && gate == null && genre != null;
        SkipWait.Text = wait > 0 ? ((wait + 999) / 1000).ToString() : "";

        LiveTagText.Text = Visuals.Tracked(gate != null ? "DURDU" : breakTitle != null ? "ANONS" : "CANLI");
        var tagBrush = gate != null ? (Brush)FindResource("Fg3") : (Brush)FindResource("Vm");
        LiveTagText.Foreground = tagBrush;
        LiveTag.BorderBrush = tagBrush;
        OnAir.Fill = gate != null ? (Brush)FindResource("Bg3") : (Brush)FindResource("Vm");

        var vol = (int?)s["vol"] ?? 0;
        if (DateTime.Now - _lastUserVolume > TimeSpan.FromSeconds(2) && Math.Abs(VolumeSlider.Value - vol) > 0.5)
        {
            _applyingState = true;
            VolumeSlider.Value = vol;
            _applyingState = false;
        }
    }

    /// <summary>"Sıradaki: Deep House · 12:00" ya da son dilimde "… gece boyunca sürer".</summary>
    static string NextSlotText(JObject s)
    {
        var slots = (s["slots"] as JArray)?.OfType<JObject>().OrderBy(o => (int?)o["hour"] ?? 0).ToList() ?? new List<JObject>();
        if (slots.Count == 0) return "";
        var minute = (int?)s["minute"] ?? 0;
        var next = slots.FirstOrDefault(o => ((int?)o["hour"] ?? 0) * 60 > minute);
        if (next != null) return $"Sıradaki: {(string?)next["name"]} · {(int?)next["hour"]:00}:00";
        var first = slots[0];
        return $"{(string?)slots.Last()["name"]} gece boyunca sürer · akış {(int?)first["hour"]:00}:00'de baştan başlar";
    }

    // ===================== bugünkü akış şeridi =====================

    void ApplyStrip(JObject s)
    {
        var slots = (s["slots"] as JArray)?.OfType<JObject>().OrderBy(o => (int?)o["hour"] ?? 0).ToList() ?? new List<JObject>();
        var minute = (int?)s["minute"] ?? 0;
        var key = string.Join("|", slots.Select(o => o.ToString(Formatting.None))) + "#" + minute / 60;
        if (key != _stripKey)
        {
            _stripKey = key;
            Strip.Children.Clear();
            Strip.ColumnDefinitions.Clear();
            StripHours.Children.Clear();
            StripHours.ColumnDefinitions.Clear();
            _stripSlots.Clear();
            for (var i = 0; i < slots.Count; i++)
            {
                var from = (int?)slots[i]["hour"] ?? 0;
                var to = i + 1 < slots.Count ? (int?)slots[i + 1]["hour"] ?? 24 : 24;
                if (to <= from) continue;
                _stripSlots.Add((from, to));
                var col = Strip.ColumnDefinitions.Count;
                Strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(to - from, GridUnitType.Star) });
                StripHours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(to - from, GridUnitType.Star) });
                var past = minute >= to * 60;
                var tile = GenreTile((string?)slots[i]["name"], (string?)slots[i]["from"], (string?)slots[i]["to"], (string?)slots[i]["ink"], 11.5, past);
                tile.Margin = new Thickness(0, 0, 3, 0);
                tile.ToolTip = $"{from:00}:00–{to:00}:00 · {(string?)slots[i]["name"]}";
                Grid.SetColumn(tile, col);
                Strip.Children.Add(tile);
                var label = new TextBlock { Text = $"{from:00}", Style = (Style)FindResource("MonoText"), FontSize = 10 };
                Grid.SetColumn(label, col);
                StripHours.Children.Add(label);
            }
        }
        _stripMinute = minute;
        DrawNowMark();
    }

    void DrawNowMark()
    {
        StripMarkLayer.Children.Clear();
        StripLabelLayer.Children.Clear();
        if (_stripSlots.Count == 0 || Strip.ActualWidth <= 0) return;
        var start = _stripSlots[0].From * 60;
        if (_stripMinute < start) return; // 00–08: son dilim sürüyor, çizgi yok
        var x = Strip.ActualWidth * (_stripMinute - start) / (1440 - start);
        var line = new Rectangle { Width = 2, Height = 70, Fill = Brushes.White, RadiusX = 1, RadiusY = 1 };
        Canvas.SetLeft(line, Math.Min(Strip.ActualWidth - 2, x));
        Canvas.SetTop(line, -6);
        StripMarkLayer.Children.Add(line);
        var label = new Border
        {
            Background = (Brush)FindResource("Vm"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Child = new TextBlock
            {
                Text = $"{_stripMinute / 60:00}:{_stripMinute % 60:00}", FontFamily = (FontFamily)FindResource("Mono"),
                FontWeight = FontWeights.SemiBold, FontSize = 10, Foreground = Brushes.White,
            },
        };
        Canvas.SetLeft(label, Math.Max(0, Math.Min(Strip.ActualWidth - 42, x - 20)));
        Canvas.SetTop(label, 4);
        StripLabelLayer.Children.Add(label);
    }

    /// <summary>Tarzın renkli, kumlu kapak parçası (web .tile / Cover).</summary>
    FrameworkElement GenreTile(string? name, string? from, string? to, string? ink, double fontSize, bool past)
    {
        var grid = new Grid { ClipToBounds = true };
        grid.Children.Add(new Border { Background = Visuals.Gradient(from, to), CornerRadius = new CornerRadius(4) });
        grid.Children.Add(new Rectangle { Fill = Visuals.Grain, Opacity = 0.45, RadiusX = 4, RadiusY = 4 });
        grid.Children.Add(new TextBlock
        {
            Text = Visuals.Upper(name), FontFamily = (FontFamily)FindResource("Wide"), FontStretch = FontStretches.Expanded, FontWeight = FontWeights.ExtraBold, FontSize = fontSize,
            Foreground = Visuals.Solid(ink) ?? Brushes.White, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 6, 7),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (past) grid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(0x9E, 0x0C, 0x0B, 0x0A)), CornerRadius = new CornerRadius(4) });
        return grid;
    }

    // ===================== kartlar =====================

    void ApplyCards(JObject s)
    {
        var sch = s["schedule"] as JObject;
        var next = (string?)sch?["next"];
        NextAnnTime.Text = next ?? "—";
        NextAnnWhat.Text = sch == null ? "Anons listesi bekleniyor" : next == null ? "Yayında anons yok" : (string?)sch["nextWhat"] ?? "";
        NavAnnBadge.Text = next ?? "";

        var hist = (s["history"] as JArray)?.OfType<JObject>().Take(2).ToList() ?? new List<JObject>();
        var key = string.Join("|", hist.Select(h => h.ToString(Formatting.None)));
        if (key == _recentKey) return;
        _recentKey = key;
        RecentList.Items.Clear();
        foreach (var h in hist) RecentList.Items.Add(HistoryRow(h, 30, compact: true));
        if (hist.Count == 0)
            RecentList.Items.Add(new TextBlock { Text = "Henüz çalan yok", FontSize = 12.5, Foreground = (Brush)FindResource("Fg3") });
    }

    FrameworkElement HistoryRow(JObject h, double thumb, bool compact)
    {
        var anons = (string?)h["kind"] == "anons";
        var row = new Grid { Margin = new Thickness(0, 0, 0, compact ? 7 : 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(thumb) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var img = Visuals.Image((string?)h["cover"], 96);
        FrameworkElement th = img != null
            ? new Border { CornerRadius = new CornerRadius(3), ClipToBounds = true, Child = new Image { Source = img, Stretch = Stretch.UniformToFill }, Width = thumb, Height = thumb }
            : new Border { CornerRadius = new CornerRadius(3), Background = (Brush)FindResource(anons ? "VmSoft" : "Bg3"), Width = thumb, Height = thumb };
        row.Children.Add(th);
        var text = new StackPanel { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = compact ? 12.5 : 14, TextTrimming = TextTrimming.CharacterEllipsis };
        if (anons) title.Inlines.Add(new System.Windows.Documents.Run(Visuals.Tracked("ANONS") + "  ") { Foreground = (Brush)FindResource("Vm"), FontFamily = (FontFamily)FindResource("Mono"), FontSize = 9.5 });
        title.Inlines.Add(new System.Windows.Documents.Run((string?)h["title"] ?? ""));
        text.Children.Add(title);
        text.Children.Add(new TextBlock { Text = (string?)h["sub"] ?? "", FontSize = compact ? 11.5 : 12, Foreground = (Brush)FindResource("Fg3"), TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var at = (string?)h["at"] ?? "";
        var time = new TextBlock { Text = compact && at.Length > 5 ? at.Substring(at.Length - 5) : at, Style = (Style)FindResource("MonoText"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(time, 2);
        row.Children.Add(time);
        return row;
    }

    // ===================== sayfalar =====================

    void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        _page = sender == NavFlow ? "flow" : sender == NavAnn ? "ann" : sender == NavHistory ? "history" : sender == NavArchive ? "archive" : "now";
        NowView.Visibility = _page == "now" ? Visibility.Visible : Visibility.Collapsed;
        PageView.Visibility = _page == "now" ? Visibility.Collapsed : Visibility.Visible;
        Ambient.Visibility = _page == "now" ? Visibility.Visible : Visibility.Collapsed;
        RenderPage(true);
    }

    void RenderPage(bool force)
    {
        var s = _state;
        if (s == null || _page == "now") return;
        JToken? data = _page switch
        {
            "flow" => new JArray(s["slots"] ?? new JArray(), s["archive"] ?? new JObject(), ((int?)s["minute"] ?? 0) / 15),
            "ann" => s["schedule"],
            "history" => s["history"],
            _ => new JArray(s["archive"] ?? new JObject(), s["library"] ?? new JObject()),
        };
        var key = _page + (data?.ToString(Formatting.None) ?? "");
        if (!force && key == _pageKey) return;
        _pageKey = key;
        PageBody.Children.Clear();
        switch (_page)
        {
            case "flow": RenderFlow(s); break;
            case "ann": RenderAnn(s); break;
            case "history": RenderHistory(s); break;
            default: RenderArchive(s); break;
        }
        if (force) PageScroll.ScrollToTop();
        // Bugünkü akış: şu an çalan dilim görünür olsun.
        if (force && _flowNowRow != null)
        {
            var row = _flowNowRow;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PageScroll.UpdateLayout();
                var y = row.TranslatePoint(new Point(0, 0), PageBody).Y;
                PageScroll.ScrollToVerticalOffset(Math.Max(0, y - 120));
            }), DispatcherPriority.ContextIdle);
        }
    }

    void Header(string eyebrow, string title, string sub)
    {
        PageEyebrow.Text = Visuals.Tracked(eyebrow);
        PageTitle.Text = title;
        PageSub.Text = sub;
        PageSub.Visibility = sub.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    FrameworkElement? _flowNowRow;

    void RenderFlow(JObject s)
    {
        _flowNowRow = null;
        Header("AKIŞ", "Bugünkü akış", "Akış web sitesinden düzenlenir (soldaki \"Akışı düzenle\"); değişiklik en geç bir saatte buraya gelir.");
        var slots = (s["slots"] as JArray)?.OfType<JObject>().OrderBy(o => (int?)o["hour"] ?? 0).ToList() ?? new List<JObject>();
        var genres = new Dictionary<string, JObject>();
        foreach (var g in ((s["archive"] as JObject)?["genres"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            genres[(string?)g["name"] ?? ""] = g;
        var minute = (int?)s["minute"] ?? 0;
        for (var i = 0; i < slots.Count; i++)
        {
            var from = (int?)slots[i]["hour"] ?? 0;
            var to = i + 1 < slots.Count ? (int?)slots[i + 1]["hour"] ?? 24 : 24;
            var name = (string?)slots[i]["name"] ?? "";
            var state = minute >= to * 60 ? "ÇALDI" : minute >= from * 60 ? "ÇALIYOR" : "SIRADA";
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10), Opacity = state == "ÇALDI" ? 0.55 : 1 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var time = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            time.Children.Add(new TextBlock { Text = $"{from:00}:00", FontFamily = (FontFamily)FindResource("Mono"), FontSize = 18, FontWeight = FontWeights.Medium, Foreground = state == "ÇALIYOR" ? (Brush)FindResource("Fg") : (Brush)FindResource("Fg2") });
            time.Children.Add(new TextBlock { Text = $"→ {to:00}:00", Style = (Style)FindResource("MonoText") });
            row.Children.Add(time);
            var tile = GenreTile("", (string?)slots[i]["from"], (string?)slots[i]["to"], null, 9, false);
            tile.Width = 44;
            tile.Height = 44;
            tile.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(tile, 1);
            row.Children.Add(tile);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = name, FontSize = 16, FontWeight = FontWeights.SemiBold });
            var g = genres.TryGetValue(name, out var gg) ? gg : null;
            info.Children.Add(new TextBlock
            {
                Text = g == null ? "Tarz modu · karışık" : $"Tarz modu · karışık · {(int?)g["ready"]}/{(int?)g["total"]} şarkı inmiş",
                FontSize = 12, Foreground = (Brush)FindResource("Fg3"),
            });
            Grid.SetColumn(info, 2);
            row.Children.Add(info);
            var tag = MakeTag(state, state == "ÇALIYOR");
            Grid.SetColumn(tag, 3);
            row.Children.Add(tag);
            PageBody.Children.Add(row);
            PageBody.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Line"), Margin = new Thickness(0, 0, 0, 10) });
            if (state == "ÇALIYOR") _flowNowRow = row;
        }
        if (slots.Count > 0)
            PageBody.Children.Add(new TextBlock { Text = $"00:00–{(int?)slots[0]["hour"]:00}:00 arası son dilim çalmaya devam eder.", FontSize = 12, Foreground = (Brush)FindResource("Fg3") });
    }

    Border MakeTag(string text, bool filled) => new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(6, 2, 6, 2),
        Background = filled ? (Brush)FindResource("Vm") : Brushes.Transparent,
        BorderBrush = filled ? (Brush)FindResource("Vm") : (Brush)FindResource("Line"),
        BorderThickness = new Thickness(1),
        Child = new TextBlock { Text = Visuals.Tracked(text), FontFamily = (FontFamily)FindResource("Mono"), FontWeight = FontWeights.SemiBold, FontSize = 9.5, Foreground = filled ? Brushes.White : (Brush)FindResource("Fg3") },
    };

    void RenderAnn(JObject s)
    {
        var sch = s["schedule"] as JObject;
        var next = (string?)sch?["next"];
        Header("ANONSLAR", next != null ? $"Sıradaki kuşak {next}" : "Anonslar",
            sch == null ? "Anons listesi bekleniyor." : next != null ? (string?)sch["nextWhat"] ?? "" : "Yayında anons yok.");
        foreach (var p in (sch?["plans"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            PageBody.Children.Add(new TextBlock { Text = Visuals.Tracked(((string?)p["label"] ?? "").ToUpper(Turkish)), Style = (Style)FindResource("Eyebrow"), Foreground = (Brush)FindResource("Vm"), Margin = new Thickness(0, 6, 0, 8) });
            foreach (var it in (p["items"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                if ((bool?)it["ready"] == false)
                {
                    var t = MakeTag("İNİYOR", false);
                    DockPanel.SetDock(t, Dock.Right);
                    row.Children.Add(t);
                }
                var ends = (string?)it["ends"];
                var tb = new TextBlock { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
                tb.Inlines.Add(new System.Windows.Documents.Run((string?)it["title"] ?? "") { FontWeight = FontWeights.SemiBold });
                if (ends != null) tb.Inlines.Add(new System.Windows.Documents.Run($"  ·  {ends} tarihine kadar") { Foreground = (Brush)FindResource("Fg3"), FontSize = 12 });
                row.Children.Add(tb);
                PageBody.Children.Add(row);
            }
            PageBody.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Line"), Margin = new Thickness(0, 6, 0, 8) });
        }
        var upcoming = (sch?["upcoming"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        if (upcoming.Count > 0)
        {
            PageBody.Children.Add(new TextBlock { Text = Visuals.Tracked("BAŞLAYACAK"), Style = (Style)FindResource("Eyebrow"), Margin = new Thickness(0, 6, 0, 8) });
            foreach (var u in upcoming)
                PageBody.Children.Add(new TextBlock { Text = $"{(string?)u["starts"]}  ·  {(string?)u["title"]}  ·  {(string?)u["plan"]}", FontSize = 13, Foreground = (Brush)FindResource("Fg2"), Margin = new Thickness(0, 0, 0, 6) });
        }
    }

    void RenderHistory(JObject s)
    {
        Header("GEÇMİŞ", "Son çalanlar", "Bu bilgisayarda son 7 gün: mağazada ne çaldığını buradan görebilirsiniz.");
        var items = (s["history"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        if (items.Count == 0) PageBody.Children.Add(new TextBlock { Text = "Henüz çalan yok.", Foreground = (Brush)FindResource("Fg3") });
        foreach (var h in items) PageBody.Children.Add(HistoryRow(h, 40, compact: false));
    }

    void RenderArchive(JObject s)
    {
        var lib = s["library"] as JObject;
        var arc = s["archive"] as JObject;
        var total = (int?)lib?["total"] ?? 0;
        var done = (int?)lib?["done"] ?? 0;
        var pct = total > 0 ? 100 * done / total : 0;
        var state = (string?)lib?["state"];
        var bytes = (long?)arc?["bytes"] ?? 0;
        var sub = state switch
        {
            "ready" => $"Arşiv tamam. Son kontrol {(string?)lib?["checkedAt"] ?? "—"}.",
            "downloading" => $"Yeni müzikler iniyor. Yayın etkilenmez; en fazla {((int?)lib?["speedKbps"] ?? 6000) / 1000} Mbps kullanılır.",
            _ => (string?)lib?["message"] ?? "Arşiv açılıyor…",
        };
        Header("ARŞİV", $"%{pct}  ·  {bytes / 1073741824.0:0.0} GB", sub);
        foreach (var g in (arc?["genres"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            var ready = (int?)g["ready"] ?? 0;
            var all = Math.Max(1, (int?)g["total"] ?? 1);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            var tile = GenreTile("", (string?)g["from"], (string?)g["to"], null, 9, false);
            tile.Width = tile.Height = 26;
            tile.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(tile);
            var name = new TextBlock { Text = (string?)g["name"], FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            var bar = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center };
            bar.Children.Add(new Border { Background = (Brush)FindResource("Track"), CornerRadius = new CornerRadius(2) });
            var fill = new Border
            {
                Background = Visuals.Gradient((string?)g["from"], (string?)g["to"]), CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
            };
            bar.Children.Add(fill);
            var ratio = (double)ready / all;
            bar.SizeChanged += (_, _) => fill.Width = bar.ActualWidth * ratio;
            Grid.SetColumn(bar, 2);
            row.Children.Add(bar);
            var count = new TextBlock { Text = $"{ready}/{all} şarkı", Style = (Style)FindResource("MonoText"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count, 3);
            row.Children.Add(count);
            PageBody.Children.Add(row);
        }
        var next = (string?)lib?["nextUpdate"];
        var updates = (bool?)lib?["updates"] ?? true;
        PageBody.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Line"), Margin = new Thickness(0, 6, 0, 12) });
        PageBody.Children.Add(new TextBlock
        {
            Text = updates
                ? $"Aylık yeni müzik açık: albümlerin bir kısmı (en fazla 5) ayda bir yenilenir{(next != null ? $"; sıradaki {next}" : "")}."
                : "Aylık yeni müzik kapalı: albüm listesi sabit kalır (akışa eklenen yeni tarz yine iner).",
            FontSize = 12.5, Foreground = (Brush)FindResource("Fg2"), TextWrapping = TextWrapping.Wrap,
        });
        PageBody.Children.Add(new TextBlock
        {
            Text = $"{(int?)arc?["albums"] ?? 0} albüm · {(int?)arc?["tracks"] ?? 0} şarkı · arşiv bu bilgisayara özel şifreli",
            Style = (Style)FindResource("MonoText"), Margin = new Thickness(0, 10, 0, 0),
        });
    }

    // ===================== ekranlar =====================

    void ShowScreen(string screen)
    {
        var wasLogin = LoginScreen.Visibility == Visibility.Visible;
        PlayerScreen.Visibility = screen == "player" ? Visibility.Visible : Visibility.Collapsed;
        LoginScreen.Visibility = screen == "login" ? Visibility.Visible : Visibility.Collapsed;
        ExpiredScreen.Visibility = screen == "expired" ? Visibility.Visible : Visibility.Collapsed;
        if (screen == "login" && !wasLogin)
        {
            LoginButton.IsEnabled = true;
            (UserBox.Text.Length == 0 ? (IInputElement)UserBox : PassBox).Focus();
        }
    }

    void ShowConnection(bool connected)
    {
        if (connected) return;
        ShowScreen("player");
        LiveEyebrow.Text = Visuals.Tracked("MOTORA BAĞLANILIYOR");
        NextButton.IsEnabled = false;
    }

    // ===================== giriş =====================

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

    // ===================== düğmeler =====================

    void DrawProgress()
    {
        var ratio = _dur > 0 ? Math.Max(0, Math.Min(1, _pos / _dur)) : 0;
        ProgressFill.Width = ProgressTrack.ActualWidth * ratio;
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
        if (_applyingState || !IsLoaded) return;
        _lastUserVolume = DateTime.Now;
        _link.Send(new JObject { ["t"] = "volume", ["v"] = (int)e.NewValue });
    }

    void Next_Click(object sender, RoutedEventArgs e) => _link.Send(new JObject { ["t"] = "next" });

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
}
