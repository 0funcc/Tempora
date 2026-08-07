using Tempora.Models;
using Windows.Storage;

namespace Tempora.Services
{
    public class SettingsService
    {
        private const string FocusTimeKey = "focusTime";
        private const string BreakDurationKey = "breakDuration";
        private const string BreakCountKey = "breakCount";
        private const string ThemeKey = "theme";
        private const string BackdropKey = "backdrop";
        private const string FlowModeKey = "flowMode";

        public TimerSettings Load()
        {
            var settings = new TimerSettings();
            var values = ApplicationData.Current.LocalSettings.Values;

            if (values.TryGetValue(FocusTimeKey, out var f) && f is double fd)
                settings.FocusDuration = (int)fd;
            if (values.TryGetValue(BreakDurationKey, out var b) && b is double bd)
                settings.BreakDuration = (int)bd;
            if (values.TryGetValue(BreakCountKey, out var c) && c is double cc)
                settings.NumberOfBreaks = (int)cc;
            if (values.TryGetValue(ThemeKey, out var themeRaw) && themeRaw is int savedTheme)
                settings.Theme = (AppTheme)savedTheme;
            if (values.TryGetValue(BackdropKey, out var backdropRaw) && backdropRaw is string savedBackdrop)
                settings.Backdrop = savedBackdrop == "MicaAlt" ? AppBackdrop.MicaAlt : AppBackdrop.Mica;
            if (values.TryGetValue(FlowModeKey, out var flowRaw) && flowRaw is bool flowMode)
                settings.FlowMode = flowMode;

            return settings;
        }

        public void SaveFocusDuration(double minutes) =>
            ApplicationData.Current.LocalSettings.Values[FocusTimeKey] = minutes;

        public void SaveBreakDuration(double minutes) =>
            ApplicationData.Current.LocalSettings.Values[BreakDurationKey] = minutes;

        public void SaveNumberOfBreaks(double count) =>
            ApplicationData.Current.LocalSettings.Values[BreakCountKey] = count;

        public void SaveTheme(AppTheme theme)
        {
            var local = ApplicationData.Current.LocalSettings.Values;
            if (theme == AppTheme.Default)
                local.Remove(ThemeKey);
            else
                local[ThemeKey] = (int)theme;
        }

        public void SaveBackdrop(AppBackdrop backdrop) =>
            ApplicationData.Current.LocalSettings.Values[BackdropKey] = backdrop == AppBackdrop.MicaAlt ? "MicaAlt" : "Mica";

        public void SaveFlowMode(bool enabled) =>
            ApplicationData.Current.LocalSettings.Values[FlowModeKey] = enabled;
    }
}
