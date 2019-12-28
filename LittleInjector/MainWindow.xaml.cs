using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LittleInjector
{
    /// <inheritdoc>
    ///     <cref></cref>
    /// </inheritdoc>
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            AllowsTransparency = true;
            Background = new SolidColorBrush(Colors.Transparent);
            ShowTitleBar = false;
            ShowCloseButton = false;
            ShowMinButton = false;
            ShowMaxRestoreButton = false;
            MouseDown += MainWindow_MouseDown;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttribute, IntPtr dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        public enum DllInjectionResult
        {
            DllNotFound,
            GameProcessNotFound,
            InjectionFailed,
            Success
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            } catch (Exception) { }
        }

        private void FlipViewTest_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FlipView flipview = ((FlipView)sender);
            switch (flipview.SelectedIndex)
            {
                case 0:
                    flipview.BannerText = "Please, select your dll file to inject...";
                    InjectButton.Visibility = Visibility.Collapsed;
                    break;
                case 1:
                    flipview.BannerText = "Exit";
                    break;
            }
        }

        private void FlipViewTest_MouseDown(object sender, MouseButtonEventArgs e)
        {
            FlipView flipview = ((FlipView)sender);
            switch (flipview.SelectedIndex)
            {
                case 0:
                    OpenFileDialog fileDialog = new OpenFileDialog();
                    fileDialog.Filter = "DLL Files (.dll)|*.dll";
                    if (fileDialog.ShowDialog() == true)
                    {
                        flipview.BannerText = fileDialog.FileName;
                        InjectButton.Visibility = Visibility.Visible;
                    }
                    break;
                case 1:
                    Close();
                    break;
            }
        }

        private void InjectButton_Click(object sender, RoutedEventArgs e)
        {
            string dllToInject = FlipViewTest.BannerText;
            new Task(() =>
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    InjectButton.IsEnabled = false;
                    InjectButton.Content = "INJECTING...";
                });

                if (Inject(dllToInject))
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        InjectButton.Content = "INJECTED !";
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        InjectButton.Content = "FAILED TO INJECT";
                    });
                }

                Thread.Sleep(3000);

                Application.Current.Dispatcher.Invoke(delegate
                {
                    InjectButton.Content = "INJECT";
                    InjectButton.IsEnabled = true;
                });
            }).Start();
        }

        private static bool Inject(string dllToInject)
        {
            if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LittleInjector"))
            {
                Directory.Delete(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LittleInjector", true);
                Thread.Sleep(1000);
            }

            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LittleInjector");
            
            if (File.Exists(dllToInject) == false)
            {
                MessageBox.Show("ERROR: DLL file not found", "ERROR");
                return false;
            }

            string newDLL = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LittleInjector\\" + GenerateFileName() + ".dll";
            File.Copy(dllToInject, newDLL);

            byte[] array = new byte[new Random().Next(1, 6969)];
            using (FileStream fileStream = new FileStream(dllToInject, FileMode.Append))
            {
                fileStream.Write(array, 0, array.Length);
            }

            Process[] processes = Process.GetProcesses();
            uint pid = (from process in processes where process.ProcessName == "RDR2" select (uint)process.Id).FirstOrDefault();
            if (pid != 0u) return DoInjection(pid, newDLL);
            MessageBox.Show("ERROR: Red Dead Redemption 2 not found, you need to start the game", "ERROR");
            return false;
        }

        private static bool DoInjection(uint pid, string dllToInject)
        {
            IntPtr pHandle = OpenProcess(1082u, 1, pid);
            if (pHandle == IntPtr.Zero)
            {
                return false;
            }
            IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (procAddress == IntPtr.Zero)
            {
                return false;
            }
            IntPtr baseAddress = VirtualAllocEx(pHandle, (IntPtr)null, (IntPtr)dllToInject.Length, 12288u, 64u);
            if (baseAddress == IntPtr.Zero)
            {
                return false;
            }
            byte[] dllBytes = Encoding.ASCII.GetBytes(dllToInject);
            if (WriteProcessMemory(pHandle, baseAddress, dllBytes, (uint)dllBytes.Length, 0) == 0)
            {
                return false;
            }
            if (CreateRemoteThread(pHandle, (IntPtr)null, IntPtr.Zero, procAddress, baseAddress, 0u, (IntPtr)null) == IntPtr.Zero)
            {
                return false;
            }
            CloseHandle(pHandle);
            return true;
        }

        private static string GenerateFileName()
        {
            Random random = new Random();
            return new string((from s in Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", random.Next(5, 50))
                               select s[random.Next(s.Length)]).ToArray());
        }
    }
}
