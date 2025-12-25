using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroTouch.UI.Services;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private object _currentView;
        
        private readonly AppWebSocketClient _wsClient;
        
        private readonly MainDashboardViewModel _dashboardViewModel;
        private readonly GestureDebugViewModel _debugViewModel;
        
        public DriverStateViewModel DriverStateVm { get; }

        public MainWindowViewModel()
        {
            _wsClient = new AppWebSocketClient();
            
            _dashboardViewModel = new MainDashboardViewModel();
            _debugViewModel = new GestureDebugViewModel(_wsClient);
            
            CurrentView = _dashboardViewModel; // Dashboard by default
            
            DriverStateVm = new DriverStateViewModel(_wsClient);
            
            _ = _wsClient.ConnectAsync("ws://localhost:8765");
            
            _wsClient.OnMessageReceived += OnWsMessage;
        }
        
        private void OnWsMessage(string json)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // gesture / driver_state
                Console.WriteLine("[UI RECV] " + json);
            });
        }
        
        public async Task SendCommand(string cmd, bool value)
        {
            var msg = new
            {
                type = "command",
                cmd = cmd,
                value = value
            };

            var json = JsonSerializer.Serialize(msg);
            await _wsClient.SendAsync(json);
        }


        [RelayCommand]
        private void ToggleView()
        {
            CurrentView = CurrentView == _dashboardViewModel ? _debugViewModel : _dashboardViewModel;
        }

        public async void ToggleDebugMode()
        {
            if (CurrentView == _dashboardViewModel)
            {
                CurrentView = _debugViewModel;
                await SendCommand("set_gesture_debug", true);
            }
            else
            {
                await SendCommand("set_gesture_debug", false);
                CurrentView = _dashboardViewModel;
            }
        }
        
        public async Task OnAppClosingAsync()
        {
            await SendCommand("set_gesture_debug", false);
            await _wsClient.DisconnectAsync();
        }

    }
}
