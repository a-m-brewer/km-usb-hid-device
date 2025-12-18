using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteHIDController
{
    public class WebSocketHIDClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isConnected;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        public async Task ConnectAsync(string ipAddress)
        {
            _webSocket = new ClientWebSocket();
            var uri = new Uri($"ws://{ipAddress}/ws");
            
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            _isConnected = true;

            // Start receiving loop
            _ = Task.Run(ReceiveLoop);
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[1024];
            try
            {
                while (_webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure, 
                            string.Empty, 
                            CancellationToken.None);
                        _isConnected = false;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                _isConnected = false;
            }
        }

        public async Task SendMouseAsync(int x, int y, byte buttons, int wheel)
        {
            if (!IsConnected) return;

            // Clamp values to valid range
            x = Math.Clamp(x, -127, 127);
            y = Math.Clamp(y, -127, 127);
            wheel = Math.Clamp(wheel, -127, 127);

            var message = new
            {
                t = "m",
                x = x,
                y = y,
                b = buttons,
                w = wheel
            };

            await SendJsonAsync(message);
        }

        public async Task SendKeyboardAsync(byte modifiers, byte[] keycodes)
        {
            if (!IsConnected) return;

            // Ensure we always send an array, pad with zeros if needed
            var keyArray = new int[6];
            for (int i = 0; i < Math.Min(keycodes.Length, 6); i++)
            {
                keyArray[i] = keycodes[i];
            }

            var message = new
            {
                t = "k",
                m = (int)modifiers,
                k = keyArray
            };

            await SendJsonAsync(message);
        }

        private async Task SendJsonAsync(object message)
        {
            if (_webSocket?.State != WebSocketState.Open) return;

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnecting",
                    CancellationToken.None);
            }
            _isConnected = false;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _webSocket?.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}
