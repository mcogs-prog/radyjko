using System;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace RADYJKO;

public class WindowsSmtcService : IDisposable
{
    private MediaPlayer? _mediaPlayer;
    private SystemMediaTransportControls? _smtc;

    public Action? OnPlay;
    public Action? OnPause;
    public Action? OnNext;
    public Action? OnPrevious;

    public void Initialize()
    {
        try
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;
            _smtc = _mediaPlayer.SystemMediaTransportControls;
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;

            _smtc.ButtonPressed += (sender, args) =>
            {
                switch (args.Button)
                {
                    case SystemMediaTransportControlsButton.Play:
                        OnPlay?.Invoke();
                        break;
                    case SystemMediaTransportControlsButton.Pause:
                        OnPause?.Invoke();
                        break;
                    case SystemMediaTransportControlsButton.Next:
                        OnNext?.Invoke();
                        break;
                    case SystemMediaTransportControlsButton.Previous:
                        OnPrevious?.Invoke();
                        break;
                }
            };
        }
        catch { }
    }

    public void UpdateMetadata(string stationName, string trackTitle, bool isPlaying, string? coverUrl)
    {
        if (_smtc == null) return;
        try
        {
            _smtc.PlaybackStatus = isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;

            string artist = stationName;
            string title = string.IsNullOrWhiteSpace(trackTitle) ? "Na żywo • RADYJKO PRO" : trackTitle;
            if (!string.IsNullOrWhiteSpace(trackTitle) && trackTitle.Contains(" - "))
            {
                var parts = trackTitle.Split(new[] { " - " }, 2, StringSplitOptions.None);
                artist = $"{parts[0].Trim()} ({stationName})";
                title = parts[1].Trim();
            }

            updater.MusicProperties.Artist = artist;
            updater.MusicProperties.Title = title;
            updater.MusicProperties.AlbumTitle = "RADYJKO PRO • OGŚ";

            if (!string.IsNullOrWhiteSpace(coverUrl))
            {
                try
                {
                    updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(coverUrl));
                }
                catch { }
            }
            else
            {
                updater.Thumbnail = null;
            }

            updater.Update();
        }
        catch { }
    }

    public void SetStopped()
    {
        if (_smtc == null) return;
        try { _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped; } catch { }
    }

    public void Dispose()
    {
        try
        {
            if (_smtc != null) _smtc.IsEnabled = false;
            _mediaPlayer?.Dispose();
        }
        catch { }
    }
}
