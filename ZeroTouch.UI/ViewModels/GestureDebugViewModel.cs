using ZeroTouch.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ZeroTouch.UI.ViewModels
{
    public partial class GestureDebugViewModel : ViewModelBase
    {
        private readonly AppWebSocketClient _ws;

        public GestureDebugViewModel(AppWebSocketClient ws)
        {
            _ws = ws;
            ws.OnMessageReceived += HandleGestureMessage;

            _ws.OnConnectionStatusChanged += status =>
            {
                Dispatcher.UIThread.Post(() => ConnectionStatus = status);
            };
        }

        [ObservableProperty] private string _connectionStatus = "Disconnected";

        [ObservableProperty] private string _lastGesture = "None";

        [ObservableProperty] private double _confidence = 0.0;

        private void HandleGestureMessage(string message)
        {
            try
            {
                var json = JsonDocument.Parse(message);

                if (!json.RootElement.TryGetProperty("type", out var t) || t.GetString() != "gesture")
                    return;
                
                var gesture = json.RootElement.TryGetProperty("gesture", out var g)
                    ? (g.GetString() ?? "unknown")
                    : "unknown";

                var confidence = json.RootElement.TryGetProperty("confidence", out var c)
                    ? c.GetDouble()
                    : 0.0;
                
                Dispatcher.UIThread.Post(() =>
                {
                    LastGesture = gesture;
                    Confidence = confidence;
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() => LastGesture = "(invalid data)");
            }
        }

        [RelayCommand]
        private async Task ConnectToBackendAsync()
        {
            Dispatcher.UIThread.Post(() => ConnectionStatus = "Connecting...");
            await _ws.ConnectAsync("ws://localhost:8765");
        }

        [RelayCommand]
        private async Task DisconnectAsync()
        {
            await _ws.DisconnectAsync();
        }
    }
}
