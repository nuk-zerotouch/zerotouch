using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTouch.UI.Services
{
    public class AppWebSocketClient
    {
        private ClientWebSocket? _client;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;

        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnConnectionStatusChanged;

        public async Task ConnectAsync(string uri)
        {
            if (_client is { State: WebSocketState.Open or WebSocketState.Connecting })
                return;

            Cleanup();
            
            // Create new instance for each connection attempt
            _client = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            
            try
            {
                await _client.ConnectAsync(new Uri(uri), _cts.Token);
                OnConnectionStatusChanged?.Invoke("Connected");
                Console.WriteLine($"Connected to {uri}");
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_client, _cts));
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Error: {ex.Message}");
                Cleanup();
            }
        }
        
        private async Task ReceiveLoopAsync(ClientWebSocket client, CancellationTokenSource cts)
        {
            var buffer = new byte[2048];
            var sb = new StringBuilder();

            try
            {
                // start receiving messages
                while (client.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    var result = await client.ReceiveAsync(buffer, cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var message = sb.ToString();
                        sb.Clear();

                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal on disconnect
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Receive error: {ex.Message}");
            }
            finally
            {
                OnConnectionStatusChanged?.Invoke("Disconnected");
                Cleanup();
            }
        }

        public async Task DisconnectAsync()
        {
            var client = _client;
            var cts = _cts;

            if (client == null || cts == null)
                return;

            try
            {
                cts.Cancel();

                if (client.State == WebSocketState.Open)
                {
                    await client.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed by user",
                        CancellationToken.None
                    );
                }
            }
            catch
            {
                // swallow
            }
            finally
            {
                Cleanup();
            }
        }
        
        private void Cleanup()
        {
            try { _cts?.Cancel(); } catch { }
            try { _client?.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }

            _client = null;
            _cts = null;
            _receiveTask = null;
        }
        
        public async Task SendAsync(string json)
        {
            var client = _client;
            if (client == null || client.State != WebSocketState.Open)
                return;

            var buffer = Encoding.UTF8.GetBytes(json);

            await client.SendAsync(
                buffer,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
