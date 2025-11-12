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
        [ObservableProperty] private string _temperature = "--°C";
        [ObservableProperty] private string _pop = "--%";
        [ObservableProperty] private string _comfort = "Loading...";

        public MainDashboardViewModel()
        {
            _ = LoadWeatherAsync();
        }

        private async Task LoadWeatherAsync()
        {
            var (condition, temp, pop, comfort) = await _weatherService.GetWeatherAsync(Location);
            WeatherCondition = condition;
            Temperature = temp;
            Pop = pop;
            Comfort = comfort;
        }
    }
}
