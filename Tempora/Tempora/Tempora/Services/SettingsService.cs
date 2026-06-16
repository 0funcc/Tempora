using Windows.Storage;
using Microsoft.UI.Xaml;

namespace Tempora.Services
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private const string FocusTimeKey = "focusTime";
        private const string BreakDurationKey = "breakDuration";
        private const string BreakCountKey = "breakCount";
        private const string ThemeKey = "theme";
        private const string BackdropKey = "backdrop";

        private SettingsService() { }

        public int FocusTime
        {
            get => ReadIntSetting(FocusTimeKey, 25);
            set => WriteSetting(FocusTimeKey, (double)value);
        }

        public int BreakDuration
        {
            get => ReadIntSetting(BreakDurationKey, 5);
            set => WriteSetting(BreakDurationKey, (double)value);
        }

        public int BreakCount
        {
            get => ReadIntSetting(BreakCountKey, 5);
            set => WriteSetting(BreakCountKey, (double)value);
        }

        public ElementTheme Theme
        {
            get
            {
                var val = ReadIntSetting(ThemeKey, (int)ElementTheme.Default);
                return (ElementTheme)val;
            }
            set
            {
                if (value == ElementTheme.Default)
                {
                    RemoveSetting(ThemeKey);
                }
                else
                {
                    WriteSetting(ThemeKey, (int)value);
                }
            }
        }

        public string Backdrop
        {
            get
            {
                var local = ApplicationData.Current.LocalSettings.Values;
                return local.TryGetValue(BackdropKey, out var v) && v is string s ? s : "Mica";
            }
            set => WriteSetting(BackdropKey, value);
        }

        private int ReadIntSetting(string key, int fallback)
        {
            var local = ApplicationData.Current.LocalSettings.Values;
            if (local.TryGetValue(key, out var val))
            {
                if (val is double d)
                    return (int)d;
                if (val is int i)
                    return i;
            }
            return fallback;
        }

        private void WriteSetting(string key, object value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }

        private void RemoveSetting(string key)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(key);
        }
    }
}
