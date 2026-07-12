using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CS2RegionPicker;

public partial class MainWindow : Window
{

    public class PopItem : INotifyPropertyChanged
    {
        public List<(string Code, List<string> Ips)> SubPops { get; set; } = new();
        public string Desc { get; set; } = "";

        public string CodeText => string.Join(", ", SubPops.Select(s => s.Code));
        public List<string> AllIps => SubPops.SelectMany(s => s.Ips).Distinct().ToList();
        public string PrimaryCode => SubPops.Count > 0 ? SubPops[0].Code : "";

        public string CountryCode => Flags.Country(PrimaryCode) ?? "";
        public bool HasCountry => !string.IsNullOrEmpty(CountryCode);

        public string FlagPath => HasCountry
            ? $"pack://application:,,,/flags/{CountryCode.ToLowerInvariant()}.png"
            : "";

        public (double Lat, double Lon)? Coord { get; set; }

        bool _allowed = true;
        public bool IsAllowed
        {
            get => _allowed;
            set { _allowed = value; NotifyState(); }
        }

        bool _blockedByRules;
        public bool IsBlockedByRules
        {
            get => _blockedByRules;
            set { _blockedByRules = value; NotifyState(); }
        }

        public bool IsPending => IsAllowed == IsBlockedByRules;

        public string StateText =>
            IsPending ? Loc.T("state_pending")
                      : (IsAllowed ? Loc.T("state_allowed") : Loc.T("state_blocked"));

        public Brush StateBrush =>
            IsPending ? Pending
                      : (IsAllowed ? Allowed : Blocked);

        void NotifyState()
        {
            Notify(nameof(IsAllowed));
            Notify(nameof(IsBlockedByRules));
            Notify(nameof(IsPending));
            Notify(nameof(StateText));
            Notify(nameof(StateBrush));
        }

        string _pingText = "…";
        public string PingText
        {
            get => _pingText;
            private set { _pingText = value; Notify(nameof(PingText)); Notify(nameof(PingBrush)); Notify(nameof(StateBrush)); }
        }

        public Brush PingBrush
        {
            get
            {
                if (PingText.EndsWith("ms") && int.TryParse(PingText.Replace(" ms", ""), out int ms))
                    return ms < 70 ? Good : ms < 140 ? Medium : Bad;
                return Muted;
            }
        }

        public void SetPingPending() => PingText = "…";

        public void SetPingResult(int ms) => PingText = ms < 0 ? "timeout" : ms + " ms";

        void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public event PropertyChangedEventHandler? PropertyChanged;

        static Brush Good    = Freeze("#A3BE8C");
        static Brush Medium  = Freeze("#EBCB8B");
        static Brush Bad     = Freeze("#BF616A");
        static Brush Muted   = Freeze("#81A1C1");

        static Brush Allowed = Freeze("#4CAF6E");
        static Brush Blocked = Freeze("#C74E7B");
        static Brush Pending = Freeze("#5E81AC");

        static Brush Freeze(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    }

    const string SdrUrl = "https://api.steampowered.com/ISteamApps/GetSDRConfig/v1?appid=730";
    static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    readonly List<PopItem> _pops = new();
    AppSettings S => App.Settings;
    bool _isDark;
    bool _busy;
    int _pingVersion;

    public MainWindow()
    {
        InitializeComponent();

        _isDark = S.DarkMode;
        ThemeManager.Apply(_isDark);
        SyncThemeToggle();
        PushThemeToMap();
        InitLanguage();

        Map.SetHome(24.45, 54.38);
        Map.PopClicked += OnMapPopClicked;
    }

    void TitleBar_MouseDown(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Min_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object s, RoutedEventArgs e) => Close();

    void Settings_Click(object s, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Visible;

    bool _langInit;

    void InitLanguage()
    {
        Loc.Set(S.Language);

        LangCombo.ItemsSource = Loc.Languages;
        LangCombo.SelectedItem = Loc.Languages.Find(l => l.Code == Loc.Current) ?? Loc.Languages[0];
        _langInit = true;

        ApplyLanguage();
    }

    void Lang_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!_langInit) return;
        if (LangCombo.SelectedItem is not Loc.Language lang) return;
        if (lang.Code == Loc.Current) return;

        Loc.Set(lang.Code);
        S.Language = lang.Code;
        S.Save();

        LogBox.Document.Blocks.Clear();

        ApplyLanguage();
    }

    void ApplyLanguage()
    {
        FlowDirection = FlowDirection.LeftToRight;

        LogBox.FlowDirection = Loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        SettingsBtn.Content = Loc.T("settings");
        AboutBtn.Content    = Loc.T("about");

        SearchPlaceholder.Text = Loc.T("search_placeholder");
        AllBtn.Content  = Loc.T("mark_all");
        NoneBtn.Content = Loc.T("unmark_all");

        if (!_busy) ApplyBtn.Content = Loc.T("apply");
        LockBadgeText.Text = Loc.T("busy_badge");

        ActiveTitle.Text    = Loc.T("active_regions");
        AppliedEmpty.Text   = Loc.T("nothing_applied");

        SettingsTitle.Text  = Loc.T("settings_title");
        ThemeLabel.Text     = Loc.T("appearance");
        ThemeSubLabel.Text  = Loc.T("appearance_sub");
        LangLabel.Text      = Loc.T("language");
        LangSubLabel.Text   = Loc.T("language_sub");

        AboutTitleText.Text = Loc.T("created_by");
        AboutBlurb.Text     = Loc.T("about_blurb");

        if (_pops.Count > 0)
        {
            RefreshAppliedPanel();
            Map.SetPops(_pops);
            ResultsList.Items.Refresh();
        }
    }
    void SettingsClose_Click(object s, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Collapsed;
    void About_Click(object s, RoutedEventArgs e) => AboutOverlay.Visibility = Visibility.Visible;
    void AboutClose_Click(object s, RoutedEventArgs e) => AboutOverlay.Visibility = Visibility.Collapsed;

    void AboutOverlay_Click(object s, MouseButtonEventArgs e) => AboutOverlay.Visibility = Visibility.Collapsed;
    void AboutCard_Click(object s, MouseButtonEventArgs e) => e.Handled = true;

    void Discord_Click(object s, RoutedEventArgs e) => OpenUrl("https://discord.com/invite/U7AuQhu");
    void Github_Click(object s, RoutedEventArgs e) => OpenUrl("https://github.com/oqyh");
    void Steam_Click(object s, RoutedEventArgs e) => OpenUrl("https://steamcommunity.com/id/oQYh");
    void Kofi_Click(object s, RoutedEventArgs e) => OpenUrl("https://ko-fi.com/goldkingz");

    static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    void ThemeToggle_Click(object s, MouseButtonEventArgs e)
    {
        _isDark = !_isDark;
        S.DarkMode = _isDark;
        S.Save();
        ThemeManager.Apply(_isDark);
        SyncThemeToggle();
        PushThemeToMap();
    }

    void SyncThemeToggle()
    {
        AnimateKnob(ThemeKnobT, _isDark ? 30 : 0);
        ThemeKnobIcon.Text = _isDark ? "🌙" : "☀";

        ThemeToggle.SetResourceReference(Border.BackgroundProperty, _isDark ? "ToggleOnBg" : "ToggleTrackBg");
    }

    static void AnimateKnob(TranslateTransform t, double to)
        => t.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase() });

    void PushThemeToMap()
    {
        if (Map == null) return;
        Brush good = Hex("#A3BE8C"), medium = Hex("#EBCB8B"), bad = Hex("#BF616A");
        if (_isDark)
            Map.SetTheme(Hex("#17212B"), Hex("#2E3B48"), Hex("#3B4B5E"), Hex("#88C0D0"), Hex("#EBCB8B"), good, medium, bad, Hex("#3B4B5E"));
        else
            Map.SetTheme(Hex("#DCE7F2"), Hex("#B9CADD"), Hex("#A9BAD0"), Hex("#DD7878"), Hex("#DF8E1D"), good, medium, bad, Hex("#CAD6E4"));

        if (_isDark)
            Map.SetInfoColors(Hex("#263340"), Hex("#D8DEE9"), Hex("#81A1C1"));
        else
            Map.SetInfoColors(Hex("#FFFFFF"), Hex("#2E3440"), Hex("#5C6470"));
    }

    static Brush Hex(string h) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await ReloadAsync();
    }

    async Task ReloadAsync()
    {
        Log(Loc.T("log_fetching"));
        try
        {
            var raw = await FetchPopsAsync();
            var blocked = await Firewall.GetBlockedPopsAsync();

            var pops = raw.GroupBy(p => p.Desc, StringComparer.OrdinalIgnoreCase)
                .Select(g => new PopItem { Desc = g.Key, SubPops = g.Select(p => (p.Code, p.Ips)).ToList() })
                .ToList();

            foreach (var pop in pops)
            {
                bool fw = pop.SubPops.Any(sp => blocked.Contains(sp.Code));
                if (S.HasSelection)
                {
                    bool inSaved = pop.SubPops.Any(sp => S.BlockedCodes.Contains(sp.Code, StringComparer.OrdinalIgnoreCase));
                    bool isNew = !pop.SubPops.Any(sp => S.KnownCodes.Contains(sp.Code, StringComparer.OrdinalIgnoreCase));
                    pop.IsAllowed = !(inSaved || (isNew && S.BlockedCodes.Count > 0));
                }
                else pop.IsAllowed = !fw;
                pop.IsBlockedByRules = fw;
            }

            _pops.Clear();
            _pops.AddRange(pops.OrderBy(p => p.Desc, StringComparer.OrdinalIgnoreCase));
            foreach (var pop in _pops)
                pop.PropertyChanged += (_, a) => { if (a.PropertyName == nameof(PopItem.IsAllowed)) { Map.SetPops(_pops); RefreshAppliedPanel(); } };

            foreach (var pop in _pops)
                pop.Coord = PopGeo.Lookup(pop.PrimaryCode);

            Map.SetPops(_pops);
            RefreshAppliedPanel();
            Log(Loc.T("log_loaded", _pops.Count));

            _pingVersion++;
            _ = PingAllAsync(_pingVersion);

            string fp = Fingerprint(_pops);
            if (S.HasSelection && fp != S.AppliedFingerprint)
            {
                Log(Loc.T("log_data_changed"));
                await ApplySelectionAsync();
            }
        }
        catch (Exception ex) { Log(Loc.T("log_error", ex.Message), Err); }
    }

    static async Task<List<(string Code, string Desc, List<string> Ips)>> FetchPopsAsync()
    {
        string json = await Http.GetStringAsync(SdrUrl);
        var result = new List<(string, string, List<string>)>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pops", out var pops)) throw new Exception("Unexpected SDR response.");
        foreach (var pop in pops.EnumerateObject())
        {
            var ips = new List<string>();
            if (pop.Value.TryGetProperty("relays", out var relays) && relays.ValueKind == JsonValueKind.Array)
                foreach (var relay in relays.EnumerateArray())
                    if (relay.TryGetProperty("ipv4", out var ip) && ip.ValueKind == JsonValueKind.String)
                    {
                        var v = ip.GetString();
                        if (!string.IsNullOrWhiteSpace(v) && !ips.Contains(v)) ips.Add(v);
                    }
            if (ips.Count == 0) continue;
            string desc = pop.Value.TryGetProperty("desc", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? pop.Name : pop.Name;
            result.Add((pop.Name, desc, ips));
        }
        if (result.Count == 0) throw new Exception("SDR response had no relays.");
        return result;
    }

    async Task PingAllAsync(int version)
    {
        var pops = _pops.ToList();
        foreach (var p in pops) p.SetPingPending();
        using var sem = new SemaphoreSlim(16);
        await Task.WhenAll(pops.Select(async pop =>
        {
            await sem.WaitAsync();
            try { int ms = await PingPopAsync(pop); if (version == _pingVersion) pop.SetPingResult(ms); }
            finally { sem.Release(); }
        }));
        if (version == _pingVersion)
        {
            Dispatcher.Invoke(() =>
            {
                Map.SetPops(_pops);
                RefreshAppliedPanel();
            });

            _ = ResolveUnknownCoordsAsync();
        }
    }

    static async Task<int> PingPopAsync(PopItem pop)
    {
        long best = long.MaxValue;
        foreach (string ip in pop.AllIps.Take(3))
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var r = await ping.SendPingAsync(ip, 1200);
                if (r.Status == System.Net.NetworkInformation.IPStatus.Success && r.RoundtripTime < best) best = r.RoundtripTime;
            }
            catch { }
        }
        return best == long.MaxValue ? -1 : (int)best;
    }

    void SearchBox_TextChanged(object s, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility =
            SearchBox.Text.Trim().Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowResults();
    }

    void SearchBox_Focus(object s, RoutedEventArgs e) => ShowResults();

    void SearchBox_LostFocus(object s, RoutedEventArgs e) => CloseResults();

    void SearchBox_Click(object s, MouseButtonEventArgs e)
    {
        if (!SearchBox.IsKeyboardFocused) SearchBox.Focus();
        ShowResults();
    }

    void ShowResults()
    {
        string q = SearchBox.Text.Trim();

        var matches = q.Length == 0
            ? _pops.ToList()
            : _pops.Where(p =>
                p.Desc.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.CodeText.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.CountryCode.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        ResultsList.ItemsSource = matches;
        ResultsPanel.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void CloseResults() => ResultsPanel.Visibility = Visibility.Collapsed;

    void Result_Click(object s, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)s).Tag is PopItem pop)
        {
            pop.IsAllowed = !pop.IsAllowed;
            Map.SetPops(_pops);
            ResultsList.Items.Refresh();
        }
    }

    void OnMapPopClicked(PopItem pop)
    {
        pop.IsAllowed = !pop.IsAllowed;
        Map.SetPops(_pops);
    }

    void SelectAll_Click(object s, RoutedEventArgs e) { foreach (var p in _pops) p.IsAllowed = true; Map.SetPops(_pops); RefreshAppliedPanel(); }
    void SelectNone_Click(object s, RoutedEventArgs e) { foreach (var p in _pops) p.IsAllowed = false; Map.SetPops(_pops); RefreshAppliedPanel(); }

    async void Apply_Click(object s, RoutedEventArgs e)
    {
        if (_pops.All(p => !p.IsAllowed))
        {
            bool ok = await ConfirmAsync(
                Loc.T("confirm_all_blocked_title"),
                Loc.T("confirm_all_blocked_msg"),
                Loc.T("confirm_apply_anyway"),
                Loc.T("confirm_cancel"));
            if (!ok) return;
        }
        await ApplySelectionAsync();
    }

    TaskCompletionSource<bool>? _confirmTcs;

    Task<bool> ConfirmAsync(string title, string message, string yesText, string noText)
    {
        ConfirmTitle.Text   = title;
        ConfirmMessage.Text = message;
        ConfirmYes.Content  = yesText;
        ConfirmNo.Content   = noText;

        ConfirmOverlay.Visibility = Visibility.Visible;
        _confirmTcs = new TaskCompletionSource<bool>();
        return _confirmTcs.Task;
    }

    void ConfirmYes_Click(object s, RoutedEventArgs e) => CloseConfirm(true);
    void ConfirmNo_Click(object s, RoutedEventArgs e)  => CloseConfirm(false);

    void CloseConfirm(bool result)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        _confirmTcs?.TrySetResult(result);
        _confirmTcs = null;
    }

    async Task ApplySelectionAsync()
    {
        SetBusy(true);
        var blockedPops = _pops.Where(p => !p.IsAllowed).ToList();
        Log(Loc.T("log_applying", blockedPops.Count));
        try
        {
            await Firewall.ClearAsync();

            var rules = blockedPops.SelectMany(p => p.SubPops).Select(sp => (sp.Code, sp.Ips)).ToList();
            await Firewall.ApplyAsync(rules);

            S.BlockedCodes = blockedPops.SelectMany(p => p.SubPops).Select(sp => sp.Code).ToList();
            S.KnownCodes = _pops.SelectMany(p => p.SubPops).Select(sp => sp.Code).ToList();
            S.AppliedFingerprint = Fingerprint(_pops);
            S.HasSelection = true;
            S.Save();

            foreach (var p in _pops) p.IsBlockedByRules = !p.IsAllowed;
            Log(Loc.T("log_applied", blockedPops.Count, blockedPops.Sum(p => p.AllIps.Count)), Ok);

            bool enforced = await VerifyEnforcementAsync(blockedPops);

            int worstAllowed = WorstAllowedPing();

            _pingVersion++;
            _ = PingAllAsync(_pingVersion);

            if (enforced)
            {
                SuggestMaxPing(worstAllowed);
                Log(Loc.T("log_can_exit"), Ok);
            }
        }
        catch (Exception ex) { Log(Loc.T("log_fw_error", ex.Message), Err); }
        finally { SetBusy(false); }
    }

    int WorstAllowedPing()
    {
        var allowedPings = _pops
            .Where(p => p.IsAllowed && p.PingText.EndsWith("ms"))
            .Select(p => int.TryParse(p.PingText.Replace(" ms", ""), out int ms) ? ms : -1)
            .Where(ms => ms > 0)
            .ToList();

        return allowedPings.Count == 0 ? -1 : allowedPings.Max();
    }

    void SuggestMaxPing(int worst)
    {
        if (worst <= 0)
        {
            Log(Loc.T("log_maxping_none"), Warn);
            return;
        }

        int suggested = ((worst / 25) + 2) * 25;

        LogCommand(Loc.T("log_maxping"), $"mm_dedicated_search_maxping {suggested}", Warn);
        Log(Loc.T("log_maxping_hint", worst), Warn);
    }

    async Task<bool> VerifyEnforcementAsync(List<PopItem> blockedPops)
    {
        var codes = blockedPops.SelectMany(p => p.SubPops).Select(sp => sp.Code).ToList();
        if (codes.Count == 0) return true;

        Log(Loc.T("log_verifying"));
        var (found, expected) = await Firewall.VerifyRulesAsync(codes);

        if (found < expected)
        {
            Log(Loc.T("log_verify_fail", found, expected), Err);
            return false;
        }

        Log(Loc.T("log_verify_ok", found, expected), Ok);

        bool? enforced = await FirewallIsEnforcedAsync();

        if (enforced == false)
        {
            Log(Loc.T("log_not_enforced"), Err);
            return false;
        }

        return true;
    }

    const string CanaryIp = "1.1.1.1";

    async Task<bool?> FirewallIsEnforcedAsync()
    {
        try
        {

            if (!await TcpReachableAsync(CanaryIp)) return null;

            await Firewall.AddSelfTestBlockAsync(CanaryIp);
            try
            {

                bool stillReachable = await TcpReachableAsync(CanaryIp);
                return !stillReachable;
            }
            finally
            {
                await Firewall.RemoveSelfTestBlockAsync();
            }
        }
        catch
        {
            return null;
        }
    }

    static async Task<bool> TcpReachableAsync(string ip)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connect = client.ConnectAsync(ip, 443);
            var done = await Task.WhenAny(connect, Task.Delay(2500));

            if (done != connect) return false;
            await connect;
            return true;
        }
        catch (System.Net.Sockets.SocketException ex)
        {

            return ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused;
        }
        catch { return false; }
    }

    async Task ResolveUnknownCoordsAsync()
    {
        var unknown = _pops.Where(p => p.Coord == null).ToList();
        if (unknown.Count == 0) return;

        bool any = false;
        foreach (var pop in unknown)
        {
            var coord = await PopGeo.ResolveAsync(pop.PrimaryCode, pop.AllIps, S.GeoCache);
            if (coord != null)
            {
                pop.Coord = coord;
                any = true;
            }
        }

        if (any)
        {
            S.Save();
            Dispatcher.Invoke(() => Map.SetPops(_pops));
            Log(Loc.T("log_located", unknown.Count(p => p.Coord != null)));
        }
    }

    void RefreshAppliedPanel()
    {
        var active = _pops
            .Where(p => !p.IsBlockedByRules)
            .OrderBy(p => PingSortKey(p))
            .ThenBy(p => p.Desc, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AppliedCount.Text = active.Count.ToString();
        AppliedList.ItemsSource = active;

        bool any = active.Count > 0;
        AppliedScroll.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        AppliedEmpty.Visibility  = any ? Visibility.Collapsed : Visibility.Visible;
    }

    static int PingSortKey(PopItem p)
        => p.PingText.EndsWith("ms") && int.TryParse(p.PingText.Replace(" ms", ""), out int ms)
            ? ms
            : int.MaxValue;

    static string Fingerprint(List<PopItem> pops)
    {
        var parts = pops.SelectMany(p => p.SubPops).OrderBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.Code + ":" + string.Join(",", s.Ips.OrderBy(i => i, StringComparer.Ordinal)));
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|", parts)));
        return Convert.ToHexString(hash);
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        LockBadge.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ApplyBtn.IsEnabled = !busy;
        AllBtn.IsEnabled = !busy;
        NoneBtn.IsEnabled = !busy;
        SearchBox.IsEnabled = !busy;
        SettingsBtn.IsEnabled = !busy;
        AboutBtn.IsEnabled = !busy;
        Map.IsEnabled = !busy;
        FinderRow.Opacity = busy ? 0.5 : 1.0;
        Map.Opacity = busy ? 0.6 : 1.0;
        ApplyBtn.Content = busy ? Loc.T("working") : Loc.T("apply");
    }

    Brush Ok   => (Brush)Application.Current.Resources["Success"];
    Brush Warn => (Brush)Application.Current.Resources["Warning"];
    Brush Err  => (Brush)Application.Current.Resources["Error"];

    void Log(string msg, Brush? color = null)
    {
        Dispatcher.Invoke(() =>
        {
            var run = new System.Windows.Documents.Run($"[{DateTime.Now:HH:mm:ss}]  {msg}");
            if (color != null) run.Foreground = color;
            var para = new System.Windows.Documents.Paragraph(run) { Margin = new Thickness(0) };
            LogBox.Document.Blocks.Add(para);
            LogBox.ScrollToEnd();
        });
    }

    void LogCommand(string msg, string command, Brush? color = null)
    {
        Dispatcher.Invoke(() =>
        {
            var head = new System.Windows.Documents.Run($"[{DateTime.Now:HH:mm:ss}]  {msg}");
            if (color != null) head.Foreground = color;

            var cmd = new System.Windows.Documents.Run(command)
            {
                Foreground = CommandBrush,
                FontWeight = FontWeights.Bold
            };

            var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };
            para.Inlines.Add(head);
            para.Inlines.Add(cmd);
            LogBox.Document.Blocks.Add(para);
            LogBox.ScrollToEnd();
        });
    }

    static readonly Brush CommandBrush =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B48EAD"));
}
