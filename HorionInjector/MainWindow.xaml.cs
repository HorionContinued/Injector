using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
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
        private int _adRefCooldown;

        private readonly bool _network;

        private ConnectionState _connectionState;
        private static readonly ConsoleWindow Console = new ConsoleWindow();

        public MainWindow()
        {
            /* Update cleanup */
            var oldPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, "old");
            if (File.Exists(oldPath)) File.Delete(oldPath);

            InitializeComponent();
            SetupAdView();

            VersionLabel.Content = $"v{GetVersion().Major}.{GetVersion().Minor}.{GetVersion().Build}";
            SetConnectionState(ConnectionState.None);

            _network = CheckForUpdate();
            if (!_network)
            {
                MessageBox.Show("Couldn't connect to download server. You can still inject a custom DLL.");
            }

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

                    if (IsShowingAds() && ++_adRefCooldown > 1200)
                    {
                        _adRefCooldown -= new Random().Next(1200, 1800); // Cycle every 2-3 minutes
                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(RefreshAd));
                    }
                }
            });
        }

        private void SetStatus(string status, bool popup = false)
        {
            Console.Log(status);

            if (popup)
            {
                MessageBox.Show(status);
                return;
            }


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
        }

        internal enum ConnectionState { None, Connected, Disconnected }
        internal ConnectionState GetConnectionState() => _connectionState;
        internal void SetConnectionState(ConnectionState state)
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
            if (!_network)
            {
                if (MessageBox.Show("Can't reach download server. Try anyways?", null, MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    SetStatus("done");
                    return;
                }
            }

            SetStatus("downloading DLL");
            var file = Path.Combine(Path.GetTempPath(), "Horion.dll");
            using (var wc = new WebClient())
            {
                wc.DownloadFileCompleted += (_, __) => { Task.Run(() => Inject(file)); SetAds(); };
                wc.DownloadFileAsync(new Uri("https://horion.download/bin/Horion.dll"), file);
            }
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
                Task.Run(() => Inject(diag.FileName));
            else
                SetStatus("done");
        }

        private void ConsoleButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (Console.IsVisible)
                Console.Close();
            else
                Console.Show();
        }

        private void ResetConfig(object sender, MouseButtonEventArgs e)
        {
            if(MessageBox.Show("Do you really want to reset your Horion config? (This will delete all your modules settings, keybinds, friends, etc.)", "Reset config", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Packages\Microsoft.MinecraftUWP_8wekyb3d8bbwe\RoamingState\Horion"), true);
            }
        }

        private async void SetupAdView()
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "WebView2UDF"));
            await AdView.EnsureCoreWebView2Async(env);
        }

        private void AdView_InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            AdView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            AdView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AdView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            AdView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.0.0 Safari/537.36";
            
            AdView.CoreWebView2.DOMContentLoaded += (o, args) => AdView.ExecuteScriptAsync("document.body.style.overflow='hidden'");

            AdView.CoreWebView2.NewWindowRequested += (o, args) =>
            {
                args.Handled = true;
                Process.Start(args.Uri);
            };
            AdView.CoreWebView2.NavigationStarting += (o, args) =>
            {
                if (!args.Uri.Contains("horion.download"))
                {
                    Process.Start(args.Uri);
                    args.Cancel = true;
                }
            };
        }

        private void SetAds(bool enable = true)
        {
            if (!Configuration.showAds || !_network)
                return;

            if(enable) RefreshAd();
            Logo.Visibility = enable ? Visibility.Hidden : Visibility.Visible;
            AdView.Visibility = enable ? Visibility.Visible : Visibility.Hidden;
            AdLabel.Visibility = enable ? Visibility.Visible : Visibility.Hidden;
        }

        private void RefreshAd() => AdView.CoreWebView2.Navigate("https://horion.download/atlas.html");

        private bool IsShowingAds() => AdView.Visibility == Visibility.Visible;

        private void ShowAdNotice(object sender, MouseButtonEventArgs e) => MessageBox.Show(
            "Ads help us pay the bills and allow you to use all of Horions features for free. " +
            "If you don't want to support the development, you can always just use a different injector and download the DLL manually at horion.download/dll!",
            "More info about ads"
        );

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        private void CloseWindow(object sender, MouseButtonEventArgs e) => Close();
        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();
        private void MinimizeWindow(object sender, MouseButtonEventArgs e) => this.WindowState = WindowState.Minimized;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            AdView.Dispose();
            Application.Current.Shutdown();
        }
    }
}
