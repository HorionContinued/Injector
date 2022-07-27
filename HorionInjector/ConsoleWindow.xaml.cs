using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HorionInjector
{
    /// <summary>
    /// Interaction logic for ConsoleWindow.xaml
    /// </summary>
    public partial class ConsoleWindow
    {
        private bool _stayOnTop;

        public ConsoleWindow()
        {
            InitializeComponent();
        }

        public void Log(string status) => Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => AppendLine($"[Injector] {status}")));

        private void AppendLine(string text) => LogBox.AppendText((LogBox.Text.Length != 0 ? Environment.NewLine : "") + text);

        private void Pin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _stayOnTop = !_stayOnTop;
            Pin.Opacity = _stayOnTop ? 1 : 0.6;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = _stayOnTop;
        }

        private void LogBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => LogBox.ScrollToEnd();

        private void SendButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => SendInput();

        private void Input_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                e.Handled = true;
                SendInput();
            }
        }

        private void SendInput()
        {
            InputBox.Clear();
        }
    }
}
