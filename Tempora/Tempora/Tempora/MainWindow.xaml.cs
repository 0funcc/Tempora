using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.UI;
using WinRT.Interop;

namespace Tempora
{
    public sealed partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;
        
        public ViewModels.MainViewModel ViewModel { get; }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate? _wndProc;
        private IntPtr _oldWndProc;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        private const int GWLP_WNDPROC = -4;
        private const uint WM_NCLBUTTONDBLCLK = 0x00A3;

        private void SubclassWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _wndProc = WndProc;
            var procPtr = Marshal.GetFunctionPointerForDelegate(_wndProc);
            _oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, procPtr);

            if (_oldWndProc == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SubclassWindow failed: {err}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
                return IntPtr.Zero; // swallow the double-click
            return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
        }

        public MainWindow()
        {
            this.ViewModel = App.MainViewModelInstance ?? throw new InvalidOperationException("MainViewModel is not initialized.");
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;

            // Retrieve the current app window
            AppWindow appWindow = this.AppWindow;

            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Set the initial size of the window
                appWindow.Resize(new SizeInt32(350, 350));

                // Set the minimum and maximum preferred size of the window
                presenter.PreferredMinimumHeight = 350;
                presenter.PreferredMinimumWidth = 350;
                presenter.PreferredMaximumHeight = 650;
                presenter.PreferredMaximumWidth = 650;

                // Disable the option to maximize/resize the window
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
            }

            var settings = Services.SettingsService.Instance;

            // Load theme settings
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = settings.Theme;
            }

            // Load backdrop settings
            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                var kind = settings.Backdrop == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base;
                SystemBackdrop = new MicaBackdrop() { Kind = kind };
            }

            ((FrameworkElement)Content).Loaded += (_, _) =>
            {
                SubclassWindow();
                ViewModel.ResetSession();
            };
        }

        // SETTINGS WINDOW
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
                _settingsWindow.Activate();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private void Button_PointerEntered(object sender, RoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "PointerOver");
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "Normal");
        }
    }
}
