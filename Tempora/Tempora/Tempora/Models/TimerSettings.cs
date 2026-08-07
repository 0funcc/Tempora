namespace Tempora.Models
{
    public enum AppTheme
    {
        Default = 0,
        Light = 1,
        Dark = 2
    }

    public enum AppBackdrop
    {
        Mica,
        MicaAlt
    }

    public class TimerSettings
    {
        public int FocusDuration { get; set; } = 25;
        public int BreakDuration { get; set; } = 5;
        public int NumberOfBreaks { get; set; } = 5;

        // Flow Mode: cycle focus/break indefinitely, ignoring NumberOfBreaks.
        public bool FlowMode { get; set; }

        // Null means "no preference saved yet" - distinct from an explicit choice of the default.
        public AppTheme? Theme { get; set; }
        public AppBackdrop? Backdrop { get; set; }
    }
}
