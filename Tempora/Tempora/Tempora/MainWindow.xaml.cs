using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Security.Cryptography.Core;
using Windows.UI;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Foundation.Metadata;
using Tempora.Models;
using Tempora.Services;
using WinRT.Interop;

namespace Tempora
{
    public sealed partial class MainWindow : Window
    {
        // Win32 interop: keeps the window always-on-top and swallows the
        // double-click-to-maximize gesture on the extended title bar's drag
        // region, without affecting the ability to drag the window.
        private const int GWLP_WNDPROC = -4;
        private const uint WM_NCLBUTTONDBLCLK = 0x00A3;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private IntPtr _hwnd;
        private IntPtr _originalWndProc;
        private WndProcDelegate? _wndProc;

        private SettingsWindow? _settingsWindow;
        private readonly SettingsService _settingsService = new();
        private readonly TimerSettings _settings = new();
        private bool _hasSessionStarted = false;
        private bool _sessionCompleted = false;

        public TimerSettings Settings => _settings;

        public int FocusDuration
        {
            get => _settings.FocusDuration;
            set
            {
                _settings.FocusDuration = value;
                // e.g. format as minutes:seconds
                DispatcherQueue.TryEnqueue(() =>
                {
                    timer.Text = $"{value:D2}:00";
                });
            }
        }
        public int BreakDuration
        {
            get => _settings.BreakDuration;
            set => _settings.BreakDuration = value;
        }
        public int NumberOfBreaks
        {
            get => _settings.NumberOfBreaks;
            set => _settings.NumberOfBreaks = value;
        }
        public bool FlowMode
        {
            get => _settings.FlowMode;
            set => _settings.FlowMode = value;
        }

        public MainWindow()
        {
            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;

            _hwnd = WindowNative.GetWindowHandle(this);

            // Keep the window pinned above other windows, like a utility/tool window
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            // Swallow double-click-to-maximize on the drag region while leaving
            // single-click-drag (a different message) untouched
            _wndProc = WndProc;
            _originalWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _wndProc);

            // Retreive the current app window
            AppWindow appWindow = this.AppWindow;

            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Set the intial size of the window
                appWindow.Resize(new SizeInt32(350, 350));

                // Set the minimum and maximum preferred size of the window
                presenter.PreferredMinimumHeight = 350;
                presenter.PreferredMinimumWidth = 350;
                presenter.PreferredMaximumHeight = 650;
                presenter.PreferredMaximumWidth = 650;

                // Disable the option to maximise the window
                presenter.IsMaximizable = false;
            }

            var loaded = _settingsService.Load();
            FocusDuration = loaded.FocusDuration;
            BreakDuration = loaded.BreakDuration;
            NumberOfBreaks = loaded.NumberOfBreaks;
            FlowMode = loaded.FlowMode;

            // Apply theme, but only if a preference was actually saved
            if (loaded.Theme is AppTheme savedTheme)
            {
                _settings.Theme = savedTheme;
                if (Content is FrameworkElement root)
                {
                    root.RequestedTheme = (ElementTheme)savedTheme;
                }
            }

            // Apply backdrop, but only if a preference was actually saved
            if (loaded.Backdrop is AppBackdrop savedBackdrop)
            {
                _settings.Backdrop = savedBackdrop;
                if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
                {
                    var kind = savedBackdrop == AppBackdrop.MicaAlt ? MicaKind.BaseAlt : MicaKind.Base;
                    SystemBackdrop = new MicaBackdrop() { Kind = kind };
                }
            }

            SetupTimer(FocusDuration, BreakDuration, NumberOfBreaks);
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Elapsed;
            ((FrameworkElement)Content).Loaded += (_, _) => ResetSession();
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                return IntPtr.Zero;
            }

            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }

        // TIMER LOGIC
        private DispatcherTimer _timer;
        private TimeSpan _timeLeft;
        private bool _isInFocus;
        private int _breaksLeft;

        private void SetupTimer(int focusMins, int breakMins, int breaks)
        {
            FocusDuration = focusMins;
        }

        private void StartSession()
        {
            _isInFocus = true;
            _breaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay(_timeLeft);
            _timer.Start();
            _hasSessionStarted = true;
            UpdateBreakIndicators();
        }

        public void ResetSession()
        {
            _timer.Stop();
            _isInFocus = true;
            _breaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay(_timeLeft);
            _hasSessionStarted = false;
        }

        private void Timer_Elapsed(object? sender, object e)
        {
            if (_timeLeft.TotalSeconds > 0)
            {
                _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
                UpdateDisplay(_timeLeft);
                return;
            }

            _timer.Stop();

            if (_isInFocus)
            {
                _isInFocus = false;
                _timeLeft = TimeSpan.FromMinutes(BreakDuration);

                if (!FlowMode)
                    _breaksLeft--;

                if (_hasSessionStarted)
                    ShowToast("Break Time", "Take a short break!");

                UpdateBreakIndicators();
            }
            else if (FlowMode || _breaksLeft > 0)
            {
                _isInFocus = true;
                _timeLeft = TimeSpan.FromMinutes(FocusDuration);
                ShowToast("Focus Time", "Back to work!");
            }
            else
            {
                ShowToast("Session Complete", "You've finished all focus sessions.");
                _sessionCompleted = true;
                return;
            }

            UpdateDisplay(_timeLeft);
            _timer.Start();
        }


        private void UpdateBreakIndicators()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var accentColor = (Color)Application.Current.Resources["SystemAccentColor"];
                var accentBrush = new SolidColorBrush(accentColor);

                breakIndicatorPanel.Children.Clear();

                if (FlowMode)
                {
                    breakIndicatorPanel.Children.Add(new TextBlock
                    {
                        Text = "∞",
                        FontSize = 22,
                        Foreground = accentBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    return;
                }

                for (int i = 0; i < NumberOfBreaks; i++)
                {
                    var ellipse = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Stroke = accentBrush,
                        StrokeThickness = 1,
                        Margin = new Thickness(2),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    if (i < (NumberOfBreaks - _breaksLeft))
                    {
                        ellipse.Fill = accentBrush;
                    }

                    breakIndicatorPanel.Children.Add(ellipse);
                }
            });
        }



        public void ClearBreakIndicators()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                breakIndicatorPanel.Children.Clear();
            });
        }

        private void ShowToast(string title, string body)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var toast = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .BuildNotification();

                AppNotificationManager.Default.Show(toast); // COM-bound, needs UI thread
            });
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSessionStarted)
            {
                StartSession();
            }
            else if (_sessionCompleted)
            {
                ResetSession();
                StartSession();
            }
            else
            {
                _timer.Start();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            ResetSession();
            breakIndicatorPanel.Children.Clear();
            _hasSessionStarted = false;
        }

        private void UpdateDisplay(TimeSpan ts)
        {
            // Because Timer_Elapsed runs on a worker thread, marshal back to UI:
            this.DispatcherQueue.TryEnqueue(() =>
            {
                // Ensure the timer does not reset after 60 minutes
                int totalMinutes = (int)ts.TotalMinutes;
                int seconds = ts.Seconds;

                // Format the timer as hours:minutes:seconds if it exceeds 60 minutes
                if (totalMinutes >= 60)
                {
                    int hours = totalMinutes / 60;
                    int minutes = totalMinutes % 60;
                    timer.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                }
                else
                {
                    timer.Text = ts.ToString(@"mm\:ss");
                }
            });
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
