using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RADYJKO;

public class WebRemoteServer : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    public int Port { get; set; } = 8080;

    public Func<object>? OnGetFullState;
    public Action? OnTogglePlay;
    public Action? OnNextStation;
    public Action? OnPrevStation;
    public Action<int>? OnSelectStationIndex;
    public Action<float>? OnSetVolume;

    public string GetLocalIpAddress()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                            return ip.Address.ToString();
                    }
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Task.Run(async () =>
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Start();
                while (!token.IsCancellationRequested)
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(ctx), token);
                }
            }
            catch { }
        }, token);
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var res = ctx.Response;
            if (!SecurityManager.IsPrivateLanAddress(req.RemoteEndPoint?.Address))
            {
                res.StatusCode = 403;
                res.Close();
                return;
            }
            res.Headers.Add("X-Content-Type-Options", "nosniff");
            res.Headers.Add("X-Frame-Options", "DENY");
            var path = req.Url?.AbsolutePath ?? "/";

            if (path == "/api/state")
            {
                var state = OnGetFullState?.Invoke() ?? new { };
                Respond(res, JsonSerializer.Serialize(state), "application/json");
            }
            else if (path == "/api/play")
            {
                OnTogglePlay?.Invoke();
                Respond(res, "OK");
            }
            else if (path == "/api/next")
            {
                OnNextStation?.Invoke();
                Respond(res, "OK");
            }
            else if (path == "/api/prev")
            {
                OnPrevStation?.Invoke();
                Respond(res, "OK");
            }
            else if (path == "/api/station")
            {
                if (int.TryParse(req.QueryString["idx"], out int idx))
                    OnSelectStationIndex?.Invoke(idx);
                Respond(res, "OK");
            }
            else if (path == "/api/volume")
            {
                if (float.TryParse(req.QueryString["val"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float vol))
                    OnSetVolume?.Invoke(Math.Clamp(vol, 0f, 1f));
                Respond(res, "OK");
            }
            else
            {
                Respond(res, GetMobileWebUiHtml(), "text/html; charset=utf-8");
            }
        }
        catch { }
    }

    private static string GetMobileWebUiHtml() => @"<!DOCTYPE html>
<html lang='pl'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
<title>RADYJKO PRO • Pilot Wi-Fi</title>
<style>
  :root { --bg: #0D0D10; --card: #16161D; --accent: #38BDF8; --lime: #10B981; --text: #FAFAFA; --muted: #94A3B8; }
  * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
  body { background: var(--bg); color: var(--text); padding: 16px; max-width: 480px; margin: 0 auto; }
  .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
  .brand { font-weight: 800; font-size: 18px; color: var(--accent); letter-spacing: 0.5px; }
  .badge { background: #0284C7; color: #fff; font-size: 11px; padding: 2px 7px; border-radius: 6px; font-weight: 700; }
  .now-card { background: var(--card); border: 1px solid #242430; border-radius: 16px; padding: 16px; text-align: center; margin-bottom: 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.4); }
  .cover { width: 140px; height: 140px; border-radius: 14px; object-fit: cover; background: #1E1E28; margin: 0 auto 12px; display: block; border: 1px solid #2D2D3A; }
  .station { font-size: 20px; font-weight: 700; margin-bottom: 4px; color: #fff; }
  .track { font-size: 13px; color: var(--accent); min-height: 18px; margin-bottom: 14px; }
  .controls { display: flex; justify-content: center; gap: 14px; margin-bottom: 14px; }
  button { background: #22222C; color: #fff; border: none; border-radius: 12px; padding: 12px 18px; font-size: 15px; font-weight: 600; cursor: pointer; transition: 0.15s; }
  button:active { transform: scale(0.96); }
  .btn-play { background: var(--accent); color: #09090B; min-width: 120px; font-weight: 800; }
  .vol-row { display: flex; align-items: center; gap: 10px; padding: 6px 4px; }
  input[type=range] { flex: 1; accent-color: var(--accent); height: 6px; }
  .section-title { font-size: 13px; font-weight: 700; color: var(--muted); text-transform: uppercase; letter-spacing: 0.8px; margin: 14px 0 8px; }
  .station-list { display: flex; flex-direction: column; gap: 8px; }
  .st-item { background: var(--card); border: 1px solid #22222C; border-radius: 12px; padding: 12px 14px; display: flex; justify-content: space-between; align-items: center; cursor: pointer; }
  .st-item.active { border-color: var(--accent); background: #142230; }
  .st-name { font-weight: 600; font-size: 15px; }
  .st-genre { font-size: 11px; color: var(--muted); }
  .footer { text-align: center; font-size: 11px; color: #52525B; margin-top: 20px; }
</style>
</head>
<body>
  <div class='header'>
    <div class='brand'>📻 RADYJKO <span class='badge'>PRO 5.0.0 FINAL</span></div>
    <div id='statusText' style='font-size:12px;color:var(--lime);'>● Połączono</div>
  </div>

  <div class='now-card'>
    <img id='coverImg' class='cover' src='' alt='' onerror=""this.style.opacity='0.2'"">
    <div id='stationName' class='station'>Ładowanie...</div>
    <div id='trackName' class='track'>-</div>
    <div class='controls'>
      <button onclick=""cmd('/api/prev')"">⏮</button>
      <button id='playBtn' class='btn-play' onclick=""cmd('/api/play')"">▶ / ⏸</button>
      <button onclick=""cmd('/api/next')"">⏭</button>
    </div>
    <div class='vol-row'>
      <span>🔈</span>
      <input id='volSlider' type='range' min='0' max='1' step='0.02' value='0.75' oninput='setVol(this.value)'>
      <span id='volVal' style='font-size:12px;width:36px;text-align:right;'>75%</span>
    </div>
  </div>

  <div class='section-title'>Ulubione Stacje</div>
  <div id='stationsBox' class='station-list'></div>
  <div class='footer'>RADYJKO PRO • OGŚ 2026</div>

<script>
  let draggingVol = false;
  const volSlider = document.getElementById('volSlider');
  volSlider.addEventListener('touchstart', () => draggingVol = true);
  volSlider.addEventListener('touchend', () => draggingVol = false);
  volSlider.addEventListener('mousedown', () => draggingVol = true);
  volSlider.addEventListener('mouseup', () => draggingVol = false);

  async function cmd(url) { await fetch(url); setTimeout(refresh, 250); }
  async function setVol(v) {
    document.getElementById('volVal').innerText = Math.round(v * 100) + '%';
    await fetch('/api/volume?val=' + v);
  }
  async function refresh() {
    try {
      const r = await fetch('/api/state');
      const d = await r.json();
      document.getElementById('stationName').innerText = d.station || 'Wybierz stację';
      document.getElementById('trackName').innerText = d.track || 'Na żywo';
      document.getElementById('playBtn').innerText = d.playing ? '⏸ Pauza' : '▶ Graj';
      if (d.coverUrl) {
        const img = document.getElementById('coverImg');
        if (img.src !== d.coverUrl) { img.src = d.coverUrl; img.style.opacity = '1'; }
      }
      if (!draggingVol && typeof d.volume === 'number') {
        volSlider.value = d.volume;
        document.getElementById('volVal').innerText = Math.round(d.volume * 100) + '%';
      }
      const box = document.getElementById('stationsBox');
      if (d.stations && box.children.length !== d.stations.length) {
        box.innerHTML = '';
        d.stations.forEach((st, idx) => {
          const div = document.createElement('div');
          div.className = 'st-item' + (st.name === d.station ? ' active' : '');
          div.innerHTML = `<span class='st-name'>${st.name}</span><span class='st-genre'>${st.genre || ''}</span>`;
          div.onclick = () => cmd('/api/station?idx=' + idx);
          box.appendChild(div);
        });
      } else if (d.stations) {
        Array.from(box.children).forEach((el, idx) => {
          el.className = 'st-item' + (d.stations[idx].name === d.station ? ' active' : '');
        });
      }
    } catch(e) {}
  }
  refresh();
  setInterval(refresh, 2500);
</script>
</body>
</html>";

    private static void Respond(HttpListenerResponse res, string text, string type = "text/plain")
    {
        var b = Encoding.UTF8.GetBytes(text);
        res.ContentType = type;
        res.OutputStream.Write(b, 0, b.Length);
        res.Close();
    }

    public void Stop() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; try { _listener?.Stop(); _listener?.Close(); } catch { } _listener = null; }
    public void Dispose() => Stop();
}
