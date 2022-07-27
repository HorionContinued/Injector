using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualBasic;

namespace HorionInjector
{
    partial class MainWindow
    {
        // IMPORTS
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(IntPtr dwDesiredAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, char[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, ref IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, IntPtr dwFreeType);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        //

        private void Inject(string path)
        {
            SetStatus("checking installation");
            if(!CheckGame())
                goto done;
            
            if (!File.Exists(path))
            {
                SetStatus("DLL not found, your Antivirus might have deleted it.", true);
                goto done;
            }

            if (File.ReadAllBytes(path).Length < 10)
            {
                SetStatus("DLL broken (Less than 10 bytes)", true);
                goto done;
            }

            SetStatus("setting file perms");
            try
            {
                var fileInfo = new FileInfo(path);
                var accessControl = fileInfo.GetAccessControl();
                accessControl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-15-2-1"), FileSystemRights.FullControl, InheritanceFlags.None, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                fileInfo.SetAccessControl(accessControl);
            }
            catch (Exception)
            {
                SetStatus("Could not set permissions, try running the injector as admin.", true);
                goto done;
            }

            SetStatus("finding process");
            var processes = Process.GetProcessesByName("Minecraft.Windows");
            if (processes.Length == 0)
            {
                SetStatus("launching minecraft");
                if (Interaction.Shell("explorer.exe shell:appsFolder\\Microsoft.MinecraftUWP_8wekyb3d8bbwe!App", Wait: false) == 0)
                {
                    SetStatus("Failed to launch Minecraft for you. Please try starting it manually.", true);
                    goto done;
                }

                int t = 0;
                while (processes.Length == 0)
                {
                    if (++t > 200)
                    {
                        SetStatus("Minecraft launch took too long.", true);
                        return;
                    }

                    processes = Process.GetProcessesByName("Minecraft.Windows");
                    Thread.Sleep(10);
                }
                Thread.Sleep(3000);
            }
            var process = processes.First(p => p.Responding);

            for (int i = 0; i < process.Modules.Count; i++)
            {
                if (process.Modules[i].FileName == path)
                {
                    SetStatus("Already injected!", true);
                    goto done;
                }
            }

            SetStatus("injecting into " + process.Id);
            IntPtr handle = OpenProcess((IntPtr)2035711, false, (uint)process.Id);
            if (handle == IntPtr.Zero || !process.Responding)
            {
                SetStatus("Failed to get process handle", true);
                goto done;
            }

            IntPtr p1 = VirtualAllocEx(handle, IntPtr.Zero, (uint)(path.Length + 1), 12288U, 64U);
            WriteProcessMemory(handle, p1, path.ToCharArray(), path.Length, out IntPtr p2);
            IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            IntPtr p3 = CreateRemoteThread(handle, IntPtr.Zero, 0U, procAddress, p1, 0U, ref p2);
            if (p3 == IntPtr.Zero)
            {
                SetStatus("Failed to create remote thread", true);
                goto done;
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
                SetStatus("Couldn't get window handle");
            else
                SetForegroundWindow(windowH);

            done: SetStatus("done");
        }

        private bool CheckGame()
        {
            var psQuery = new Process
            {
                StartInfo = new ProcessStartInfo("powershell.exe", "Get-AppxPackage Microsoft.MinecraftUWP | Select -ExpandProperty Version")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            psQuery.Start();
            string version = psQuery.StandardOutput.ReadToEnd().Trim();
            psQuery.WaitForExit();
            psQuery.Close();

            if (version == string.Empty)
            {
                SetStatus("Minecraft does not appear to be installed. Please make sure you have the Windows edition of the game installed.", true);
                return false;
            }

            if (!Regex.IsMatch(version, Configuration.supportedVersions))
            {
                return MessageBox.Show($"Your Minecraft version ({version}) isn't supported by the latest Horion version. You can still try to inject it, but your game might crash. Continue?", "Unsupported Minecraft version", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            }

            return true;
        }
    }
}
