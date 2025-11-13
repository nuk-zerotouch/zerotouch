using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using ZeroTouch.UI.Services;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainDashboardViewModel : ViewModelBase
    {
        private readonly WeatherService _weatherService = new();

        private bool _testTempBar = false;

        [ObservableProperty] private string _location = "高雄市";
        [ObservableProperty] private string _weatherCondition = "Loading...";
        [ObservableProperty] private string _pop = "--%";
        [ObservableProperty] private string _comfort = "Loading...";
        [ObservableProperty] private string _weatherIconPath = "avares://ZeroTouch.UI/Assets/Icons/Dark/rainy.gif";
        [ObservableProperty] private string _minTemperature = "--°";
        [ObservableProperty] private string _maxTemperature = "--°";
        [ObservableProperty] private IBrush _temperatureBarBrush = new SolidColorBrush(Colors.Orange);


        public MainDashboardViewModel()
        {
            _ = LoadWeatherAsync();
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
            if (condition.Contains("晴"))
                return "avares://ZeroTouch.UI/Assets/Icons/Dark/sunny.svg";
            if (condition.Contains("雨"))
                return "avares://ZeroTouch.UI/Assets/Icons/Dark/rainy-1.svg";
            if (condition.Contains("陰"))
                return "avares://ZeroTouch.UI/Assets/Icons/Dark/cloudy.svg";
            return "avares://ZeroTouch.UI/Assets/Icons/Dark/rainy.gif";
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
                GradientStops = new GradientStops
        {
            new GradientStop(leftColor,   0.0),
            new GradientStop(middleColor,0.5),
            new GradientStop(rightColor,  1.0),
        }
            };
        }

    }
}
