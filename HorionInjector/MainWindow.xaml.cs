using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace HorionInjector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private string _status;
        private bool _done = true;
        private int _ticks;

        private ConnectionState _connectionState;
        private readonly ConsoleWindow console = new ConsoleWindow();

        public MainWindow()
        {
            /* Update cleanup */
            var oldPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, "old");
            if (File.Exists(oldPath)) File.Delete(oldPath);

            InitializeComponent();
            VersionLabel.Content = $"v{GetVersion().Major}.{GetVersion().Minor}.{GetVersion().Revision}";
            SetConnectionState(ConnectionState.None);

            Task.Run(() =>
            {
                while (true)
                {
                    if (!_done)
                    {
                        if (++_ticks > 12)
                            _ticks = 0;

                        string load = _status + ".";
                        if (_ticks > 4) load += ".";
                        if (_ticks > 8) load += ".";

                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => InjectButton.Content = load));
                    }
                    Thread.Sleep(100);
                }
            });

            if (!CheckConnection())
            {
                MessageBox.Show("Couldn't connect to download server. You can still inject a custom DLL.");
            }
            else
            {
                CheckForUpdate();
            }
        }

        private void SetStatus(string status)
        {
            if (status == "done")
            {
                _done = true;
                _status = string.Empty;
                _ticks = 0;
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => InjectButton.Content = "inject"));
            }
            else
            {
                _done = false;
                _status = status;
            }
            Console.WriteLine("[Status] " + status);
        }

        enum ConnectionState { None, Connected, Disconnected }
        private void SetConnectionState(ConnectionState state)
        {
            _connectionState = state;
            switch (state)
            {
                case ConnectionState.None:
                    ConnectionStateLabel.Content = "Not connected";
                    ConnectionStateLabel.Foreground = System.Windows.Media.Brushes.White;
                    break;
                case ConnectionState.Connected:
                    ConnectionStateLabel.Content = "Connected";
                    ConnectionStateLabel.Foreground = System.Windows.Media.Brushes.ForestGreen;
                    break;
                case ConnectionState.Disconnected:
                    ConnectionStateLabel.Content = "Disconnected";
                    ConnectionStateLabel.Foreground = System.Windows.Media.Brushes.Coral;
                    break;
            }
        }

        private void InjectButton_Left(object sender, RoutedEventArgs e)
        {
            if (!_done) return;

            SetStatus("checking connection");
            if (!CheckConnection())
            {
                if (MessageBox.Show("Can't reach download server. Try anyways?", null, MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    SetStatus("done");
                    return;
                }
            }

            SetStatus("downloading DLL");
            var wc = new WebClient();
            var file = Path.Combine(Path.GetTempPath(), "Horion.dll");
            wc.DownloadFileCompleted += (_, __) => Inject(file);
            wc.DownloadFileAsync(new Uri("https://horion.download/bin/Horion.dll"), file);
        }

        private void InjectButton_Right(object sender, MouseButtonEventArgs e)
        {
            if (!_done) return;
            
            SetStatus("selecting DLL");
            var diag = new OpenFileDialog
            {
                Filter = "dll files (*.dll)|*.dll",
                RestoreDirectory = true
            };

            if (diag.ShowDialog().GetValueOrDefault())
                Inject(diag.FileName);
            else
                SetStatus("done");
        }

        private void ConsoleButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (console.IsVisible)
                console.Close();
            else
                console.Show();
        }

        private bool CheckConnection()
        {
            try
            {
                Ping ping = new Ping();
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = ping.Send("horion.download", timeout, buffer, pingOptions);
                return reply.Status == IPStatus.Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        private void CloseWindow(object sender, MouseButtonEventArgs e) => Application.Current.Shutdown();
        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();
    }
}
