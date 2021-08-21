using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        // IMPORTS
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr OpenProcess(IntPtr dwDesiredAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, char[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, ref IntPtr lpThreadId);

        [DllImport("kernel32", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, IntPtr dwFreeType);

        [DllImport("user32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);

        [DllImport("user32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        //

        private string status;
        private bool done = true;
        private int ticks;

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;

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

            if (!checkConnection())
                MessageBox.Show("Couldn't connect to download server. You can still inject a custom DLL.");
        }

        private void InjectButton_Left(object sender, RoutedEventArgs e)
        {
            if (!done) return;
            done = false;
            
            status = "checking connection";
            if (!checkConnection())
            {
                MessageBox.Show("Can't reach download server.");
                done = true;
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

        private void Inject(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show("DLL not found, your Antivirus might have deleted it.");
                done = true;
                return;
            }

            if (File.ReadAllBytes(path).Length < 10)
            {
                MessageBox.Show("DLL broken (Less than 10 bytes)");
                done = true;
                return;
            }


            status = "setting file perms";
            try
            {
                var fileInfo = new FileInfo(path);
                var accessControl = fileInfo.GetAccessControl();
                accessControl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-15-2-1"), FileSystemRights.FullControl, InheritanceFlags.None, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                fileInfo.SetAccessControl(accessControl);
            }
            catch (Exception)
            {
                MessageBox.Show("Could not set permissions, try running the injector as admin.");
                done = true;
                return;
            }

            status = "finding process";
            var processes = Process.GetProcessesByName("Minecraft.Windows");
            if (processes.Length == 0)
            {
                status = "launching minecraft";
                Interaction.Shell("explorer.exe shell:appsFolder\\Microsoft.MinecraftUWP_8wekyb3d8bbwe!App", Wait: false);
                while (processes.Length == 0)
                    processes = Process.GetProcessesByName("Minecraft.Windows");
                Thread.Sleep(100);
            }
            var process = processes.OrderBy(p => p.StartTime).First();

            for (int i = 0; i < process.Modules.Count; i++)
            {
                if (process.Modules[i].FileName == path)
                {
                    MessageBox.Show("Already injected!");
                    done = true;
                    return;
                }
            } 

            status = "injecting into " + process.Id;
            IntPtr handle = OpenProcess((IntPtr)2035711, false, (uint)process.Id);
            if (handle == IntPtr.Zero || !process.Responding)
            {
                MessageBox.Show("Failed to get process handle");
                done = true;
                return;
            }

            IntPtr p1 = VirtualAllocEx(handle, IntPtr.Zero, (uint)(path.Length + 1), 12288U, 64U);
            WriteProcessMemory(handle, p1, path.ToCharArray(), path.Length, out IntPtr p2);
            IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            IntPtr p3 = CreateRemoteThread(handle, IntPtr.Zero, 0U, procAddress, p1, 0U, ref p2);
            if (p3 == IntPtr.Zero)
            {
                MessageBox.Show("Failed to create remote thread");
                done = true;
                return;
            }

            uint n = WaitForSingleObject(p3, 5000);
            if (n == 128L || n == 258L)
            {
                CloseHandle(p3);
            }
            else
            {
                VirtualFreeEx(handle, p1, 0, (IntPtr)32768);
                if (p3 != IntPtr.Zero)
                    CloseHandle(p3);
                if (handle != IntPtr.Zero)
                    CloseHandle(handle);
            }

            IntPtr windowH = FindWindow(null, "Minecraft");
            if (windowH == IntPtr.Zero)
                Console.WriteLine("Couldn't get window handle");
            else
                SetForegroundWindow(windowH);

            status = "injected!";
            done = true;
        }

        private bool checkConnection()
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
    }
}
