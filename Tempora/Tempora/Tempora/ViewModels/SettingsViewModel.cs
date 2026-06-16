using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation.Metadata;

namespace Tempora.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public double FocusTime
        {
            get => Services.SettingsService.Instance.FocusTime;
            set
            {
                if (Services.SettingsService.Instance.FocusTime != (int)value)
                {
                    Services.SettingsService.Instance.FocusTime = (int)value;
                    OnPropertyChanged();
                    var mainVM = App.MainViewModelInstance;
                    if (mainVM != null)
                    {
                        mainVM.FocusDuration = (int)value;
                        mainVM.ResetSession();
                    }
                }
            }
        }

        public double BreakDuration
        {
            get => Services.SettingsService.Instance.BreakDuration;
            set
            {
                if (Services.SettingsService.Instance.BreakDuration != (int)value)
                {
                    Services.SettingsService.Instance.BreakDuration = (int)value;
                    OnPropertyChanged();
                    var mainVM = App.MainViewModelInstance;
                    if (mainVM != null)
                    {
                        mainVM.BreakDuration = (int)value;
                        mainVM.ResetSession();
                    }
                }
            }
        }

        public double NumberOfBreaks
        {
            get => Services.SettingsService.Instance.BreakCount;
            set
            {
                if (Services.SettingsService.Instance.BreakCount != (int)value)
                {
                    Services.SettingsService.Instance.BreakCount = (int)value;
                    OnPropertyChanged();
                    var mainVM = App.MainViewModelInstance;
                    if (mainVM != null)
                    {
                        mainVM.NumberOfBreaks = (int)value;
                        mainVM.ResetSession();
                    }
                }
            }
        }

        public string ThemeSelection
        {
            get => Services.SettingsService.Instance.Theme switch
            {
                ElementTheme.Light => "Light",
                ElementTheme.Dark => "Dark",
                _ => "Use system setting"
            };
            set
            {
                var newTheme = value switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                if (Services.SettingsService.Instance.Theme != newTheme)
                {
                    Services.SettingsService.Instance.Theme = newTheme;
                    OnPropertyChanged();
                    ApplyTheme(newTheme);
                }
            }
        }

        public string BackdropSelection
        {
            get => Services.SettingsService.Instance.Backdrop;
            set
            {
                if (Services.SettingsService.Instance.Backdrop != value)
                {
                    Services.SettingsService.Instance.Backdrop = value;
                    OnPropertyChanged();
                    ApplyBackdrop(value);
                }
            }
        }

        public string VersionText
        {
            get
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
        }

        private void ApplyTheme(ElementTheme theme)
        {
            if (App.MainWindowInstance?.Content is FrameworkElement mainRoot)
            {
                mainRoot.RequestedTheme = theme;
            }
            ThemeChanged?.Invoke(this, theme);
        }

        private void ApplyBackdrop(string backdropName)
        {
            var kind = backdropName == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base;
            
            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
            {
                if (App.MainWindowInstance is Window main)
                {
                    main.SystemBackdrop = new MicaBackdrop() { Kind = kind };
                }
            }
            BackdropChanged?.Invoke(this, kind);
        }

        public event EventHandler<ElementTheme>? ThemeChanged;
        public event EventHandler<MicaKind>? BackdropChanged;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
