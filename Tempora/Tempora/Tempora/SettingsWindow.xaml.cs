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
using Windows.Foundation.Metadata;
using Tempora.Models;
using Tempora.Services;

namespace Tempora
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService = new();
        private readonly FrameworkElement? _root;

        public SettingsWindow()
        {
            this.InitializeComponent();

            // Retreive the current app window
            AppWindow appWindow = this.AppWindow;

            // Initialize the presenter properties
            if (appWindow != null && appWindow.Presenter is OverlappedPresenter presenter)
            {
                // Set the intial size of the window
                appWindow.Resize(new SizeInt32(800, 750));

                // Set the minimum and maximum preferred size of the window
                presenter.PreferredMinimumHeight = 600;
                presenter.PreferredMinimumWidth = 800;
            }

            // Extend the content to the title bar
            this.ExtendsContentIntoTitleBar = true;

            _root = (FrameworkElement)Content;

            // On open, apply whatever was saved last time
            var loaded = _settingsService.Load();

            if (loaded.Theme is AppTheme savedTheme)
            {
                _root.RequestedTheme = (ElementTheme)savedTheme;
            }

            // Pre-select the ComboBox to match that theme
            if (themeSelector.SelectedIndex < 0)
            {
                themeSelector.SelectedItem = _root.RequestedTheme switch
                {
                    ElementTheme.Light => "Light",
                    ElementTheme.Dark => "Dark",
                    _ => "Use system setting"
                };
            }

            // No saved backdrop preference defaults to plain Mica
            var backdrop = loaded.Backdrop ?? AppBackdrop.Mica;
            var kind = backdrop == AppBackdrop.MicaAlt ? MicaKind.BaseAlt : MicaKind.Base;
            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                SystemBackdrop = new MicaBackdrop() { Kind = kind };
            }

            // PRE-SELECT the ComboBox to match
            if (backdropSelector.SelectedIndex < 0)
            {
                backdropSelector.SelectedItem = backdrop == AppBackdrop.MicaAlt ? "MicaAlt" : "Mica";
            }

            focusTime.Value = loaded.FocusDuration;
            breakTime.Value = loaded.BreakDuration;
            numberOfBreaks.Value = loaded.NumberOfBreaks;
            flowModeToggle.IsOn = loaded.FlowMode;
            numberOfBreaks.IsEnabled = !loaded.FlowMode;

            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            versionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

        }

        private void ThemeSelector_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the default theme based on the current system theme
            if (themeSelector.SelectedIndex < 0)
            {
                var root = (FrameworkElement)Content;
                themeSelector.SelectedItem = root.RequestedTheme switch
                {
                    ElementTheme.Light => "Light",
                    ElementTheme.Dark => "Dark",
                    _ => "Use system setting"
                };
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (themeSelector.SelectedItem is not string pick)
                return;

            // Figure out the new theme
            var newTheme = pick switch
            {
                "Light" => AppTheme.Light,
                "Dark" => AppTheme.Dark,
                "Use system setting" => AppTheme.Default,
                _ => AppTheme.Default
            };

            // Apply it to this window's root
            if (_root != null)
            {
                _root.RequestedTheme = (ElementTheme)newTheme;
            }

            // Also apply it to MainWindow (and PillWindow, if it's open)
            if (App.MainWindowInstance is MainWindow main)
            {
                main.ApplyTheme((ElementTheme)newTheme);
            }

            // Persist for next launch
            _settingsService.SaveTheme(newTheme);
        }

        private void BackdropSelector_Loaded(object sender, RoutedEventArgs e)
        {
            if (backdropSelector.SelectedIndex < 0)
                return;
        }

        private void BackdropSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (backdropSelector.SelectedItem is not string pick)
                return;

            // 1) choose the backdrop
            var backdrop = pick == "MicaAlt" ? AppBackdrop.MicaAlt : AppBackdrop.Mica;
            var kind = backdrop == AppBackdrop.MicaAlt ? MicaKind.BaseAlt : MicaKind.Base;

            // 2) apply to this window
            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                SystemBackdrop = new MicaBackdrop() { Kind = kind };
            }

            // 3) also apply to MainWindow (and PillWindow, if it's open)
            if (App.MainWindowInstance is MainWindow main)
            {
                main.ApplyBackdrop(kind);
            }

            // 4) persist choice
            _settingsService.SaveBackdrop(backdrop);
        }

        // TIMER RELATED SETTINGS
        private void FocusTime_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
        {
            _settingsService.SaveFocusDuration(e.NewValue);
            if (App.MainWindowInstance is MainWindow main)
            {
                main.FocusDuration = (int)e.NewValue;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }

        private void BreakTime_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
        {
            _settingsService.SaveBreakDuration(e.NewValue);
            if (App.MainWindowInstance is MainWindow main)
            {
                main.BreakDuration = (int)e.NewValue;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }

        private void NumberOfBreaks_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
        {
            _settingsService.SaveNumberOfBreaks(e.NewValue);
            if (App.MainWindowInstance is MainWindow main)
            {
                main.NumberOfBreaks = (int)e.NewValue;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }

        private void FlowModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var enabled = flowModeToggle.IsOn;
            _settingsService.SaveFlowMode(enabled);
            numberOfBreaks.IsEnabled = !enabled;

            if (App.MainWindowInstance is MainWindow main)
            {
                main.FlowMode = enabled;
                main.ResetSession();
                main.ClearBreakIndicators();
            }
        }
    }
}
