using System;
namespace RADYJKO;
public class DiscordRpcService : IDisposable
{
    public bool IsEnabled { get; set; } = true;
    public async System.Threading.Tasks.Task ConnectAsync() { await System.Threading.Tasks.Task.CompletedTask; }
    public void UpdatePresence(string s, string t, bool p) { }
    public void Dispose() { }
}
