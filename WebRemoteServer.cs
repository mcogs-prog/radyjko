using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RADYJKO;

public class WebRemoteServer : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    public int Port { get; set; } = 8080;
    public Func<string>? OnGetStatus;
    public Action? OnTogglePlay;
    public Action? OnNextStation;
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
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address)) return ip.Address.ToString();
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
                    var res = ctx.Response;
                    var path = ctx.Request.Url?.AbsolutePath ?? "/";
                    if (path == "/api/play") { OnTogglePlay?.Invoke(); Respond(res, "OK"); }
                    else if (path == "/api/status") { Respond(res, OnGetStatus?.Invoke() ?? "{}"); }
                    else { Respond(res, "<html><body><h1>RADYJKO Pilot</h1></body></html>", "text/html"); }
                }
            }
            catch { }
        }, token);
    }

    private static void Respond(HttpListenerResponse res, string text, string type = "text/plain")
    {
        var b = Encoding.UTF8.GetBytes(text);
        res.ContentType = type; res.OutputStream.Write(b, 0, b.Length); res.Close();
    }

    public void Stop() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; try { _listener?.Stop(); _listener?.Close(); } catch { } _listener = null; }
    public void Dispose() => Stop();
}
