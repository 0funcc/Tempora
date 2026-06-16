using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Storage;
using Windows.Foundation.Metadata;

namespace Tempora
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly FrameworkElement? _root;
        
        public ViewModels.SettingsViewModel ViewModel { get; }

        public SettingsWindow()
        {
            this.ViewModel = new ViewModels.SettingsViewModel();
            this.InitializeComponent();

            // Retrieve the current app window
            AppWindow appWindow = this.AppWindow;

            // Initialize the presenter properties
            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Set the initial size of the window
                appWindow.Resize(new SizeInt32(800, 750));

                // Set the minimum and maximum preferred size of the window
                presenter.PreferredMinimumHeight = 600;
                presenter.PreferredMinimumWidth = 800;
            }

            // Extend the content to the title bar
            this.ExtendsContentIntoTitleBar = true;

            _root = (FrameworkElement)Content;

            // Load theme and backdrop settings on start
            var settings = Services.SettingsService.Instance;
            if (_root != null)
            {
                _root.RequestedTheme = settings.Theme;
            }

            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                var kind = settings.Backdrop == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base;
                SystemBackdrop = new MicaBackdrop() { Kind = kind };
            }

            // Subscribe to VM visual events to update Window properties
            ViewModel.ThemeChanged += (s, theme) =>
            {
                if (_root != null)
                {
                    _root.RequestedTheme = theme;
                }
            };

            ViewModel.BackdropChanged += (s, kind) =>
            {
                if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
                {
                    SystemBackdrop = new MicaBackdrop() { Kind = kind };
                }
            };
        }
    }
}
