using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using ZeroTouch.UI.Services;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainDashboardViewModel : ViewModelBase
    {
        private readonly WeatherService _weatherService = new();

        [ObservableProperty] private string _location = "高雄市";
        [ObservableProperty] private string _weatherCondition = "Loading...";
        [ObservableProperty] private string _pop = "--%";
        [ObservableProperty] private string _comfort = "Loading...";
        [ObservableProperty] private string _weatherIconPath = "avares://ZeroTouch.UI/Assets/Icons/Dark/rainy.gif";
        [ObservableProperty] private string _minTemperature = "--°";
        [ObservableProperty] private string _maxTemperature = "--°";


        public MainDashboardViewModel()
        {
            _ = LoadWeatherAsync();
        }

        private async Task LoadWeatherAsync()
        {
            var (condition, minT, maxT, pop, comfort) = await _weatherService.GetWeatherAsync(Location);
            WeatherCondition = condition;
            MinTemperature = $"{minT}°";
            MaxTemperature = $"{maxT}°";
            Pop = pop;
            Comfort = comfort;
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
    }
}
