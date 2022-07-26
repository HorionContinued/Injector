using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;

namespace HorionInjector
{
    partial class MainWindow
    {
        private bool CheckForUpdate()
        {
            try
            {

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://horion.download/latest");
                req.Timeout = 3000;
                HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream() ?? throw new Exception("Couldn't get version"));
                var latest = sr.ReadToEnd();
                sr.Close();

                if (Version.Parse(latest) > GetVersion())
                {
                    if (MessageBox.Show("New update available! Do you want to update now?", $"Update to v{latest}", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        Update();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Update()
        {
            var path = Assembly.GetExecutingAssembly().Location;

            try
            {
                Directory.GetAccessControl(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Uh oh! The updater has no permission to access the injectors directory!");
                return;
            }

            File.Move(path, Path.ChangeExtension(path, "old"));
            new WebClient().DownloadFile("https://horion.download/bin/HorionInjector.exe", path);
            
            MessageBox.Show("Updater is done! The injector will now restart.");
            Process.Start(path);
            Application.Current.Shutdown();
        }
    }
}