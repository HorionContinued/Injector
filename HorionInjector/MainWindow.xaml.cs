using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace HorionInjector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private string status;
        private bool done = true;
        private int ticks;

        public MainWindow()
        {
            InitializeComponent();
            VersionLabel.Content = "v" + GetVersion();
            SetConnectionState(ConnectionState.NONE);

            Task.Run(() =>
            {
                while (true)
                {
                    if (done)
                    {
                      status = string.Empty;
                      ticks = 0;
                      Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => InjectButton.Content = "inject"));
                    }
                    else
                    {
                      if (++ticks > 12)
                          ticks = 0;

                      string load = status + ".";
                      if (ticks > 4) load += ".";
                      if (ticks > 8) load += ".";

                      Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => InjectButton.Content = load));
                    }
                    Thread.Sleep(100);
                }
            });

            if (!CheckConnection())
                MessageBox.Show("Couldn't connect to download server. You can still inject a custom DLL.");
        }

        private void SetStatus(string status)
        {
            if (status == "done")
                done = true;
            else
                this.status = status;
        }

        enum ConnectionState { NONE, CONNECTED, DISCONNECTED }
        private void SetConnectionState(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.NONE:
                    ConnectionStateLabel.Content = "Not connected";
                    ConnectionStateLabel.Foreground = System.Windows.Media.Brushes.SlateGray;
                    break;
                case ConnectionState.CONNECTED:
                    ConnectionStateLabel.Content = "Connected";
                    ConnectionStateLabel.Foreground = System.Windows.Media.Brushes.ForestGreen;
                    break;
                case ConnectionState.DISCONNECTED:
                    ConnectionStateLabel.Content = "Disconnected";
                    ConnectionStateLabel.Foreground = System.Windows.Media.Brushes.Coral;
                    break;
            }
        }

        private void InjectButton_Left(object sender, RoutedEventArgs e)
        {
            if (!done) return;
            done = false;
            
            status = "checking connection";
            if (!CheckConnection())
            {
                MessageBox.Show("Can't reach download server.");
                SetStatus("done");
                return;
            }

            status = "downloading DLL";
            var wc = new WebClient();
            var file = Path.Combine(Path.GetTempPath(), "Horion.dll");
            wc.DownloadFileCompleted += (_, __) => Inject(file);
            wc.DownloadFileAsync(new Uri("https://horion.download/bin/Horion.dll"), file);
        }

        private void InjectButton_Right(object sender, MouseButtonEventArgs e)
        {
            if (!done) return;
            done = false;
            
            var diag = new OpenFileDialog();
            diag.Filter = "dll files (*.dll)|*.dll";
            diag.RestoreDirectory = true;

            if (diag.ShowDialog().GetValueOrDefault())
                Inject(diag.FileName);
            else
                done = true;
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

        private string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString(); 
    }
}
