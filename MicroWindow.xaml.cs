using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
namespace RADYJKO;
public partial class MicroWindow : Window
{
    private readonly MainWindow _mainWindow;
    public MicroWindow(MainWindow main) { InitializeComponent(); _mainWindow = main; var area = SystemParameters.WorkArea; Left = area.Right - Width - 20; Top = area.Bottom - Height - 20; }
    public void UpdateInfo(string s, string t, BitmapImage? c) { Dispatcher.Invoke(() => { TxtStation.Text = s; TxtTrack.Text = string.IsNullOrWhiteSpace(t) ? "Na Żywo" : t; ImgCoverArt.Source = c; }); }
    private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e) { _mainWindow.RestoreFromMicroMode(); }
}
