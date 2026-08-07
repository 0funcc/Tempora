using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Tempora.Services;

namespace Tempora.ViewModels
{
    public partial class TimerViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly DispatcherTimer _timer;

        private TimeSpan _timeLeft;
        private bool _isInFocus;
        private int _breaksLeft;

        [ObservableProperty]
        private int focusDuration = 25;

        [ObservableProperty]
        private int breakDuration = 5;

        [ObservableProperty]
        private int numberOfBreaks = 5;

        [ObservableProperty]
        private string displayTime = "25:00";

        [ObservableProperty]
        private bool hasSessionStarted;

        [ObservableProperty]
        private bool sessionCompleted;

        public ObservableCollection<bool> BreakIndicators { get; } = new();

        public TimerViewModel(SettingsService settingsService, INotificationService notificationService)
        {
            _settingsService = settingsService;
            _notificationService = notificationService;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;

            LoadSettings();
            ResetSession();
        }

        public void LoadSettings()
        {
            var settings = _settingsService.Load();
            FocusDuration = settings.FocusDuration;
            BreakDuration = settings.BreakDuration;
            NumberOfBreaks = settings.NumberOfBreaks;
        }

        public void ApplySettings(int newFocusDuration, int newBreakDuration, int newNumberOfBreaks)
        {
            FocusDuration = newFocusDuration;
            BreakDuration = newBreakDuration;
            NumberOfBreaks = newNumberOfBreaks;
            ResetSession();
            BreakIndicators.Clear();
        }

        [RelayCommand]
        private void Start()
        {
            if (!HasSessionStarted)
            {
                StartSession();
            }
            else if (SessionCompleted)
            {
                ResetSession();
                StartSession();
            }
            else
            {
                _timer.Start();
            }
        }

        [RelayCommand]
        private void Pause()
        {
            _timer.Stop();
        }

        [RelayCommand]
        private void Stop()
        {
            _timer.Stop();
            ResetSession();
            BreakIndicators.Clear();
            HasSessionStarted = false;
        }

        private void StartSession()
        {
            _isInFocus = true;
            _breaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay();
            _timer.Start();
            HasSessionStarted = true;
            UpdateBreakIndicators();
        }

        public void ResetSession()
        {
            _timer.Stop();
            _isInFocus = true;
            _breaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay();
            HasSessionStarted = false;
        }

        private void OnTimerTick(object? sender, object e)
        {
            if (_timeLeft.TotalSeconds > 0)
            {
                _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
                UpdateDisplay();
                return;
            }

            _timer.Stop();

            if (_isInFocus)
            {
                _isInFocus = false;
                _timeLeft = TimeSpan.FromMinutes(BreakDuration);
                _breaksLeft--;

                if (HasSessionStarted)
                    _notificationService.Show("Break Time", "Take a short break!");

                UpdateBreakIndicators();
            }
            else if (_breaksLeft > 0)
            {
                _isInFocus = true;
                _timeLeft = TimeSpan.FromMinutes(FocusDuration);
                _notificationService.Show("Focus Time", "Back to work!");
            }
            else
            {
                _notificationService.Show("Session Complete", "You've finished all focus sessions.");
                SessionCompleted = true;
                return;
            }

            UpdateDisplay();
            _timer.Start();
        }

        private void UpdateBreakIndicators()
        {
            BreakIndicators.Clear();
            for (int i = 0; i < NumberOfBreaks; i++)
            {
                BreakIndicators.Add(i < (NumberOfBreaks - _breaksLeft));
            }
        }

        private void UpdateDisplay()
        {
            int totalMinutes = (int)_timeLeft.TotalMinutes;
            int seconds = _timeLeft.Seconds;

            // Ensure the timer does not reset after 60 minutes
            DisplayTime = totalMinutes >= 60
                ? $"{totalMinutes / 60:D2}:{totalMinutes % 60:D2}:{seconds:D2}"
                : _timeLeft.ToString(@"mm\:ss");
        }
    }
}
