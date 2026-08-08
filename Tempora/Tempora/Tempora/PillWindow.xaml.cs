using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace Tempora
{
    // A dedicated compact window, configured borderless *before it's ever shown*.
    // Earlier this was implemented by toggling MainWindow's own border/title-bar
    // styles at runtime, which left a persistent 1px frame line - a known, documented
    // WinUI3 limitation with windows that had a title bar removed mid-lifetime.
    // Creating a separate window that never had one avoids that transition entirely.
    public sealed partial class PillWindow : Window
    {
        private const int PillWidth = 180;
        private const int PillHeight = 70;
        private const int PillExpandedHeight = 160;
        private const int HoverAnimationMs = 180;
        private const int HoverPollMs = 150;

        // Win32 interop: always-on-top, double-click-to-exit, defensive
        // WM_NCCALCSIZE override (see comment at its use site), and DWM
        // rounded-corner / border-color styling.
        private const int GWLP_WNDPROC = -4;
        private const uint WM_NCLBUTTONDBLCLK = 0x00A3;
        private const uint WM_NCCALCSIZE = 0x0083;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000;
        private const long WS_THICKFRAME = 0x00040000;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

        private readonly MainWindow _owner;
        private IntPtr _hwnd;
        private IntPtr _originalWndProc;
        private WndProcDelegate? _wndProc;
        private OverlappedPresenter? _presenter;

        private bool _isExpanded;
        private bool _isPillVisible;
        private bool _pillContentLoaded;
        private DispatcherTimer? _hoverAnimTimer;
        private DispatcherTimer? _hoverPollTimer;

        public PillWindow(MainWindow owner)
        {
            _owner = owner;

            this.InitializeComponent();

            _hwnd = WindowNative.GetWindowHandle(this);

            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            _wndProc = WndProc;
            _originalWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _wndProc);

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                _presenter = presenter;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            // Belt-and-suspenders alongside SetBorderAndTitleBar: strip the style
            // bits directly too, since the higher-level API alone left a residual
            // frame line when tried on an existing (previously bordered) window.
            var style = GetWindowLongPtr(_hwnd, GWL_STYLE).ToInt64();
            SetWindowLongPtr(_hwnd, GWL_STYLE, (IntPtr)(style & ~WS_CAPTION & ~WS_THICKFRAME));
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            AppWindow.Resize(new SizeInt32(PillWidth, PillHeight));

            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            int noBorder = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, ref noBorder, sizeof(int));

            // Mica composition depends on DWM's frame-extension machinery, which
            // stripping WS_CAPTION/WS_THICKFRAME above otherwise leaves unconfigured -
            // without this, Mica fails to connect and the window renders solid black.
            // A margin of -1 on all sides tells DWM to extend the frame across the
            // entire client area.
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // Own instance, not a shared reference to owner.SystemBackdrop - a backdrop
            // object connects to exactly one window's compositor target internally,
            // so two windows sharing the same instance causes an intermittent
            // COM-level conflict between them.
            if (owner.SystemBackdrop is MicaBackdrop ownerMica)
            {
                SystemBackdrop = new MicaBackdrop { Kind = ownerMica.Kind };
            }

            if (Content is FrameworkElement root && owner.Content is FrameworkElement ownerRoot)
            {
                root.RequestedTheme = ownerRoot.RequestedTheme;

                // The very first time this window is shown, its visual tree isn't
                // guaranteed to be fully realized within a single dispatcher tick -
                // Loaded is a real signal that it's safe to call TransformToVisual,
                // rather than a timing guess.
                root.Loaded += (s, e) => _pillContentLoaded = true;
            }
        }

        public void MoveTo(PointInt32 position) => AppWindow.Move(position);

        public PointInt32 CurrentPosition => AppWindow.Position;

        public void ShowPill()
        {
            _isPillVisible = true;
            _isExpanded = false;
            PillExpandContainer.Height = 0;

            AppWindow.Show();
            this.Activate();

            if (_pillContentLoaded)
            {
                DispatcherQueue.TryEnqueue(UpdateDragRegion);
            }
            else if (Content is FrameworkElement root)
            {
                root.Loaded += OnFirstLoadedForDragRegion;
            }

            StartHoverPolling();
        }

        private void OnFirstLoadedForDragRegion(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)sender).Loaded -= OnFirstLoadedForDragRegion;
            DispatcherQueue.TryEnqueue(UpdateDragRegion);
        }

        public void SetDisplayText(string text) => timer.Text = text;

        public void ApplyTheme(ElementTheme theme)
        {
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }
        }

        public void ApplyBackdrop(MicaKind kind) => SystemBackdrop = new MicaBackdrop { Kind = kind };

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                ExitToMainWindow();
                return IntPtr.Zero;
            }

            if (msg == WM_NCCALCSIZE && wParam != IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e) => _owner.StartOrResume();

        private void PauseButton_Click(object sender, RoutedEventArgs e) => _owner.Pause();

        private void StopButton_Click(object sender, RoutedEventArgs e) => _owner.Stop();

        private void ExitPillModeButton_Click(object sender, RoutedEventArgs e) => ExitToMainWindow();

        private void ExitToMainWindow()
        {
            // Set first: a deferred callback (e.g. the DispatcherQueue.TryEnqueue in
            // ShowPill) can still be pending when this runs, and it needs to see this
            // as false so it skips touching elements in a window that's about to hide.
            _isPillVisible = false;

            StopHoverPolling();
            _hoverAnimTimer?.Stop();

            _owner.RestoreFromPill(AppWindow.Position);
            AppWindow.Hide();
        }

        private static bool IsCursorOverWindow(IntPtr hWnd)
        {
            if (!GetCursorPos(out var cursor) || !GetWindowRect(hWnd, out var rect))
                return false;

            return cursor.X >= rect.Left && cursor.X < rect.Right
                && cursor.Y >= rect.Top && cursor.Y < rect.Bottom;
        }

        // Polls actual cursor position against the actual window rect on a fixed
        // cadence rather than relying on WM_NCMOUSEMOVE/WM_NCMOUSELEAVE delivery,
        // which is unreliable across the mix of Caption (drag) and Passthrough
        // (button) regions this window has.
        private void StartHoverPolling()
        {
            _hoverPollTimer?.Stop();
            _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoverPollMs) };
            _hoverPollTimer.Tick += (s, e) =>
            {
                bool isOver = IsCursorOverWindow(_hwnd);
                if (isOver && !_isExpanded)
                {
                    ExpandPill();
                }
                else if (!isOver && _isExpanded)
                {
                    CollapsePill();
                }
            };
            _hoverPollTimer.Start();
        }

        private void StopHoverPolling()
        {
            _hoverPollTimer?.Stop();
            _hoverPollTimer = null;
        }

        private void ExpandPill()
        {
            if (_isExpanded)
                return;

            _isExpanded = true;
            AnimateHeight(PillExpandedHeight, UpdateDragRegion);
        }

        private void CollapsePill()
        {
            if (!_isExpanded)
                return;

            _isExpanded = false;
            AnimateHeight(PillHeight, UpdateDragRegion);
        }

        private void AnimateHeight(int targetHeight, Action? onComplete = null)
        {
            _hoverAnimTimer?.Stop();

            int startHeight = AppWindow.Size.Height;
            if (startHeight == targetHeight)
            {
                SyncContentHeight(targetHeight);
                onComplete?.Invoke();
                return;
            }

            var start = DateTime.UtcNow;
            _hoverAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _hoverAnimTimer.Tick += (s, e) =>
            {
                double t = (DateTime.UtcNow - start).TotalMilliseconds / HoverAnimationMs;
                if (t >= 1.0)
                {
                    _hoverAnimTimer?.Stop();
                    AppWindow.Resize(new SizeInt32(PillWidth, targetHeight));
                    SyncContentHeight(targetHeight);
                    onComplete?.Invoke();
                    return;
                }

                double eased = 1 - Math.Pow(1 - t, 3);
                int currentHeight = startHeight + (int)((targetHeight - startHeight) * eased);
                AppWindow.Resize(new SizeInt32(PillWidth, currentHeight));
                SyncContentHeight(currentHeight);
            };
            _hoverAnimTimer.Start();
        }

        // Keeps the revealed content's height exactly in step with how much extra
        // window height currently exists beyond the base pill size, at every frame,
        // so the timer text never has to jump to re-center mid-animation.
        private void SyncContentHeight(int windowHeight)
        {
            double contentHeight = Math.Max(0, windowHeight - PillHeight);
            PillExpandContainer.Height = Math.Min(contentHeight, PillExpandedHeight - PillHeight);
        }

        private void UpdateDragRegion()
        {
            if (!_isPillVisible)
                return;

            try
            {
                var nonClientSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
                int currentHeight = _isExpanded ? PillExpandedHeight : PillHeight;

                nonClientSource.SetRegionRects(NonClientRegionKind.Caption,
                    new[] { new RectInt32(0, 0, PillWidth, currentHeight) });

                var passthroughRects = new List<RectInt32> { GetElementRectInPhysicalPixels(ExitPillModeButton) };

                if (_isExpanded)
                {
                    passthroughRects.Add(GetElementRectInPhysicalPixels(StartButton));
                    passthroughRects.Add(GetElementRectInPhysicalPixels(PauseButton));
                    passthroughRects.Add(GetElementRectInPhysicalPixels(StopButton));
                }

                nonClientSource.SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects.ToArray());
            }
            catch (COMException)
            {
                // Belt-and-suspenders: if the visual tree still isn't ready for
                // TransformToVisual for some other transient reason, skip this pass
                // rather than crash - it'll be retried on the next hover expand/collapse.
            }
        }

        private static RectInt32 GetElementRectInPhysicalPixels(FrameworkElement element)
        {
            var transform = element.TransformToVisual(null);
            var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var scale = element.XamlRoot?.RasterizationScale ?? 1.0;

            return new RectInt32(
                (int)(bounds.X * scale),
                (int)(bounds.Y * scale),
                (int)(bounds.Width * scale),
                (int)(bounds.Height * scale));
        }
    }
}
