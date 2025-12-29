using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroTouch.UI.Services;
using ZeroTouch.UI.Navigation;

namespace ZeroTouch.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty] private object _currentView;

        [ObservableProperty] private FocusGroup _activeFocusGroup;

        public FocusGroup DockFocusGroup { get; }
        public FocusGroup MusicFocusGroup { get; }

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

            ActiveFocusGroup = DockFocusGroup;

            DockFocusGroup = new FocusGroup([
                new FocusItemViewModel(_dashboardViewModel.ShowHomeCommand),
                new FocusItemViewModel(_dashboardViewModel.ShowPhoneCommand),
                new FocusItemViewModel(_dashboardViewModel.ShowMapsCommand),
                new FocusItemViewModel(_dashboardViewModel.ShowSettingsCommand)
            ]);

            MusicFocusGroup = new FocusGroup([
                new FocusItemViewModel(_dashboardViewModel.PreviousCommand),
                new FocusItemViewModel(_dashboardViewModel.PlayPauseCommand),
                new FocusItemViewModel(_dashboardViewModel.NextCommand)
            ]);
        }

        private void OnWsMessage(string json)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("type", out var typeProp) &&
                        typeProp.GetString() == "gesture")
                    {
                        if (root.TryGetProperty("gesture", out var gestureProp))
                        {
                            string gestureName = gestureProp.GetString();
                            Console.WriteLine($"[GESTURE] {gestureName}"); // Debug

                            HandleGesture(gestureName);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[UI RECV] " + json);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"JSON Parse Error: {ex.Message}");
                }
            });
        }

        private void HandleGesture(string gesture)
        {
            // if (CurrentView != _dashboardViewModel) return;

            bool isMapPage = _dashboardViewModel.CurrentPageIndex == 3;

            switch (gesture)
            {
                case "swipe_up":
                case "down2up":
                    if (ActiveFocusGroup == _dashboardViewModel.RouteFocusGroup)
                    {
                        _dashboardViewModel.RouteFocusGroup.Move(-1);
                    }
                    else
                    {
                        ActiveFocusGroup = DockFocusGroup;
                        DockFocusGroup.Move(-1);
                    }
                    break;

                case "swipe_down":
                case "up2down":
                    if (ActiveFocusGroup == _dashboardViewModel.RouteFocusGroup)
                    {
                        _dashboardViewModel.RouteFocusGroup.Move(1);
                    }
                    else
                    {
                        ActiveFocusGroup = DockFocusGroup;
                        DockFocusGroup.Move(1);
                    }
                    break;

                case "swipe_left":
                case "right2left":
                    if (isMapPage)
                    {
                        if (ActiveFocusGroup == _dashboardViewModel.RouteFocusGroup)
                        {
                            ActiveFocusGroup = DockFocusGroup;
                        }
                    }
                    else
                    {
                        ActiveFocusGroup = MusicFocusGroup;
                        MusicFocusGroup.Move(-1);
                    }
                    break;

                case "swipe_right":
                case "left2right":
                    if (isMapPage)
                    {
                        if (ActiveFocusGroup == DockFocusGroup)
                        {
                            ActiveFocusGroup = _dashboardViewModel.RouteFocusGroup;
                        }
                    }
                    else
                    {
                        ActiveFocusGroup = MusicFocusGroup;
                        MusicFocusGroup.Move(1);
                    }
                    break;

                case "push":
                case "tap":
                    ActiveFocusGroup?.Activate();
                    break;

                case "rotate_clockwise":
                    break;
            }
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
