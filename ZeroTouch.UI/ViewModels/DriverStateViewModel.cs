using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;
using Avalonia.Threading;
using ZeroTouch.UI.Services;

namespace ZeroTouch.UI.ViewModels
{
    public partial class DriverStateViewModel : ViewModelBase
    {
        [ObservableProperty]
        private bool _isFatigueActive = false;

        [ObservableProperty]
        private string _warningText = "⚠️ 偵測到駕駛疲勞\n請立即注意路況";

        // slider (0.0 ~ 1.0)
        [ObservableProperty]
        private double _slideProgress = 0.0;

        public DriverStateViewModel(AppWebSocketClient ws)
        {
            ws.OnMessageReceived += OnWsMessage;
        }

        private void OnWsMessage(string message)
        {
            try
            {
                var json = JsonDocument.Parse(message).RootElement;

                if (json.GetProperty("type").GetString() != "driver_state")
                    return;

                if (json.GetProperty("fatigue").GetBoolean())
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        IsFatigueActive = true;
                        SlideProgress = 0.0;
                    });
                }
            }
            catch
            {
                // ignore invalid message
            }
        }
        
        public void AcknowledgeFatigue()
        {
            IsFatigueActive = false;
            SlideProgress = 0.0;

            // TODO
            // hook
            // Please stop the music...
            // ack backend
        }
    }
}
