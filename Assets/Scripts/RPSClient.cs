using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace RPSClient
{
    public class RPSClient
    {
        private static RPSClient _instance;

        public static RPSClient Instance
        {
            get => _instance ??= new RPSClient();
        }

        private const string BASE_URL = "http://localhost:5012/";
        private const string BASE_SOCKET_URL = "ws://localhost:5012/";

        private ClientWebSocket _webSocket;
        private string _token;

        private CancellationTokenSource _cts = new();

        private bool _pongReceived = true;

        private const float PingIntervalSeconds = 10f;
        private const float PongTimeoutSeconds = 10f;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<WebSocketMessage> OnMessage;

        private RPSClient()
        {
        }

        public async Task LoginAsync()
        {
            _token = await RequestLoginTokenAsync();
            await ConnectWebSocketAsync();
        }

        private async Task<String> RequestLoginTokenAsync()
        {
            using (var request = new UnityWebRequest(BASE_URL + "api/auth/login", "POST"))
            {
                Debug.Log("Started logging in");
                request.SetRequestHeader("Content-Type", "application/json");

                var json = JsonConvert.SerializeObject(new AuthRequestDto
                {
                    DeviceGUID = GetDeviceGuid()
                });

                byte[] message = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(message);
                request.downloadHandler = new DownloadHandlerBuffer();

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to authenticate => {request.error}");
                    return string.Empty;
                }

                var token = request.downloadHandler.text;
                Debug.Log($"Received Token => {token}");
                return token;
            }
        }

        private async Task ConnectWebSocketAsync()
        {
            _cts = new();
            _webSocket = new ClientWebSocket();

            try
            {
                Uri uri = new Uri($"{BASE_SOCKET_URL}ws?token={_token}");
                await _webSocket.ConnectAsync(uri, _cts.Token);
                Debug.Log("Connected to server.");

                OnConnected?.Invoke();

                _ = Task.Run(PingLoopAsync, _cts.Token);
                _ = Task.Run(ListenLoopAsync, _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"WS connect failed: {ex.Message}");
                await RetryReconnectAsync();
            }
        }

        private async Task PingLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested &&
                   _webSocket.State == WebSocketState.Open)
            {
                _pongReceived = false;
                await SendMessage(new WebSocketMessage { Type = WebSocketMessageTypes.Ping });

                await Task.Delay(TimeSpan.FromSeconds(PongTimeoutSeconds));

                if (!_pongReceived)
                {
                    Debug.LogWarning("Heartbeat timeout. Reconnecting...");
                    await RetryReconnectAsync();
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(PingIntervalSeconds));
            }
        }

        private async Task ListenLoopAsync()
        {
            var buffer = new byte[4096];

            while (!_cts.Token.IsCancellationRequested &&
                   _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(buffer, _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await RetryReconnectAsync();
                        return;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var msg = JsonConvert.DeserializeObject<WebSocketMessage>(json);

                    if (msg.Type == WebSocketMessageTypes.Pong)
                    {
                        _pongReceived = true;
                        continue;
                    }

                    OnMessage?.Invoke(msg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Listen error: {ex.Message}");
                    await RetryReconnectAsync();
                    return;
                }
            }
        }

        private async Task RetryReconnectAsync()
        {
            if (_cts.IsCancellationRequested) return;
            _cts.Cancel();
            
            if (_webSocket is {State: WebSocketState.Open or  WebSocketState.CloseReceived})
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "Reconnecting",
                        CancellationToken.None);
                }
                catch { /* ignore */ }
            }

            _webSocket?.Dispose();
            
            OnDisconnected?.Invoke();

            await Task.Delay(3000);

            Debug.Log("Reconnecting...");
            await ConnectWebSocketAsync();
        }

        public async Task SendMessage(WebSocketMessage msg)
        {
            var json = JsonConvert.SerializeObject(msg);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static Guid GetDeviceGuid()
        {
            // Get Unity's built-in device identifier
            string deviceId = SystemInfo.deviceUniqueIdentifier;

            // Hash it for consistent, fixed-length output
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(deviceId);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Create a GUID from the 16-byte MD5 hash
                return new Guid(hashBytes);
            }
        }
    }


    [Serializable]
    public class AuthRequestDto
    {
        public Guid DeviceGUID { get; set; }
    }

    public class WebSocketMessage
    {
        public WebSocketMessageTypes Type;
        public string Data;
    }

    public enum WebSocketMessageTypes
    {
        Ping = 1,
        Pong = 2,
        GetInitData = 3,
        GetUserProfile = 4,
        RequestMatch = 100,
        CancelMatch = 101,
        MatchFound = 102,
        LeaveMatch = 103,
        StartMatch = 104,
        EndMatch = 105,
        StartRound = 106,
        RoundResult = 107,
        Move = 108
    }
}