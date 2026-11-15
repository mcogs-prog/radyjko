using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RADYJKO;

public partial class MainWindow : Window
{
    private readonly AudioPlayer _player = new();
    private readonly StreamRecorder _recorder = new();
    private readonly SleepTimer _sleepTimer = new();
    private readonly DiscordRpcService _discordRpc = new();
    private readonly WebRemoteServer _webRemote = new();
    private readonly GlobalHotkeys _hotkeys = new();
    private readonly WindowsSmtcService _smtc = new();
    private readonly UserSettings _settings;
    private readonly DispatcherTimer _alarmTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _timeShiftUiTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string _lastAlarmTriggeredMinute = string.Empty;
    private string _lastDvrTriggeredMinute = string.Empty;

    private ObservableCollection<Station> _favorites = new();
    private ObservableCollection<HistoryEntry> _history = new();
    private ObservableCollection<ScheduledRecording> _schedules = new();
    private ObservableCollection<CustomEqPreset> _userPresets = new();

    private Station? _currentStation;
    private string _currentTrack = string.Empty;
    private string? _currentCoverUrl;
    private bool _isMiniPlayerMode = false;
    private MicroWindow? _microWindow;
    private BitmapImage? _currentCover;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _isInitializing = true;
    private float _previousVolume = 0.75f;
    private bool _isMuted = false;

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            _settings = SettingsManager.Load();
            ChkAlarmEnabled.IsChecked = _settings.AlarmEnabled;
            TxtAlarmTime.Text = string.IsNullOrWhiteSpace(_settings.AlarmTime) ? "07:00" : _settings.AlarmTime;
            ChkDvrAutoSplit.IsChecked = _settings.DvrAutoSplitTracks;
            _recorder.AutoSplitOnTrackChange = _settings.DvrAutoSplitTracks;
            ChkDarkTheme.IsChecked = _settings.DarkTheme;
            ChkAutostart.IsChecked = _settings.AutostartEnabled;
            ChkResumeStartup.IsChecked = _settings.ResumeOnStartup;
            ChkNightCompressor.IsChecked = _settings.EnableNightCompressor;

            int langIdx = 0;
            if (_settings.Language == "EN") langIdx = 1;
            else if (_settings.Language == "LT") langIdx = 2;
            else if (_settings.Language == "DE") langIdx = 3;

            ComboLang.SelectedIndex = langIdx;
            ComboLangOptions.SelectedIndex = langIdx;

            _isInitializing = false;
            ApplyLocalization(_settings.Language);
            ApplyTheme(_settings.DarkTheme);

            InitializeTray();
            InitializeWebRemote();
            InitializeSmtc();

            _favorites = new ObservableCollection<Station>(StationStorage.LoadFavorites());
            _schedules = new ObservableCollection<ScheduledRecording>(_settings.Schedules);
            _history = new ObservableCollection<HistoryEntry>(_settings.TrackHistory);
            _userPresets = new ObservableCollection<CustomEqPreset>(_settings.UserEqPresets);

            ListFavorites.ItemsSource = _favorites;
            ComboAlarmStation.ItemsSource = _favorites;
            ListHistory.ItemsSource = _history;
            ListSchedules.ItemsSource = _schedules;
            ComboUserPresets.ItemsSource = _userPresets;

            if (!string.IsNullOrEmpty(_settings.AlarmStationUrl))
                ComboAlarmStation.SelectedItem = _favorites.FirstOrDefault(s => s.StreamUrl == _settings.AlarmStationUrl);
            if (ComboAlarmStation.SelectedItem == null && _favorites.Count > 0)
                ComboAlarmStation.SelectedIndex = 0;

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
            _player.SetNightCompressor(_settings.EnableNightCompressor);
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
            _player.VuMeterData += (l, r) => Dispatcher.Invoke(() =>
            {
                VuLeft.Value = Math.Clamp(l * 1.4, 0, 1);
                VuRight.Value = Math.Clamp(r * 1.4, 0, 1);
            });

            _recorder.RecordingStateChanged += (isRec, path) => Dispatcher.Invoke(() =>
            {
                if (isRec)
                {
                    BtnRecord.Content = "⏹ STOP";
                    BtnRecord.Foreground = System.Windows.Media.Brushes.White;
                    BtnRecord.Background = System.Windows.Media.Brushes.Red;
                    TxtRecordingIndicator.Text = "● DVR REC";
                }
                else
                {
                    BtnRecord.Content = "⏺ REC";
                    BtnRecord.Foreground = System.Windows.Media.Brushes.Red;
                    BtnRecord.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#1E1E24")!;
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

            _alarmTimer.Tick += SchedulerAndAlarm_Tick;
            _alarmTimer.Start();

            _timeShiftUiTimer.Tick += (s, e) => UpdateTimeShiftUi();
            _timeShiftUiTimer.Start();

            Loaded += (s, e) =>
            {
                DwmHelper.ApplyWindowTheme(this, _settings.DarkTheme);
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
            System.Windows.MessageBox.Show("Błąd interfejsu: " + ex.Message);
        }
    }

    private void UpdateTimeShiftUi()
    {
        if (!_player.IsPlaying)
        {
            TxtTimeShiftStatus.Text = "● OFF";
            TxtTimeShiftStatus.Foreground = System.Windows.Media.Brushes.Gray;
            BtnTimeShiftPause.Content = "⏸ Buforuj";
            return;
        }

        if (_player.IsTimeShiftPaused)
        {
            TxtTimeShiftStatus.Text = $"⏸ BUFOR (-{_player.TimeShiftSecondsBehind}s)";
            TxtTimeShiftStatus.Foreground = System.Windows.Media.Brushes.Orange;
            BtnTimeShiftPause.Content = "▶ Wznów";
        }
        else if (_player.IsTimeShifted && _player.TimeShiftSecondsBehind > 0)
        {
            TxtTimeShiftStatus.Text = $"⏪ -{_player.TimeShiftSecondsBehind}s";
            TxtTimeShiftStatus.Foreground = System.Windows.Media.Brushes.DeepSkyBlue;
            BtnTimeShiftPause.Content = "⏸ Buforuj";
        }
        else
        {
            TxtTimeShiftStatus.Text = "● LIVE";
            TxtTimeShiftStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#10B981")!;
            BtnTimeShiftPause.Content = "⏸ Buforuj";
        }
    }

    private void BtnRewind30_Click(object sender, RoutedEventArgs e) { _player.RewindSeconds(30); UpdateTimeShiftUi(); }
    private void BtnRewind15_Click(object sender, RoutedEventArgs e) { _player.RewindSeconds(15); UpdateTimeShiftUi(); }
    private void BtnForward15_Click(object sender, RoutedEventArgs e) { _player.ForwardSeconds(15); UpdateTimeShiftUi(); }
    private void BtnTimeShiftPause_Click(object sender, RoutedEventArgs e) { _player.ToggleTimeShiftPause(); UpdateTimeShiftUi(); }
    private void BtnJumpLive_Click(object sender, RoutedEventArgs e) { _player.JumpToLive(); UpdateTimeShiftUi(); }

    private async void BtnRecognizeSong_Click(object sender, RoutedEventArgs e)
    {
        BtnRecognizeSong.IsEnabled = false;
        BtnRecognizeSong.Content = "⏳";
        TxtStatus.Text = "🎙 Nasłuch...";

        var result = await SongRecognitionService.RecognizeFromLoopbackAsync();
        BtnRecognizeSong.IsEnabled = true;
        BtnRecognizeSong.Content = "🎙 ID";
        TxtStatus.Text = "Gotowy";

        if (!string.IsNullOrEmpty(result.Title))
        {
            OnTrackChanged(result.Title);
        }
        else
        {
            // Prompt quick lyric/artist lookup if station lacks ICY metadata
            string defaultText = !string.IsNullOrWhiteSpace(_currentTrack) && _currentTrack != "Ładowanie..." ? _currentTrack : "";
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                result.Message + "\n\nPodaj tytuł lub fragment tekstu utworu do dopasowania okładki i zapisu w historii:",
                "RADYJKO PRO • Rozpoznawanie utworu",
                defaultText);

            if (!string.IsNullOrWhiteSpace(input))
            {
                var found = await SongRecognitionService.SearchTrackMetadataAsync(input);
                if (!string.IsNullOrEmpty(found.FullTitle))
                {
                    OnTrackChanged(found.FullTitle);
                }
                else
                {
                    OnTrackChanged(input.Trim());
                }
            }
        }
    }

    private void InitializeSmtc()
    {
        _smtc.Initialize();
        _smtc.OnPlay = () => Dispatcher.Invoke(() => { if (!_player.IsPlaying && _currentStation != null) PlayStation(_currentStation); });
        _smtc.OnPause = () => Dispatcher.Invoke(() => { if (_player.IsPlaying) TogglePlay(); });
        _smtc.OnNext = () => Dispatcher.Invoke(PlayNextStation);
        _smtc.OnPrevious = () => Dispatcher.Invoke(PlayPrevStation);
    }

    private void ApplyTheme(bool isDark)
    {
        DwmHelper.ApplyWindowTheme(this, isDark);
        var bc = new System.Windows.Media.BrushConverter();
        if (isDark)
        {
            Background = (System.Windows.Media.Brush)bc.ConvertFrom("#0D0D10")!;
            Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FAFAFA")!;
            BannerCard.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#CC141418")!;
        }
        else
        {
            Background = (System.Windows.Media.Brush)bc.ConvertFrom("#F1F5F9")!;
            Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#0F172A")!;
            BannerCard.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#1E293B")!;
        }
    }

    private void UpdateWindowsStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("RADYJKO_PRO", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("RADYJKO_PRO", false);
            }
        }
        catch { }
    }

    private void SchedulerAndAlarm_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        string nowStr = now.ToString("HH:mm");

        // 1. Check Radio Alarm
        if (_settings.AlarmEnabled && !string.IsNullOrWhiteSpace(_settings.AlarmTime) && nowStr == _settings.AlarmTime.Trim() && _lastAlarmTriggeredMinute != nowStr)
        {
            _lastAlarmTriggeredMinute = nowStr;
            var targetStation = _favorites.FirstOrDefault(s => s.StreamUrl == _settings.AlarmStationUrl) ?? _favorites.FirstOrDefault();
            if (targetStation != null)
            {
                ShowAndRestore();
                PlayStation(targetStation);
                TxtStatus.Text = "⏰ Budzik!";
            }
        }

        // 2. Check Cyclical DVR Schedules
        if (_lastDvrTriggeredMinute != nowStr)
        {
            foreach (var task in _schedules.Where(t => t.IsEnabled))
            {
                bool dayMatches = task.EveryDay || task.Day == now.DayOfWeek;
                string taskTimeStr = $"{task.StartTime.Hours:D2}:{task.StartTime.Minutes:D2}";
                if (dayMatches && taskTimeStr == nowStr)
                {
                    _lastDvrTriggeredMinute = nowStr;
                    _recorder.StartRecording(task.StreamUrl, task.StationName, $"DVR_{now:yyyyMMdd_HHmm}");
                    int durMin = Math.Clamp(task.DurationMinutes, 1, 360);
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(durMin));
                        Dispatcher.Invoke(() => { if (_recorder.IsRecording) _recorder.StopRecording(); });
                    });
                    break;
                }
            }
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

        ChkAutoLeveler.Content = Localization.Get(lang, "AutoLeveler");
        BtnResetEq.Content = Localization.Get(lang, "ResetEq");
        BtnElecEq.Content = Localization.Get(lang, "ElectronicEq");
        BtnVocalEq.Content = Localization.Get(lang, "VocalEq");
        BtnRockEq.Content = Localization.Get(lang, "RockEq");
        BtnBassEq.Content = Localization.Get(lang, "BassEq");
        BtnPopEq.Content = Localization.Get(lang, "PopEq");
        BtnJazzEq.Content = Localization.Get(lang, "JazzEq");
        BtnMetalEq.Content = Localization.Get(lang, "MetalEq");
        BtnClassicalEq.Content = Localization.Get(lang, "ClassicalEq");
        BtnHipHopEq.Content = Localization.Get(lang, "HipHopEq");
        BtnAcousticEq.Content = Localization.Get(lang, "AcousticEq");
        BtnClubEq.Content = Localization.Get(lang, "ClubEq");
        BtnSaveUserEq.Content = Localization.Get(lang, "SaveUserEq");
        BtnDelUserEq.Content = Localization.Get(lang, "DelUserEq");

        LblRemoteDesc.Text = Localization.Get(lang, "RemoteDesc");
        BtnCopyRemote.Content = Localization.Get(lang, "CopyUrl");
        BtnOpenRemote.Content = Localization.Get(lang, "OpenBrowser");

        BtnSearchYT.Content = Localization.Get(lang, "SearchYouTube");
        BtnExportHistCsv.Content = Localization.Get(lang, "ExportHistCsv");
        BtnExportHistTxt.Content = Localization.Get(lang, "ExportHistTxt");
        BtnClearHist.Content = Localization.Get(lang, "ClearHist");

        LblTimerTitle.Text = Localization.Get(lang, "TimerTitle");
        ChkShutdownPc.Content = Localization.Get(lang, "ShutdownPc");
        BtnCancelTimer.Content = Localization.Get(lang, "CancelTimer");

        ChkDarkTheme.Content = Localization.Get(lang, "DarkThemeChk");
        ChkAutostart.Content = Localization.Get(lang, "AutostartWin");
        ChkResumeStartup.Content = Localization.Get(lang, "ResumeStartup");
        ChkCoverArt.Content = Localization.Get(lang, "CoverArt");
        BtnOpenRecs.Content = Localization.Get(lang, "OpenRecords");
        BtnExportFavs.Content = Localization.Get(lang, "ExportFavs");
        BtnImportFavs.Content = Localization.Get(lang, "ImportFavs");
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
        _webRemote.OnPrevStation = () => Dispatcher.Invoke(PlayPrevStation);
        _webRemote.OnSelectStationIndex = idx => Dispatcher.Invoke(() =>
        {
            if (idx >= 0 && idx < _favorites.Count) PlayStation(_favorites[idx]);
        });
        _webRemote.OnSetVolume = vol => Dispatcher.Invoke(() => SliderVolume.Value = vol);
        _webRemote.OnGetFullState = () => Dispatcher.Invoke(() => new
        {
            station = _currentStation?.Name ?? "RADYJKO PRO",
            track = _currentTrack,
            playing = _player.IsPlaying,
            volume = _player.CurrentVolume,
            coverUrl = _currentCoverUrl ?? "",
            stations = _favorites.Select(s => new { name = s.Name, genre = s.Genre }).ToList()
        });
        _webRemote.Start();
        TxtRemoteUrl.Text = $"http://{_webRemote.GetLocalIpAddress()}:{_webRemote.Port}";
    }

    private async void OnTrackChanged(string track)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            bool changed = _currentTrack != track && !string.IsNullOrWhiteSpace(track) && track != "Ładowanie...";
            _currentTrack = track;
            TxtTrackInfo.Text = string.IsNullOrWhiteSpace(track) ? Localization.Get(_settings.Language, "NoTrack") : track;
            _discordRpc.UpdatePresence(_currentStation?.Name ?? "RADYJKO", track, _player.IsPlaying);

            if (changed)
            {
                _recorder.OnTrackChangedDuringRecording(track);
                PerformShoppingSearch(track);
            }

            if (!string.IsNullOrWhiteSpace(track) && track != "Ładowanie...")
            {
                var latest = _history.FirstOrDefault();
                if (latest == null || latest.Track != track)
                {
                    var entry = new HistoryEntry
                    {
                        Timestamp = DateTime.Now,
                        Station = _currentStation?.Name ?? "Radio",
                        Track = track
                    };
                    _history.Insert(0, entry);
                    if (_history.Count > 500) _history.RemoveAt(_history.Count - 1);
                    _settings.TrackHistory = _history.ToList();
                    SettingsManager.Save(_settings);
                }
            }

            if (_settings.EnableCoverArt && !string.IsNullOrWhiteSpace(track) && track != "Ładowanie...")
            {
                var artUrl = await CoverArtService.FetchCoverArtUrlAsync(track);
                _currentCoverUrl = artUrl;
                if (!string.IsNullOrEmpty(artUrl))
                {
                    try
                    {
                        _currentCover = new BitmapImage(new Uri(artUrl, UriKind.Absolute));
                        ImgCoverArt.Source = _currentCover;
                        _microWindow?.UpdateInfo(_currentStation?.Name ?? "", track, _currentCover);
                        _smtc.UpdateMetadata(_currentStation?.Name ?? "RADYJKO", track, _player.IsPlaying, artUrl);
                        return;
                    }
                    catch { }
                }
            }
            _currentCoverUrl = null;
            ImgCoverArt.Source = null;
            _currentCover = null;
            _microWindow?.UpdateInfo(_currentStation?.Name ?? "", track, null);
            _smtc.UpdateMetadata(_currentStation?.Name ?? "RADYJKO", track, _player.IsPlaying, null);
        });
    }

    private void InitializeTray()
    {
        try
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon { Text = "RADYJKO PRO 5.0.0 FINAL", Visible = true };
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
        if (bands.Length < 12) return;
        Bar0.Value = bands[0]; Bar1.Value = bands[1]; Bar2.Value = bands[2]; Bar3.Value = bands[3];
        Bar4.Value = bands[4]; Bar5.Value = bands[5]; Bar6.Value = bands[6]; Bar7.Value = bands[7];
        Bar8.Value = bands[8]; Bar9.Value = bands[9]; Bar10.Value = bands[10]; Bar11.Value = bands[11];
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
        _smtc.UpdateMetadata(station.Name, _currentTrack, true, _currentCoverUrl);
    }

    private void TogglePlay()
    {
        if (_player.IsPlaying)
        {
            _player.Stop();
            BtnTogglePlay.Content = Localization.Get(_settings.Language, "Play");
            _recorder.StopRecording();
            VuLeft.Value = 0;
            VuRight.Value = 0;
            _smtc.SetStopped();
        }
        else if (_currentStation != null) PlayStation(_currentStation);
    }

    private void BtnTogglePlay_Click(object sender, RoutedEventArgs e) => TogglePlay();

    private void BtnRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording) _recorder.StopRecording();
        else if (_currentStation != null && _player.IsPlaying) _recorder.StartRecording(_currentStation.StreamUrl, _currentStation.Name, _currentTrack);
        else System.Windows.MessageBox.Show(_settings.Language == "EN" ? "Turn on a station first!" : (_settings.Language == "LT" ? "Pirmiausia įjunkite stotį!" : (_settings.Language == "DE" ? "Bitte erst einen Sender einschalten!" : "Włącz stację przed nagrywaniem!")));
    }

    private void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        if (!_isMuted)
        {
            _previousVolume = (float)SliderVolume.Value;
            SliderVolume.Value = 0;
            BtnMute.Content = "🔇";
            _isMuted = true;
        }
        else
        {
            SliderVolume.Value = _previousVolume > 0 ? _previousVolume : 0.5f;
            BtnMute.Content = "🔊";
            _isMuted = false;
        }
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
        if (_isMiniPlayerMode) { Height = 200; Width = 400; Topmost = true; BtnMiniPlayer.Content = "🗖"; }
        else { Height = 855; Width = 590; Topmost = false; BtnMiniPlayer.Content = "🗗 Mini"; }
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
        if (sender is System.Windows.Controls.TextBox t && (t.Text.StartsWith("Nazwa") || t.Text.StartsWith("Adres") || t.Text.StartsWith("http") || t.Text.StartsWith("Station") || t.Text.StartsWith("Stoties") || t.Text.StartsWith("Srauto") || t.Text.StartsWith("Sender") || t.Text.StartsWith("Mój profil")))
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

    private void TxtFilterStations_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = TxtFilterStations.Text.Trim().ToLower();
        TxtFilterPlaceholder.Visibility = string.IsNullOrEmpty(filter) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(filter))
        {
            ListFavorites.ItemsSource = _favorites;
        }
        else
        {
            ListFavorites.ItemsSource = _favorites.Where(s => s.Name.ToLower().Contains(filter) || s.Genre.ToLower().Contains(filter)).ToList();
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

    private void ChkNightCompressor_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings != null && _player != null)
        {
            _settings.EnableNightCompressor = ChkNightCompressor.IsChecked == true;
            _player.SetNightCompressor(_settings.EnableNightCompressor);
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

    private void SetEqValues(params double[] v)
    {
        if (v.Length < 12) return;
        SliderEq0.Value = v[0]; SliderEq1.Value = v[1]; SliderEq2.Value = v[2]; SliderEq3.Value = v[3];
        SliderEq4.Value = v[4]; SliderEq5.Value = v[5]; SliderEq6.Value = v[6]; SliderEq7.Value = v[7];
        SliderEq8.Value = v[8]; SliderEq9.Value = v[9]; SliderEq10.Value = v[10]; SliderEq11.Value = v[11];
    }

    private void PresetFlat_Click(object sender, RoutedEventArgs e) => SetEqValues(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private void PresetElectronic_Click(object sender, RoutedEventArgs e) => SetEqValues(4, 5, 3, 1, -1, 0, 2, 4, 5, 5, 4, 3);
    private void PresetVocal_Click(object sender, RoutedEventArgs e) => SetEqValues(-2, -3, -1, 2, 5, 6, 6, 4, 1, -1, -2, -3);
    private void PresetRock_Click(object sender, RoutedEventArgs e) => SetEqValues(5, 4, 2, -1, -2, 0, 2, 4, 5, 6, 5, 4);
    private void PresetBass_Click(object sender, RoutedEventArgs e) => SetEqValues(8, 7, 6, 3, 1, 0, 0, 0, 0, 1, 2, 2);
    private void PresetPop_Click(object sender, RoutedEventArgs e) => SetEqValues(-1, 1, 4, 5, 4, 2, 0, 1, 3, 4, 3, 2);
    private void PresetJazz_Click(object sender, RoutedEventArgs e) => SetEqValues(3, 4, 2, 1, -1, -2, 0, 2, 3, 4, 4, 3);
    private void PresetMetal_Click(object sender, RoutedEventArgs e) => SetEqValues(6, 5, 3, 0, -4, -5, -2, 2, 5, 7, 7, 6);
    private void PresetClassical_Click(object sender, RoutedEventArgs e) => SetEqValues(4, 3, 2, 0, -1, -1, 0, 2, 3, 4, 5, 5);
    private void PresetHipHop_Click(object sender, RoutedEventArgs e) => SetEqValues(8, 7, 5, 2, -1, -2, 0, 1, 3, 4, 3, 2);
    private void PresetAcoustic_Click(object sender, RoutedEventArgs e) => SetEqValues(2, 3, 2, 1, 1, 2, 3, 4, 4, 3, 2, 2);
    private void PresetClub_Click(object sender, RoutedEventArgs e) => SetEqValues(6, 6, 4, 2, 0, 1, 3, 4, 5, 5, 4, 3);

    private void BtnSaveUserEq_Click(object sender, RoutedEventArgs e)
    {
        string pName = TxtNewPresetName.Text.Trim();
        if (string.IsNullOrWhiteSpace(pName) || pName == "Mój profil...")
            pName = $"Profil {_userPresets.Count + 1}";

        var gains = new float[12];
        Array.Copy(_settings.EqGains, gains, 12);

        var existing = _userPresets.FirstOrDefault(p => p.Name.Equals(pName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Gains = gains;
        }
        else
        {
            var np = new CustomEqPreset { Name = pName, Gains = gains };
            _userPresets.Add(np);
            ComboUserPresets.SelectedItem = np;
        }

        _settings.UserEqPresets = _userPresets.ToList();
        SettingsManager.Save(_settings);
        TxtStatus.Text = $"EQ: {pName}";
    }

    private void ComboUserPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboUserPresets.SelectedItem is CustomEqPreset preset && preset.Gains.Length == 12)
        {
            SetEqValues(preset.Gains.Select(g => (double)g).ToArray());
        }
    }

    private void BtnDelUserEq_Click(object sender, RoutedEventArgs e)
    {
        if (ComboUserPresets.SelectedItem is CustomEqPreset preset)
        {
            _userPresets.Remove(preset);
            _settings.UserEqPresets = _userPresets.ToList();
            SettingsManager.Save(_settings);
        }
    }

    private void Timer15_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(15, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void Timer30_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(30, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void Timer45_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(45, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void Timer60_Click(object sender, RoutedEventArgs e) => _sleepTimer.Start(60, v => _player.SetVolume(v), _player.CurrentVolume, ChkShutdownPc.IsChecked == true);
    private void BtnCancelTimer_Click(object sender, RoutedEventArgs e) => _sleepTimer.Stop();

    
    private async void PerformShoppingSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "Ładowanie...") return;
        try
        {
            // Extract artist part before " - "
            string searchKey = query.Contains(" - ") ? query.Split(" - ")[0].Trim() : query.Trim();
            TxtShopQuery.Text = searchKey;

            var allOffers = new List<ProductOffer>();
            var allegro = await ShoppingSearchService.SearchAllegroAsync(searchKey);
            var ebay = await ShoppingSearchService.SearchEbayAsync(searchKey);
            var imusic = await ShoppingSearchService.SearchImusicAsync(searchKey);

            allOffers.AddRange(allegro);
            allOffers.AddRange(ebay);
            allOffers.AddRange(imusic);

            // Sort: New first (cheapest to most expensive), then Used
            var sorted = allOffers.OrderBy(o => !o.IsNew).ThenBy(o => o.Price).ToList();
            ListShopOffers.ItemsSource = sorted;
        }
        catch { }
    }

    private void BtnShopSearch_Click(object sender, RoutedEventArgs e)
    {
        PerformShoppingSearch(TxtShopQuery.Text.Trim());
    }

    private void ListShopOffers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListShopOffers.SelectedItem is ProductOffer offer && !string.IsNullOrEmpty(offer.Url))
        {
            try { Process.Start(new ProcessStartInfo(offer.Url) { UseShellExecute = true }); } catch { }
        }
    }

    private void BtnQuickYouTube_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentTrack) && _currentTrack != "Ładowanie...")
        {
            Process.Start(new ProcessStartInfo($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(_currentTrack)}") { UseShellExecute = true });
        }
    }

    private void TxtFilterHistory_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = TxtFilterHistory.Text.Trim().ToLower();
        TxtHistoryPlaceholder.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(q))
        {
            ListHistory.ItemsSource = _history;
        }
        else
        {
            ListHistory.ItemsSource = _history.Where(h => h.DisplayText.ToLower().Contains(q)).ToList();
        }
    }

    private void BtnSearchYouTube_Click(object sender, RoutedEventArgs e)
    {
        if (ListHistory.SelectedItem is HistoryEntry item && !string.IsNullOrWhiteSpace(item.Track))
        {
            Process.Start(new ProcessStartInfo($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(item.Track)}") { UseShellExecute = true });
        }
    }

    private void BtnExportHistoryCsv_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = $"radyjko_historia_{DateTime.Now:yyyyMMdd}.csv" };
        if (sfd.ShowDialog() == true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Data_i_Czas;Stacja;Utwor");
            foreach (var h in _history)
                sb.AppendLine($"\"{h.Timestamp:yyyy-MM-dd HH:mm:ss}\";\"{h.Station.Replace("\"", "\"\"")}\";\"{h.Track.Replace("\"", "\"\"")}\"");
            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            System.Windows.MessageBox.Show("Wyeksportowano historię do CSV!", "RADYJKO PRO");
        }
    }

    private void BtnExportHistoryTxt_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt", FileName = $"radyjko_historia_{DateTime.Now:yyyyMMdd}.txt" };
        if (sfd.ShowDialog() == true)
        {
            File.WriteAllLines(sfd.FileName, _history.Select(h => h.DisplayText), Encoding.UTF8);
            System.Windows.MessageBox.Show("Wyeksportowano historię do TXT!", "RADYJKO PRO");
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        _settings.TrackHistory.Clear();
        SettingsManager.Save(_settings);
    }

    private void BtnOpenRecordsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "RADYJKO"));
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "RADYJKO")) { UseShellExecute = true });
    }

    private void BtnCopyRemoteUrl_Click(object sender, RoutedEventArgs e) { System.Windows.Clipboard.SetText(TxtRemoteUrl.Text); TxtStatus.Text = "OK"; }
    private void BtnOpenBrowserRemote_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(TxtRemoteUrl.Text) { UseShellExecute = true });

    private void ChkAlarm_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _settings == null) return;
        _settings.AlarmEnabled = ChkAlarmEnabled.IsChecked == true;
        SettingsManager.Save(_settings);
    }

    private void BtnSaveAlarm_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlarmEnabled = ChkAlarmEnabled.IsChecked == true;
        _settings.AlarmTime = TxtAlarmTime.Text.Trim();
        if (ComboAlarmStation.SelectedItem is Station st)
            _settings.AlarmStationUrl = st.StreamUrl;
        SettingsManager.Save(_settings);
        TxtStatus.Text = $"⏰ {_settings.AlarmTime}";
    }

    private void ChkDvrAutoSplit_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _settings == null) return;
        _settings.DvrAutoSplitTracks = ChkDvrAutoSplit.IsChecked == true;
        _recorder.AutoSplitOnTrackChange = _settings.DvrAutoSplitTracks;
        SettingsManager.Save(_settings);
    }

    private void BtnAddScheduleCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStation == null)
        {
            System.Windows.MessageBox.Show("Najpierw wybierz stację radiową z listy!", "RADYJKO PRO");
            return;
        }

        TimeSpan startT = TimeSpan.TryParse(TxtDvrTime.Text.Trim(), out var parsedT) ? parsedT : new TimeSpan(20, 0, 0);
        int durMin = int.TryParse(TxtDvrDuration.Text.Trim(), out var parsedDur) ? Math.Clamp(parsedDur, 1, 360) : 60;

        string dayTag = (ComboDvrDay.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "EVERYDAY";
        bool everyDay = dayTag == "EVERYDAY";
        DayOfWeek dow = DayOfWeek.Friday;
        if (!everyDay && int.TryParse(dayTag, out int dInt))
            dow = (DayOfWeek)dInt;

        var rec = new ScheduledRecording
        {
            StationName = _currentStation.Name,
            StreamUrl = _currentStation.StreamUrl,
            EveryDay = everyDay,
            Day = dow,
            StartTime = startT,
            DurationMinutes = durMin,
            IsEnabled = true
        };

        _schedules.Add(rec);
        _settings.Schedules = _schedules.ToList();
        SettingsManager.Save(_settings);
    }

    private void BtnDeleteSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (ListSchedules.SelectedItem is ScheduledRecording sr)
        {
            _schedules.Remove(sr);
            _settings.Schedules = _schedules.ToList();
            SettingsManager.Save(_settings);
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        var q = TxtSearchQuery.Text.Trim();
        string country = (ComboSearchCountry.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        string tag = (ComboSearchTag.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        int minBr = int.TryParse((ComboSearchBitrate.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out int br) ? br : 0;

        BtnSearchAction.IsEnabled = false;
        ListSearchResults.ItemsSource = await RadioBrowserApi.SearchStationsAsync(q, country, tag, minBr);
        BtnSearchAction.IsEnabled = true;
    }

    private void TxtSearchQuery_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) BtnSearch_Click(sender, e); }
    private void BtnPlaySearched_Click(object sender, RoutedEventArgs e) { if (ListSearchResults.SelectedItem is Station s) PlayStation(s); }
    private void BtnAddSearchedToFavorites_Click(object sender, RoutedEventArgs e)
    {
        if (ListSearchResults.SelectedItem is Station s)
        {
            string cleanName = s.Name.Replace("🟢 ", "").Replace("🟡 ", "").Trim();
            _favorites.Add(new Station(cleanName, s.StreamUrl, s.Genre, true));
            StationStorage.SaveFavorites(_favorites);
        }
    }
    private void BtnDeleteFavorite_Click(object sender, RoutedEventArgs e) { if (ListFavorites.SelectedItem is Station s) { _favorites.Remove(s); StationStorage.SaveFavorites(_favorites); } }

    private void BtnExportFavorites_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", FileName = "radyjko_stacje_backup.json" };
        if (sfd.ShowDialog() == true)
        {
            File.WriteAllText(sfd.FileName, System.Text.Json.JsonSerializer.Serialize(_favorites, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            System.Windows.MessageBox.Show("Wyeksportowano pomyślnie!", "RADYJKO PRO");
        }
    }

    private void BtnImportFavorites_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json" };
        if (ofd.ShowDialog() == true)
        {
            try
            {
                var imported = System.Text.Json.JsonSerializer.Deserialize<List<Station>>(File.ReadAllText(ofd.FileName));
                if (imported != null)
                {
                    _favorites.Clear();
                    foreach (var s in imported) _favorites.Add(s);
                    StationStorage.SaveFavorites(_favorites);
                    System.Windows.MessageBox.Show("Zaimportowano stacje!", "RADYJKO PRO");
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show("Błąd importu: " + ex.Message); }
        }
    }

    private void ChkDarkTheme_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _settings == null) return;
        _settings.DarkTheme = ChkDarkTheme.IsChecked == true;
        ApplyTheme(_settings.DarkTheme);
        SettingsManager.Save(_settings);
    }

    private void ChkAutostart_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _settings == null) return;
        _settings.AutostartEnabled = ChkAutostart.IsChecked == true;
        UpdateWindowsStartupRegistry(_settings.AutostartEnabled);
        SettingsManager.Save(_settings);
    }

    private void ChkResumeStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _settings == null) return;
        _settings.ResumeOnStartup = ChkResumeStartup.IsChecked == true;
        SettingsManager.Save(_settings);
    }

    private void ChkCoverArt_Changed(object sender, RoutedEventArgs e) { if (_settings != null) { _settings.EnableCoverArt = ChkCoverArt.IsChecked == true; SettingsManager.Save(_settings); } }
    private void ListFavorites_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ListFavorites.SelectedItem is Station s) PlayStation(s); }

    private void ShowAndRestore() { Show(); WindowState = WindowState.Normal; Activate(); }
    protected override void OnStateChanged(EventArgs e) { if (WindowState == WindowState.Minimized && _microWindow == null) Hide(); base.OnStateChanged(e); }
    private void ExitApp() { _timeShiftUiTimer.Stop(); _smtc.Dispose(); _alarmTimer.Stop(); _hotkeys.Dispose(); _microWindow?.Close(); _webRemote.Dispose(); _recorder.Dispose(); _sleepTimer.Dispose(); _discordRpc.Dispose(); _trayIcon?.Dispose(); _player?.Dispose(); System.Windows.Application.Current.Shutdown(); }
    protected override void OnClosed(EventArgs e) { ExitApp(); base.OnClosed(e); }
}
