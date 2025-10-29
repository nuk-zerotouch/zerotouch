using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTouch.UI.Services
{
    public class GestureWebSocketClient
    {
        private ClientWebSocket? _client;
        private CancellationTokenSource? _cts;

        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnConnectionStatusChanged;

        public async Task ConnectAsync(string uri)
        {
            try
            {
                // Create new instance for each connection attempt
                _client = new ClientWebSocket();
                _cts = new CancellationTokenSource();

                await _client.ConnectAsync(new Uri(uri), _cts.Token);
                OnConnectionStatusChanged?.Invoke("Connected");
                Console.WriteLine($"Connected to {uri}");

                var buffer = new byte[2048];

                // start receiving messages
                while (_client.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var result = await _client.ReceiveAsync(buffer, _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
                        OnConnectionStatusChanged?.Invoke("Disconnected (by server)");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnMessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                // close connection manually
            }
            catch (Exception ex)
            {
                Console.WriteLine("WebSocket error: " + ex.Message);
                OnConnectionStatusChanged?.Invoke($"Error: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client == null)
                return;

            try
            {
                if (_client.State == WebSocketState.Open || _client.State == WebSocketState.CloseReceived)
                {
                    _cts?.Cancel();
                    await _client.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed by user",
                        CancellationToken.None
                    );
                    Console.WriteLine("WebSocket closed cleanly.");
                }
                else
                {
                    Console.WriteLine($"Skip closing — current state: {_client.State}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Disconnect error: {ex.Message}");
            }
            finally
            {
                _cts?.Dispose();
                _client?.Dispose();
                _cts = null;
                _client = null;
            }
        }
    }
}
