using System;
using System.IO;
using System.Windows;

namespace RADYJKO;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) => LogCrash(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (s, args) => { LogCrash(args.Exception); args.Handled = true; };
        base.OnStartup(e);
    }
    private static void LogCrash(Exception? ex)
    {
        try
        {
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "radyjko_crash_5_0.txt"), ex?.ToString() ?? "");
        }
        catch { }
    }
}
