using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Timers;
using System.Threading.Tasks;
using ZeroTouch.UI.Navigation;
using ZeroTouch.UI.Services;
using ZeroTouch.Services;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainDashboardViewModel : ViewModelBase
    {
        private readonly Timer _timer;
        private bool _blinkColon = true;
        
        public FocusGroup MusicFocusGroup { get; }

        [ObservableProperty] private string currentTime = string.Empty;
        [ObservableProperty] private bool is24HourFormat = true;

        private readonly WeatherService _weatherService = new();
        private bool _testTempBar = true;
        [ObservableProperty] private string _location = "高雄市";
        [ObservableProperty] private string _weatherCondition = "Loading...";
        [ObservableProperty] private string _pop = "--%";
        [ObservableProperty] private string _comfort = "Loading...";
        [ObservableProperty] private string _weatherIconPath = "avares://ZeroTouch.UI/Assets/Weather/Day/clear.gif";
        [ObservableProperty] private string _minTemperature = "--°";
        [ObservableProperty] private string _maxTemperature = "--°";
        [ObservableProperty] private IBrush _temperatureBarBrush = new SolidColorBrush(Colors.Orange);

        private readonly MusicPlayerService _player;
        [ObservableProperty] private string _currentSong = "No song";
        [ObservableProperty] private long _progress;
        [ObservableProperty] private long _duration;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private bool _isUserInteracting;

        // Page state
        // 0: Settings
        // 1: Dashboard (Home)
        // 2: Phone
        // 3: Maps
        // 4: In-Call
        [ObservableProperty] private int _currentPageIndex = 1;

        // Phone Features
        [ObservableProperty] private string _displayNumber = "";
        [ObservableProperty] private string _callingContactName = "Unknown";
        [ObservableProperty] private string _callStatusText = "Calling...";
        [ObservableProperty] private bool _isMuted = false;
        [ObservableProperty] private bool _isKeypadVisible = false;
        [ObservableProperty] private bool _isSpeakerOn = false;
        
        [ObservableProperty] private bool _isMusicFocused;
        private System.Threading.CancellationTokenSource? _musicFocusCts;

        // Navigation Info, e.g., "Turn Right", "Go Straight"
        [ObservableProperty] private string _navigationInstruction = "Follow Route";

        // Navigation distance, e.g. "in 300 meters"
        [ObservableProperty] private string _navigationDistance = "Calculating...";

        // Navigation Icon
        [ObservableProperty] private string _navigationIcon = "↑";

        // Settings options
        [ObservableProperty] private bool _isDarkTheme = true;
        [ObservableProperty] private bool _isClockBlinking = true;

        [ObservableProperty] private IPageTransition _currentPageTransition;

        [ObservableProperty] private string _previewRouteId = "";
        [ObservableProperty] private string _navigationRouteId = "";
        
        private DateTime _lastPauseTime;
        private readonly TimeSpan _smartPauseThreshold = TimeSpan.FromSeconds(10);

        public FocusGroup RouteFocusGroup { get; }

        [RelayCommand]
        private void GoRoute(string routeName)
        {
            NavigationInstruction = $"Navigating to {routeName}...";
            NavigationDistance = "Calculating...";
            
            NavigationRouteId = string.Empty; 
            NavigationRouteId = routeName;
            
            SoundService.PlaySound("route-start.mp3");
        }

        private readonly IPageTransition _horizontalTransition = new CompositePageTransition
        {
            PageTransitions = new()
            {
                new PageSlide(TimeSpan.FromSeconds(0.5), PageSlide.SlideAxis.Horizontal),
                new CrossFade(TimeSpan.FromSeconds(0.5))
            }
        };

        private readonly IPageTransition _verticalTransition = new CompositePageTransition
        {
            PageTransitions = new()
            {
                new PageSlide(TimeSpan.FromSeconds(0.5), PageSlide.SlideAxis.Vertical),
                new CrossFade(TimeSpan.FromSeconds(0.5))
            }
        };

        public IImage PlayPauseIconPath
        {
            get
            {
                var uri = new Uri(
                    IsPlaying
                        ? "avares://ZeroTouch.UI/Assets/Icons/Dark/pause-button.png"
                        : "avares://ZeroTouch.UI/Assets/Icons/Dark/play-button.png");

                return new Bitmap(AssetLoader.Open(uri));
            }
        }

        partial void OnIsPlayingChanged(bool value)
        {
            OnPropertyChanged(nameof(PlayPauseIconPath));
        }

        public MainDashboardViewModel()
        {
            _timer = new Timer(1000);
            _timer.Elapsed += (_, __) => UpdateTime();
            _timer.Start();
            UpdateTime();

            _ = LoadWeatherAsync();
            _player = new MusicPlayerService();

            _currentPageTransition = _horizontalTransition;

            _player.SetPlaylist(new[]
            {
                @"E:\ZeroTouch\ZeroTouch\ZeroTouch.UI\Assets\Music\Romantic_Inspiration.mp3",
                @"E:\ZeroTouch\ZeroTouch\ZeroTouch.UI\Assets\Music\Lovely_Piano_Song.mp3",
                @"E:\ZeroTouch\ZeroTouch\ZeroTouch.UI\Assets\Music\Pond.mp3",
                @"E:\ZeroTouch\ZeroTouch\ZeroTouch.UI\Assets\Music\Shining_Stars.mp3",
                @"E:\ZeroTouch\ZeroTouch\ZeroTouch.UI\Assets\Music\Nostalgic_Piano.mp3"
            });
            
            MusicFocusGroup = new FocusGroup([
                new FocusItemViewModel(PreviousCommand, "Previous", null, PreviewMusicAction, false),
                new FocusItemViewModel(PlayPauseCommand, "PlayPause", null, PreviewMusicAction, false),
                new FocusItemViewModel(NextCommand, "Next", null, PreviewMusicAction, false)
            ]);

            _player.PositionChanged += (pos, dur) =>
            {
                if (!IsUserInteracting)
                {
                    Progress = pos;
                    Duration = dur;
                }
            };

            _player.PositionChanged += (pos, dur) =>
            {
                Progress = pos;
                Duration = dur;
            };

            RouteFocusGroup = new FocusGroup([
                new FocusItemViewModel(GoRouteCommand, "Home", null, PreviewRoute, true),
                new FocusItemViewModel(GoRouteCommand, "Work", null, PreviewRoute, true),
                new FocusItemViewModel(GoRouteCommand, "Gym", null, PreviewRoute, true),
                new FocusItemViewModel(GoRouteCommand, "School", null, PreviewRoute, true),
                new FocusItemViewModel(GoRouteCommand, "Cinema", null, PreviewRoute, true)
            ]);
        }
        
        private void PreviewMusicAction(object? actionObj)
        {
            if (actionObj is string actionName)
            {
                string soundFile = "";
                switch (actionName)
                {
                    case "Previous":  soundFile = "prev.mp3"; break; 
                    case "Next":      soundFile = "next.mp3"; break;
                }

                if (!string.IsNullOrEmpty(soundFile))
                {
                    SoundService.PlaySound(soundFile);
                }
            }
        }

        private void PreviewRoute(object? routeNameObj)
        {
            if (routeNameObj is string routeName)
            {
                PreviewRouteId = routeName;
                NavigationInstruction = $"Preview: {routeName}"; 
                
                PlayRoutePreviewSound(routeName);
            }
        }
        
        private void PlayRoutePreviewSound(string routeName)
        {
            string soundFile = "";
            
            switch (routeName.ToLower())
            {
                case "home":   soundFile = "route-home.mp3";   break;
                case "work":   soundFile = "route-work.mp3";   break;
                case "gym":    soundFile = "route-gym.mp3";    break;
                case "school": soundFile = "route-school.mp3"; break;
                case "cinema": soundFile = "route-cinema.mp3"; break;
            }

            if (!string.IsNullOrEmpty(soundFile))
            {
                SoundService.PlaySound(soundFile);
            }
        }

        private void UpdateTime()
        {
            var now = DateTime.Now;
            string format = Is24HourFormat ? "HH:mm" : "hh:mm tt";
            var timeText = now.ToString(format);

            if (IsClockBlinking)
            {
                if (!_blinkColon)
                {
                    timeText = timeText.Replace(":", " ");
                }

                _blinkColon = !_blinkColon;
            }

            CurrentTime = timeText;
        }

        private bool IsNight()
        {
            var hour = DateTime.Now.Hour;
            return hour < 6 || hour >= 18;
        }

        private async Task LoadWeatherAsync()
        {
            if (_testTempBar)
            {
                string testMin = "16";
                string testMax = "25";
                string testCondition = "多雲";
                string testPop = "30%";
                string testComfort = "舒適";

                WeatherCondition = testCondition;

                Pop = testPop;
                Comfort = testComfort;
                MinTemperature = $"{testMin}°";
                MaxTemperature = $"{testMax}°";

                WeatherIconPath = GetIconPath(testCondition);

                if (double.TryParse(testMin, out double tMin) &&
                    double.TryParse(testMax, out double tMax))
                {
                    TemperatureBarBrush = CreateTemperatureBrush(tMin, tMax);
                }

                return;
            }

            var (condition, minT, maxT, pop, comfort) = await _weatherService.GetWeatherAsync(Location);
            WeatherCondition = condition;
            MinTemperature = $"{minT}°";
            MaxTemperature = $"{maxT}°";
            Pop = pop;
            Comfort = comfort;

            WeatherIconPath = GetIconPath(condition);

            if (double.TryParse(minT, out var min) &&
                double.TryParse(maxT, out var max))
            {
                TemperatureBarBrush = CreateTemperatureBrush(min, max);
            }
            else
            {
                TemperatureBarBrush = new SolidColorBrush(Colors.Orange);
            }
        }

        private string GetIconPath(string condition)
        {
            string icon = MapConditionToIcon(condition);

            bool night = IsNight();
            string timeFolder = night ? "Night" : "Day";

            string basePath = "avares://ZeroTouch.UI/Assets/Weather";

            string nightPath = $"{basePath}/Night/{icon}";
            string dayPath = $"{basePath}/Day/{icon}";

            if (night && AssetExists(nightPath))
                return nightPath;

            return dayPath;
        }

        private string MapConditionToIcon(string condition)
        {
            condition ??= "";

            if (condition.Contains('雷'))
                return "thunder.gif";

            if (condition.Contains('晴') && condition.Contains("陣雨"))
                return "sunny-isolated-thunderstorms.gif";

            if (condition.Contains('晴') && condition.Contains('雨'))
                return "sunny-isolated-showers.gif";

            if (condition.Contains('晴'))
                return "clear.gif";

            if (condition.Contains("陰短暫雨"))
                return "drizzle.gif";

            if (condition.Contains("局部") && condition.Contains('雨'))
                return "isolated-showers.gif";

            if (condition.Contains("陣雨"))
                return "rainy.gif";

            if (condition.Contains("陰有雨") || condition.Contains('雨'))
                return "rainy.gif";

            if (condition.Contains("多雲時陰"))
                return "mostly-cloudy.gif";

            if (condition.Contains("多雲"))
                return "cloudy.gif";

            if (condition.Contains('陰'))
                return "mostly-cloudy.gif";

            return "clear.gif";
        }

        private bool AssetExists(string uri)
        {
            try
            {
                var stream = AssetLoader.Open(new Uri(uri));
                stream.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            byte lerp(byte x, byte y) => (byte)(x + (y - x) * t);

            return Color.FromArgb(
                lerp(a.A, b.A),
                lerp(a.R, b.R),
                lerp(a.G, b.G),
                lerp(a.B, b.B)
            );
        }

        private static Color GetTemperatureColor(double temp)
        {
            // Cold to hot color scale
            var blue = Color.Parse("#2196F3");      // Very cold
            var cyan = Color.Parse("#26C6DA");      // Pleasantly cool
            var green = Color.Parse("#66BB6A");     // Comfortable
            var yellow = Color.Parse("#FFEB3B");    // Warm
            var orange = Color.Parse("#FF9800");    // Hot
            var red = Color.Parse("#F44336");       // Very Hot

            if (temp <= 10)
                return blue;

            if (temp <= 16)
                return Lerp(blue, cyan, (temp - 10) / (16 - 10));

            if (temp <= 20)
                return Lerp(cyan, green, (temp - 16) / (20 - 16));

            if (temp <= 24)
                return Lerp(green, yellow, (temp - 20) / (24 - 20));

            if (temp <= 28)
                return Lerp(yellow, orange, (temp - 24) / (28 - 24));

            if (temp <= 34)
                return Lerp(orange, red, (temp - 28) / (34 - 28));

            return red;
        }

        private static IBrush CreateTemperatureBrush(double minTemp, double maxTemp)
        {
            var leftColor = GetTemperatureColor(minTemp);
            var rightColor = GetTemperatureColor(maxTemp);
            var middleColor = Lerp(leftColor, rightColor, 0.5);

            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(leftColor, 0.0),
                    new GradientStop(middleColor, 0.5),
                    new GradientStop(rightColor, 1.0)
                ]
            };
        }

        [RelayCommand]
        private void PlayPause()
        {
            _ = TriggerMusicFocusAsync();
            
            if (IsPlaying)
            {
                _player.Pause();
                IsPlaying = false;
                
                _lastPauseTime = DateTime.Now;
                SoundService.PlaySound("pause.mp3");
            }
            else
            {
                _player.Play();
                CurrentSong = _player.CurrentSongName;
                IsPlaying = true;
                
                var timePaused = DateTime.Now - _lastPauseTime;
                
                if (timePaused > _smartPauseThreshold)
                {
                    SoundService.PlaySound("play.mp3");
                }
            }
        }

        [RelayCommand]
        private void Next()
        {
            _ = TriggerMusicFocusAsync();
            SoundService.PlaySound("next.mp3");
            _player.Next();
            CurrentSong = _player.CurrentSongName;
            IsPlaying = true;
        }

        [RelayCommand]
        private void Previous()
        {
            _ = TriggerMusicFocusAsync();
            SoundService.PlaySound("prev.mp3");
            _player.Previous();
            CurrentSong = _player.CurrentSongName;
            IsPlaying = true;
        }

        [RelayCommand]
        private void Seek(long value)
        {
            _player.Seek(value);
        }
        
        private async Task TriggerMusicFocusAsync()
        {
            _musicFocusCts?.Cancel();
            _musicFocusCts = new System.Threading.CancellationTokenSource();
            var token = _musicFocusCts.Token;

            IsMusicFocused = true;

            try
            {
                await Task.Delay(5000, token);
                IsMusicFocused = false;
            }
            catch (TaskCanceledException)
            {
            }
        }

        [RelayCommand]
        private void KeypadTap(string number)
        {
            if (DisplayNumber.Length < 15)
            {
                DisplayNumber += number;
            }
        }

        [RelayCommand]
        private void Backspace()
        {
            if (DisplayNumber.Length > 0)
            {
                DisplayNumber = DisplayNumber.Substring(0, DisplayNumber.Length - 1);
            }
        }

        [RelayCommand]
        private void Call()
        {
            if (!string.IsNullOrEmpty(DisplayNumber))
            {
                StartCall(DisplayNumber);
            }
        }

        [RelayCommand]
        private void CallContact(string contactName)
        {
            StartCall(contactName);
        }

        private void StartCall(string target)
        {
            CallingContactName = target;
            CallStatusText = "Calling...";

            CurrentPageTransition = _verticalTransition;

            CurrentPageIndex = 4;
        }

        [RelayCommand]
        private void HangUp()
        {
            CurrentPageIndex = 2;
            DisplayNumber = "";
        }

        [RelayCommand]
        private void DtmfTap(string key)
        {
            CallStatusText = $"Numbers: {key}";
        }

        [RelayCommand]
        private void ToggleMute() => IsMuted = !IsMuted;

        [RelayCommand]
        private void ToggleKeypad() => IsKeypadVisible = !IsKeypadVisible;

        [RelayCommand]
        private void ToggleSpeaker() => IsSpeakerOn = !IsSpeakerOn;

        [RelayCommand]
        private void ShowHome()
        {
            if (CurrentPageIndex == 0)
            {
                CurrentPageTransition = _horizontalTransition;
            }
            else if (CurrentPageIndex == 2)
            {
                CurrentPageTransition = _verticalTransition;
            }

            CurrentPageIndex = 1;
        }

        [RelayCommand]
        private void ShowSettings()
        {
            CurrentPageTransition = _horizontalTransition;
            CurrentPageIndex = 0;
        }

        [RelayCommand]
        private void ShowPhone()
        {
            CurrentPageTransition = _verticalTransition;
            CurrentPageIndex = 2;
        }

        [RelayCommand]
        private void ShowMaps()
        {
            // Map is now a sub-page of Dashboard.
            // Add new page index if maps becomes an
            // independent page in the future.
            if (CurrentPageIndex == 0) CurrentPageTransition = _horizontalTransition;
            else if (CurrentPageIndex == 2) CurrentPageTransition = _verticalTransition;

            CurrentPageIndex = 3;
        }
    }
}
