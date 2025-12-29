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
        [ObservableProperty]
        private object _currentView;

        [ObservableProperty]
        private FocusGroup _activeFocusGroup;
        
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
            
            _currentView = _dashboardViewModel;
            
            DriverStateVm = new DriverStateViewModel(_wsClient);

            // 2. 初始化 FocusGroup (必須在設定 ActiveFocusGroup 之前)
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

            // 3. 設定初始焦點
            ActiveFocusGroup = DockFocusGroup;

            // 4. 綁定事件並開始連線 (只做一次，避免重複觸發)
            _wsClient.OnMessageReceived += OnWsMessage;
            _ = _wsClient.ConnectAsync("ws://localhost:8765");
        }

        private void OnWsMessage(string json)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // 判斷訊息類型是否為手勢，並解析
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "gesture")
                    {
                        string gesture = root.GetProperty("gesture").GetString() ?? "";
                        ExecuteGestureAction(gesture);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[UI RECV ERROR] " + ex.Message);
                }
            });
        }

        // 將手勢字串直接映射到 UI 動作 (間接觸發)
        private void ExecuteGestureAction(string gesture)
        {
            switch (gesture)
            {
                case "swipe_left":
                    ActiveFocusGroup = MusicFocusGroup;
                    MusicFocusGroup.Move(-1); // 同鍵盤 Left
                    break;
                    
                case "swipe_right":
                    ActiveFocusGroup = MusicFocusGroup;
                    MusicFocusGroup.Move(1);  // 同鍵盤 Right
                    break;
                    
                case "rotate_counterclockwise":
                    ActiveFocusGroup = DockFocusGroup;
                    DockFocusGroup.Move(-1); // 同鍵盤 Up
                    break;
                    
                case "rotate_clockwise":
                    ActiveFocusGroup = DockFocusGroup;
                    DockFocusGroup.Move(1);  // 同鍵盤 Down
                    break;
                    
                case "push":
                case "tap":
                    ActiveFocusGroup?.Activate(); // 同鍵盤 Enter/Space
                    break;
            }
        }

        public async Task SendCommand(string cmd, bool value)
        {
            var msg = new { type = "command", cmd = cmd, value = value };
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
