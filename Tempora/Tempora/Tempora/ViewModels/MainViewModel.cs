using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.UI;

namespace Tempora.ViewModels
{
    public class BreakIndicator : INotifyPropertyChanged
    {
        private bool _isFilled;
        public bool IsFilled
        {
            get => _isFilled;
            set
            {
                if (_isFilled != value)
                {
                    _isFilled = value;
                    OnPropertyChanged(nameof(IsFilled));
                    OnPropertyChanged(nameof(FillBrush));
                }
            }
        }

        public Brush? FillBrush
        {
            get
            {
                if (_isFilled)
                {
                    if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColorObj) && accentColorObj is Color color)
                    {
                        return new SolidColorBrush(color);
                    }
                    return new SolidColorBrush(Colors.Blue); // Fallback
                }
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
        private readonly DispatcherTimer _timer;
        private TimeSpan _timeLeft;
        
        private int _focusDuration;
        private int _breakDuration;
        private int _numberOfBreaks;
        private int _breaksLeft;
        private bool _isInFocus;
        private bool _hasSessionStarted;
        private bool _sessionCompleted;
        private string _timerText = "00:00";

        private ObservableCollection<BreakIndicator> _breakIndicators = new();

        public MainViewModel()
        {
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // Load persisted settings
            _focusDuration = Services.SettingsService.Instance.FocusTime;
            _breakDuration = Services.SettingsService.Instance.BreakDuration;
            _numberOfBreaks = Services.SettingsService.Instance.BreakCount;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Elapsed;

            StartCommand = new RelayCommand(StartSession);
            PauseCommand = new RelayCommand(PauseSession);
            StopCommand = new RelayCommand(StopSession);

            ResetSession();
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand PauseCommand { get; }
        public RelayCommand StopCommand { get; }

        public int FocusDuration
        {
            get => _focusDuration;
            set
            {
                if (_focusDuration != value)
                {
                    _focusDuration = value;
                    OnPropertyChanged();
                    UpdateDisplay(TimeSpan.FromMinutes(_focusDuration));
                }
            }
        }

        public int BreakDuration
        {
            get => _breakDuration;
            set
            {
                if (_breakDuration != value)
                {
                    _breakDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        public int NumberOfBreaks
        {
            get => _numberOfBreaks;
            set
            {
                if (_numberOfBreaks != value)
                {
                    _numberOfBreaks = value;
                    OnPropertyChanged();
                    UpdateBreakIndicators();
                }
            }
        }

        public int BreaksLeft
        {
            get => _breaksLeft;
            set
            {
                if (_breaksLeft != value)
                {
                    _breaksLeft = value;
                    OnPropertyChanged();
                    UpdateBreakIndicators();
                }
            }
        }

        public bool IsInFocus
        {
            get => _isInFocus;
            set
            {
                if (_isInFocus != value)
                {
                    _isInFocus = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasSessionStarted
        {
            get => _hasSessionStarted;
            set
            {
                if (_hasSessionStarted != value)
                {
                    _hasSessionStarted = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SessionCompleted
        {
            get => _sessionCompleted;
            set
            {
                if (_sessionCompleted != value)
                {
                    _sessionCompleted = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TimerText
        {
            get => _timerText;
            set
            {
                if (_timerText != value)
                {
                    _timerText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<BreakIndicator> BreakIndicators
        {
            get => _breakIndicators;
            set
            {
                _breakIndicators = value;
                OnPropertyChanged();
            }
        }

        public void StartSession()
        {
            if (!_hasSessionStarted)
            {
                _isInFocus = true;
                BreaksLeft = NumberOfBreaks;
                _timeLeft = TimeSpan.FromMinutes(FocusDuration);
                UpdateDisplay(_timeLeft);
                _timer.Start();
                HasSessionStarted = true;
                UpdateBreakIndicators();
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

        public void PauseSession()
        {
            _timer.Stop();
        }

        public void StopSession()
        {
            _timer.Stop();
            ResetSession();
            ClearBreakIndicators();
            HasSessionStarted = false;
        }

        public void ResetSession()
        {
            _timer.Stop();
            _isInFocus = true;
            BreaksLeft = NumberOfBreaks;
            _timeLeft = TimeSpan.FromMinutes(FocusDuration);
            UpdateDisplay(_timeLeft);
            HasSessionStarted = false;
            SessionCompleted = false;
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
                BreaksLeft--;

                if (HasSessionStarted)
                    ShowToast("Break Time", "Take a short break!");

                UpdateBreakIndicators();
            }
            else if (BreaksLeft > 0)
            {
                _isInFocus = true;
                _timeLeft = TimeSpan.FromMinutes(FocusDuration);
                ShowToast("Focus Time", "Back to work!");
            }
            else
            {
                ShowToast("Session Complete", "You've finished all focus sessions.");
                SessionCompleted = true;
                return;
            }

            UpdateDisplay(_timeLeft);
            _timer.Start();
        }

        private void UpdateDisplay(TimeSpan ts)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                int totalMinutes = (int)ts.TotalMinutes;
                int seconds = ts.Seconds;

                if (totalMinutes >= 60)
                {
                    int hours = totalMinutes / 60;
                    int minutes = totalMinutes % 60;
                    TimerText = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                }
                else
                {
                    TimerText = ts.ToString(@"mm\:ss");
                }
            });
        }

        public void UpdateBreakIndicators()
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _breakIndicators.Clear();
                for (int i = 0; i < NumberOfBreaks; i++)
                {
                    _breakIndicators.Add(new BreakIndicator
                    {
                        IsFilled = i < (NumberOfBreaks - BreaksLeft)
                    });
                }
            });
        }

        public void ClearBreakIndicators()
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _breakIndicators.Clear();
            });
        }

        private void ShowToast(string title, string body)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                var toast = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(body)
                    .BuildNotification();

                AppNotificationManager.Default.Show(toast);
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
