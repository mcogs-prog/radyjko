using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RADYJKO;

public partial class MainWindow : Window
{
    private readonly AudioPlayer _player = new();
    private readonly StreamRecorder _recorder = new();
    private readonly SleepTimer _sleepTimer = new();
    private readonly DiscordRpcService _discordRpc = new();
    private readonly WebRemoteServer _webRemote = new();
    private readonly GlobalHotkeys _hotkeys = new();
    private readonly SpotifyService _spotify = new();
    private readonly UserSettings _settings;

    private ObservableCollection<Station> _favorites = new();
    private ObservableCollection<string> _history = new();
    private ObservableCollection<ScheduledRecording> _schedules = new();

    private Station? _currentStation;
    private string _currentTrack = string.Empty;
    private bool _isMiniPlayerMode = false;
    private MicroWindow? _microWindow;
    private BitmapImage? _currentCover;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _isInitializing = true;

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            _settings = SettingsManager.Load();
            TxtSpotifyClientId.Text = _settings.SpotifyClientId;

            int langIdx = 0;
            if (_settings.Language == "EN") langIdx = 1;
            else if (_settings.Language == "LT") langIdx = 2;
            else if (_settings.Language == "DE") langIdx = 3;

            ComboLang.SelectedIndex = langIdx;
            ComboLangOptions.SelectedIndex = langIdx;

            _isInitializing = false;
            ApplyLocalization(_settings.Language);

            InitializeTray();
            InitializeWebRemote();

            _favorites = new ObservableCollection<Station>(StationStorage.LoadFavorites());
            _schedules = new ObservableCollection<ScheduledRecording>(_settings.Schedules);
            ListFavorites.ItemsSource = _favorites;
            ListHistory.ItemsSource = _history;
            ListSchedules.ItemsSource = _schedules;

            _player.SetVolume(_settings.Volume);
            SliderVolume.Value = _settings.Volume;

            SliderEq0.Value = _settings.EqGains[0];
            SliderEq1.Value = _settings.EqGains[1];
            SliderEq2.Value = _settings.EqGains[2];
            SliderEq3.Value = _settings.EqGains[3];
            SliderEq4.Value = _settings.EqGains[4];
            SliderEq5.Value = _settings.EqGains[5];
            SliderEq6.Value = _settings.EqGains[6];
            SliderEq7.Value = _settings.EqGains[7];
            SliderEq8.Value = _settings.EqGains[8];
            SliderEq9.Value = _settings.EqGains[9];
            SliderEq10.Value = _settings.EqGains[10];
            SliderEq11.Value = _settings.EqGains[11];

            for (int i = 0; i < 12; i++) _player.ApplyBandGain(i, _settings.EqGains[i]);

            _player.SetAutoLeveler(_settings.EnableAutoLeveler);
            ChkAutoLeveler.IsChecked = _settings.EnableAutoLeveler;
            ChkCoverArt.IsChecked = _settings.EnableCoverArt;

            _player.PlaybackStateChanged += status => Dispatcher.Invoke(() => TxtStatus.Text = status);
            _player.TrackChanged += OnTrackChanged;
            _player.DiagnosticsChanged += diag => Dispatcher.Invoke(() =>
            {
                TxtDiagCodec.Text = diag.Codec;
                TxtDiagBitrate.Text = diag.Bitrate;
            });
            _player.SpectrumData += bands => Dispatcher.Invoke(() => UpdateVisualizer(bands));

            _recorder.RecordingStateChanged += (isRec, path) => Dispatcher.Invoke(() =>
            {
                if (isRec)
                {
                    BtnRecord.Content = "⏹ STOP";
                    BtnRecord.Foreground = System.Windows.Media.Brushes.White;
                    BtnRecord.Background = System.Windows.Media.Brushes.Red;
                    TxtRecordingIndicator.Text = "● REC";
                }
                else
                {
                    BtnRecord.Content = "⏺ REC";
                    BtnRecord.Foreground = System.Windows.Media.Brushes.Red;
                    BtnRecord.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#1E1E24");
                    TxtRecordingIndicator.Text = string.Empty;
                }
            });

            _sleepTimer.Tick += secs => Dispatcher.Invoke(() =>
            {
                if (secs > 0)
                {
                    var ts = TimeSpan.FromSeconds(secs);
                    TxtSleepStatus.Text = $"⏱ {ts.Minutes:D2}:{ts.Seconds:D2}";
                }
                else { TxtSleepStatus.Text = string.Empty; }
            });
            _sleepTimer.Elapsed += () => Dispatcher.Invoke(() => { _player.Stop(); BtnTogglePlay.Content = Localization.Get(_settings.Language, "Play"); });

            Loaded += (s, e) =>
            {
                DwmHelper.EnableMicaAndDarkMode(this);
                _hotkeys.Register(this);
                _hotkeys.OnPlayPause = TogglePlay;
                _hotkeys.OnNextStation = PlayNextStation;
                _hotkeys.OnPrevStation = PlayPrevStation;

                if (_settings.ResumeOnStartup && !string.IsNullOrEmpty(_settings.LastStationUrl))
                {
                    var last = _favorites.FirstOrDefault(st => st.StreamUrl == _settings.LastStationUrl);
                    if (last != null) PlayStation(last);
                }
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show("Błąd interfejsu: " + ex.Message);
        }
    }

    private void ApplyLocalization(string lang)
    {
        Title = Localization.Get(lang, "AppTitle");
        TabStationsHeader.Header = Localization.Get(lang, "TabStations");
        TabSearchHeader.Header = Localization.Get(lang, "TabSearch");
        TabEqHeader.Header = Localization.Get(lang, "TabEq");
        TabRemoteHeader.Header = Localization.Get(lang, "TabRemote");
        TabPlannerHeader.Header = Localization.Get(lang, "TabPlanner");
        TabHistoryHeader.Header = Localization.Get(lang, "TabHistory");
        TabTimerHeader.Header = Localization.Get(lang, "TabTimer");
        TabOptionsHeader.Header = Localization.Get(lang, "TabOptions");

        BtnTogglePlay.Content = _player.IsPlaying ? Localization.Get(lang, "Pause") : Localization.Get(lang, "Play");
        TxtStatus.Text = Localization.Get(lang, "Ready");
        if (_currentStation == null) TxtCurrentStation.Text = Localization.Get(lang, "SelectStation");
        if (string.IsNullOrEmpty(_currentTrack)) TxtTrackInfo.Text = Localization.Get(lang, "NoTrack");

        LblAddCustom.Text = Localization.Get(lang, "AddCustom");
        TxtCustomName.Text = Localization.Get(lang, "CustomName");
        TxtCustomUrl.Text = Localization.Get(lang, "CustomUrl");
        BtnAddCustom.Content = Localization.Get(lang, "AddToList");
        BtnDelFav.Content = Localization.Get(lang, "DeleteSelected");

        BtnSearchAction.Content = Localization.Get(lang, "SearchBtn");
        BtnPlaySearched.Content = Localization.Get(lang, "Play");
        BtnAddSearched.Content = Localization.Get(lang, "AddToFav");

        LblEqTitle.Text = Localization.Get(lang, "TabEq");
        ChkAutoLeveler.Content = Localization.Get(lang, "AutoLeveler");
        BtnResetEq.Content = Localization.Get(lang, "ResetEq");
        BtnElecEq.Content = Localization.Get(lang, "ElectronicEq");
        BtnVocalEq.Content = Localization.Get(lang, "VocalEq");

        LblRemoteHeader.Text = Localization.Get(lang, "RemoteDesc");
        BtnCopyRemote.Content = Localization.Get(lang, "CopyUrl");
        BtnOpenRemote.Content = Localization.Get(lang, "OpenBrowser");

        LblPlannerTitle.Text = Localization.Get(lang, "PlannerTitle");
        BtnAddSchedule.Content = Localization.Get(lang, "AddPlanner");
        BtnSearchYT.Content = Localization.Get(lang, "SearchYouTube");

        LblTimerTitle.Text = Localization.Get(lang, "TimerTitle");
        ChkShutdownPc.Content = Localization.Get(lang, "ShutdownPc");
        BtnCancelTimer.Content = Localization.Get(lang, "CancelTimer");

        LblSpotifyHeader.Text = Localization.Get(lang, "SpotifyTitle");
        LblSpotifyDesc.Text = Localization.Get(lang, "SpotifyDesc");
        BtnSpotifyAuth.Content = Localization.Get(lang, "Authorize");
        ChkCoverArt.Content = Localization.Get(lang, "CoverArt");
        BtnOpenRecs.Content = Localization.Get(lang, "OpenRecords");
    }

    private void ComboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        int idx = ComboLang.SelectedIndex;
        if (sender == ComboLangOptions) idx = ComboLangOptions.SelectedIndex;

        string lang = "PL";
        if (idx == 1) lang = "EN";
        else if (idx == 2) lang = "LT";
        else if (idx == 3) lang = "DE";

        _settings.Language = lang;
        SettingsManager.Save(_settings);

        _isInitializing = true;
        ComboLang.SelectedIndex = idx;
        ComboLangOptions.SelectedIndex = idx;
        _isInitializing = false;

        ApplyLocalization(lang);
    }

    private void InitializeWebRemote()
    {
        _webRemote.Port = _settings.WebRemotePort;
        _webRemote.OnTogglePlay = () => Dispatcher.Invoke(TogglePlay);
        _webRemote.OnNextStation = () => Dispatcher.Invoke(PlayNextStation);
        _webRemote.OnSetVolume = vol => Dispatcher.Invoke(() => SliderVolume.Value = vol);
        _webRemote.OnGetStatus = () => System.Text.Json.JsonSerializer.Serialize(new
        {
            station = _currentStation?.Name ?? "RADYJKO",
            track = _currentTrack,
            playing = _player.IsPlaying,
            volume = _player.CurrentVolume
        });
        _webRemote.Start();
        TxtRemoteUrl.Text = $"http://{_webRemote.GetLocalIpAddress()}:{_webRemote.Port}";
    }

    private async void OnTrackChanged(string track)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            _currentTrack = track;
            TxtTrackInfo.Text = string.IsNullOrWhiteSpace(track) ? Localization.Get(_settings.Language, "NoTrack") : track;
            _discordRpc.UpdatePresence(_currentStation?.Name ?? "RADYJKO", track, _player.IsPlaying);

            if (!string.IsNullOrWhiteSpace(track) && !_history.Contains(track))
            {
                _history.Insert(0, $"{DateTime.Now:HH:mm} - {track}");
                if (_history.Count > 30) _history.RemoveAt(_history.Count - 1);
            }

            if (_settings.EnableCoverArt && !string.IsNullOrWhiteSpace(track))
            {
                var artUrl = await CoverArtService.FetchCoverArtUrlAsync(track);
                if (!string.IsNullOrEmpty(artUrl))
                {
                    try
                    {
                        _currentCover = new BitmapImage(new Uri(artUrl, UriKind.Absolute));
                        ImgCoverArt.Source = _currentCover;
                        _microWindow?.UpdateInfo(_currentStation?.Name ?? "", track, _currentCover);
                        return;
                    }
                    catch { }
                }
            }
            ImgCoverArt.Source = null;
            _currentCover = null;
            _microWindow?.UpdateInfo(_currentStation?.Name ?? "", track, null);
        });
    }

    private void InitializeTray()
    {
        try
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon { Text = "RADYJKO 3.3", Visible = true };
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Otwórz RADYJKO", null, (s, e) => ShowAndRestore());
            menu.Items.Add("Zakończ", null, (s, e) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => ShowAndRestore();
        }
        catch { }
    }

    private void UpdateVisualizer(float[] bands)
    {
        if (bands.Length < 8) return;
        Bar0.Value = bands[0]; Bar1.Value = bands[1]; Bar2.Value = bands[2]; Bar3.Value = bands[3];
        Bar4.Value = bands[4]; Bar5.Value = bands[5]; Bar6.Value = bands[6]; Bar7.Value = bands[7];
    }

    private async void PlayStation(Station station)
    {
        _currentStation = station;
        TxtCurrentStation.Text = station.Name;
        BtnTogglePlay.Content = Localization.Get(_settings.Language, "Pause");
        _settings.LastStationUrl = station.StreamUrl;
        SettingsManager.Save(_settings);

        await _player.PlayAsync(station.StreamUrl);
        _discordRpc.UpdatePresence(station.Name, "", true);
        _microWindow?.UpdateInfo(station.Name, _currentTrack, _currentCover);
    }

    private void TogglePlay()
    {
        if (_player.IsPlaying)
        {
            _player.Stop();
            BtnTogglePlay.Content = Localization.Get(_settings.Language, "Play");
            _recorder.StopRecording();
        }
        else if (_currentStation != null) PlayStation(_currentStation);
    }

    private void BtnTogglePlay_Click(object sender, RoutedEventArgs e) => TogglePlay();

    private void BtnRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording) _recorder.StopRecording();
        else if (_currentStation != null && _player.IsPlaying) _recorder.StartRecording(_currentStation.StreamUrl, _currentStation.Name, _currentTrack);
        else MessageBox.Show(_settings.Language == "EN" ? "Turn on a station first!" : (_settings.Language == "LT" ? "Pirmiausia įjunkite stotį!" : (_settings.Language == "DE" ? "Bitte erst einen Sender einschalten!" : "Włącz stację przed nagrywaniem!")));
    }

    private void PlayNextStation()
    {
        if (_favorites.Count == 0) return;
        int idx = _currentStation != null ? _favorites.IndexOf(_currentStation) : -1;
        PlayStation(_favorites[(idx + 1) % _favorites.Count]);
    }

    private void PlayPrevStation()
    {
        if (_favorites.Count == 0) return;
        int idx = _currentStation != null ? _favorites.IndexOf(_currentStation) : 0;
        PlayStation(_favorites[(idx - 1 < 0) ? _favorites.Count - 1 : idx - 1]);
    }

    private void BtnMiniPlayer_Click(object sender, RoutedEventArgs e)
    {
        _isMiniPlayerMode = !_isMiniPlayerMode;
        if (_isMiniPlayerMode) { Height = 185; Width = 390; Topmost = true; BtnMiniPlayer.Content = "🗖"; }
        else { Height = 780; Width = 550; Topmost = false; BtnMiniPlayer.Content = "🗗 Mini"; }
    }

    private void BtnMicroPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (_microWindow == null) _microWindow = new MicroWindow(this);
        _microWindow.UpdateInfo(_currentStation?.Name ?? "RADYJKO", _currentTrack, _currentCover);
        _microWindow.Show(); Hide();
    }

    public void RestoreFromMicroMode() { _microWindow?.Hide(); Show(); WindowState = WindowState.Normal; Activate(); }

    private void TxtCustom_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox t && (t.Text.StartsWith("Nazwa") || t.Text.StartsWith("Adres") || t.Text.StartsWith("http") || t.Text.StartsWith("Station") || t.Text.StartsWith("Stoties") || t.Text.StartsWith("Srauto") || t.Text.StartsWith("Sender")))
        {
            t.Text = string.Empty;
            t.Foreground = System.Windows.Media.Brushes.White;
        }
    }

    private void BtnAddCustomStation_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtCustomName.Text.Trim();
        var url = TxtCustomUrl.Text.Trim();
        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
        {
            _favorites.Insert(0, new Station(name, url, "Własna", true));
            StationStorage.SaveFavorites(_favorites);
            TxtStatus.Text = "OK";
        }
    }

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtVolume != null) TxtVolume.Text = $"{(int)(e.NewValue * 100)}%";
        _player?.SetVolume((float)e.NewValue);
        if (_settings != null) _settings.Volume = (float)e.NewValue;
    }

    private void ChkAutoLeveler_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings != null && _player != null)
        {
            _settings.EnableAutoLeveler = ChkAutoLeveler.IsChecked == true;
            _player.SetAutoLeveler(_settings.EnableAutoLeveler);
            SettingsManager.Save(_settings);
        }
    }

    private void EqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider s && s.Name.StartsWith("SliderEq") && int.TryParse(s.Name.Substring(8), out int idx))
        {
            float val = (float)s.Value;
            _player.ApplyBandGain(idx, val);
            _settings.EqGains[idx] = val;
            SettingsManager.Save(_settings);
        }
    }

    private void PresetFlat_Click(object sender, RoutedEventArgs e)
    {
        SliderEq0.Value = SliderEq1.Value = SliderEq2.Value = SliderEq3.Value = SliderEq4.Value = SliderEq5.Value = SliderEq6.Value = SliderEq7.Value = SliderEq8.Value = SliderEq9.Value = SliderEq10.Value = SliderEq11.Value = 0;
    }

    private void PresetElectronic_Click(object sender, RoutedEventArgs e)
    {
        SliderEq0.Value = 4; SliderEq1.Value = 5; SliderEq2.Value = 3; SliderEq3.Value = 1; SliderEq4.Value = -1; SliderEq5.Value = 0; SliderEq6.Value = 2; SliderEq7.Value = 4; SliderEq8.Value = 5; SliderEq9.Value = 5; SliderEq10.Value = 4; SliderEq11.Value = 3;
    }

    private void PresetVocal_Click(object sender, RoutedEventArgs e)
    {
        SliderEq0.Value = -2; SliderEq1.Value = -3; SliderEq2.Value = -1; SliderEq3.Value = 2; SliderEq4.Value = 5; SliderEq5.Value = 6; SliderEq6.Value = 6; SliderEq7.Value = 4; SliderEq8.Value = 1; SliderEq9.Value = -1; SliderEq10.Value = -2; SliderEq11.Value = -3;
    }

    private void Timer15_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(15, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void Timer30_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(30, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void Timer45_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(45, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void Timer60_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(60, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void BtnCancelTimer_Click(object sender, RoutedEventArgs e) => _sleepTimer.Stop();

    private void BtnSearchYouTube_Click(object sender, RoutedEventArgs e)
    {
        if (ListHistory.SelectedItem is string item)
        {
            var q = item.Contains(" - ") ? string.Join(" - ", item.Split(" - ").Skip(1)) : item;
            Process.Start(new ProcessStartInfo($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(q)}") { UseShellExecute = true });
        }
    }

    private void BtnOpenRecordsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "RADYJKO"));
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "RADYJKO")) { UseShellExecute = true });
    }

    private void BtnCopyRemoteUrl_Click(object sender, RoutedEventArgs e) { Clipboard.SetText(TxtRemoteUrl.Text); TxtStatus.Text = "OK"; }
    private void BtnOpenBrowserRemote_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(TxtRemoteUrl.Text) { UseShellExecute = true });

    private void BtnAddScheduleCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStation == null) return;
        _schedules.Add(new ScheduledRecording { StationName = _currentStation.Name, StreamUrl = _currentStation.StreamUrl });
        _settings.Schedules = _schedules.ToList();
        SettingsManager.Save(_settings);
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        var q = TxtSearchQuery.Text.Trim();
        if (string.IsNullOrEmpty(q)) return;
        ListSearchResults.ItemsSource = await RadioBrowserApi.SearchStationsAsync(q);
    }
    private void TxtSearchQuery_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) BtnSearch_Click(sender, e); }
    private void BtnPlaySearched_Click(object sender, RoutedEventArgs e) { if (ListSearchResults.SelectedItem is Station s) PlayStation(s); }
    private void BtnAddSearchedToFavorites_Click(object sender, RoutedEventArgs e) { if (ListSearchResults.SelectedItem is Station s) { _favorites.Add(s); StationStorage.SaveFavorites(_favorites); } }
    private void BtnDeleteFavorite_Click(object sender, RoutedEventArgs e) { if (ListFavorites.SelectedItem is Station s) { _favorites.Remove(s); StationStorage.SaveFavorites(_favorites); } }

    private async void BtnSpotifyLogin_Click(object sender, RoutedEventArgs e)
    {
        var id = TxtSpotifyClientId.Text.Trim();
        if (string.IsNullOrEmpty(id)) return;
        _settings.SpotifyClientId = id; SettingsManager.Save(_settings);
        bool ok = await _spotify.AuthenticateAsync(id);
        TxtSpotifyStatus.Text = ok ? "OK" : "Err";
    }

    private async void BtnSpotify_Click(object sender, RoutedEventArgs e)
    {
        if (!_spotify.IsAuthenticated) return;
        TxtSpotifyStatus.Text = await _spotify.SaveTrackAsync(_currentTrack);
    }

    private void ChkCoverArt_Changed(object sender, RoutedEventArgs e) { if (_settings != null) { _settings.EnableCoverArt = ChkCoverArt.IsChecked == true; SettingsManager.Save(_settings); } }
    private void ListFavorites_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ListFavorites.SelectedItem is Station s) PlayStation(s); }

    private void ShowAndRestore() { Show(); WindowState = WindowState.Normal; Activate(); }
    protected override void OnStateChanged(EventArgs e) { if (WindowState == WindowState.Minimized && _microWindow == null) Hide(); base.OnStateChanged(e); }
    private void ExitApp() { _hotkeys.Dispose(); _microWindow?.Close(); _webRemote.Dispose(); _recorder.Dispose(); _sleepTimer.Dispose(); _discordRpc.Dispose(); _trayIcon?.Dispose(); _player?.Dispose(); Application.Current.Shutdown(); }
    protected override void OnClosed(EventArgs e) { ExitApp(); base.OnClosed(e); }
}
