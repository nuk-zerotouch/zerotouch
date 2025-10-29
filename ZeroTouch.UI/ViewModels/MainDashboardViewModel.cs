using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainDashboardViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _temperature = "27°C";

        [ObservableProperty]
        private string _weatherDescription = "Sunny";

        [ObservableProperty]
        private string _songTitle = "Feelin' True";

        [ObservableProperty]
        private string _artistName = "Elisa Jones";
    }
}
