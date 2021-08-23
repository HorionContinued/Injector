using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualBasic;

namespace HorionInjector
{
    partial class MainWindow
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

        private void Inject(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show("DLL not found, your Antivirus might have deleted it.");
                goto done;
            }

            if (File.ReadAllBytes(path).Length < 10)
            {
                MessageBox.Show("DLL broken (Less than 10 bytes)");
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
                MessageBox.Show("Could not set permissions, try running the injector as admin.");
                goto done;
            }

            SetStatus("finding process");
            var processes = Process.GetProcessesByName("Minecraft.Windows");
            if (processes.Length == 0)
            {
                SetStatus("launching minecraft");
                if (Interaction.Shell("explorer.exe shell:appsFolder\\Microsoft.MinecraftUWP_8wekyb3d8bbwe!App", Wait: false) == 0)
                {
                    MessageBox.Show("Failed to launch Minecraft (Is it installed?)");
                    goto done;
                }

                Task.Run(() =>
                {
                    int t = 0;
                    while (processes.Length == 0)
                    {
                        if (++t > 200)
                        {
                            MessageBox.Show("Minecraft launch took too long.");
                            return;
                        }

                        processes = Process.GetProcessesByName("Minecraft.Windows");
                        Thread.Sleep(10);
                    }
                    Thread.Sleep(100);
                }).Wait();
            }
            var process = processes.OrderBy(p => p.StartTime).First();

            for (int i = 0; i < process.Modules.Count; i++)
            {
                if (process.Modules[i].FileName == path)
                {
                    MessageBox.Show("Already injected!");
                    goto done;
                }
            }

            SetStatus("injecting into " + process.Id);
            IntPtr handle = OpenProcess((IntPtr)2035711, false, (uint)process.Id);
            if (handle == IntPtr.Zero || !process.Responding)
            {
                MessageBox.Show("Failed to get process handle");
                goto done;
            }

            IntPtr p1 = VirtualAllocEx(handle, IntPtr.Zero, (uint)(path.Length + 1), 12288U, 64U);
            WriteProcessMemory(handle, p1, path.ToCharArray(), path.Length, out IntPtr p2);
            IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            IntPtr p3 = CreateRemoteThread(handle, IntPtr.Zero, 0U, procAddress, p1, 0U, ref p2);
            if (p3 == IntPtr.Zero)
            {
                MessageBox.Show("Failed to create remote thread");
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
                Console.WriteLine("Couldn't get window handle");
            else
                SetForegroundWindow(windowH);

            done: SetStatus("done");
        }
    }
}
