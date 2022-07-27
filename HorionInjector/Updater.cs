using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using Newtonsoft.Json;

namespace HorionInjector
{
    partial class MainWindow
    {
        public static InjectorConfiguration Configuration;
        public struct InjectorConfiguration
        {
            public string latest;
            public bool showAds;
            public string supportedVersions;
        }

        private bool CheckForUpdate()
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://horion.download/config");
                req.Timeout = 3000;
                HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream() ?? throw new Exception("Couldn't get version"));
                Configuration = JsonConvert.DeserializeObject<InjectorConfiguration>(sr.ReadToEnd());
                sr.Close();

                if (Version.Parse(Configuration.latest) > GetVersion())
                {
                    if (MessageBox.Show("New update available! Do you want to update now?", $"Update to v{Configuration.latest}", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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