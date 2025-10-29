using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly MainDashboardViewModel _dashboardViewModel = new();
        private readonly GestureDebugViewModel _debugViewModel = new();

        [ObservableProperty]
        private object _currentView;

        public MainWindowViewModel()
        {
            CurrentView = _dashboardViewModel; // dashboard by default
        }

        [RelayCommand]
        private void ToggleView()
        {
            CurrentView = CurrentView == _dashboardViewModel ? _debugViewModel : _dashboardViewModel;
        }

        public void ToggleDebugMode()
        {
            if (CurrentView == _dashboardViewModel)
                CurrentView = _debugViewModel;
            else
                CurrentView = _dashboardViewModel;
        }
    }
}
