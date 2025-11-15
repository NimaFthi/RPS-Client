using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ServerServices
{
    public class WebSocketHandler
    {
        private static WebSocketHandler _instance;

        public static WebSocketHandler Instance
        {
            get => _instance ??= new WebSocketHandler();
        }

        private const string BASE_URL = "http://localhost:5012/";
        private const string BASE_SOCKET_URL = "ws://localhost:5012/";

        private ClientWebSocket _webSocket;
        private string _token;

        private CancellationTokenSource _cts = new();
        
        private const int PingIntervalSeconds = 10;
        private const int PongTimeoutSeconds = 10;
        
        private TaskCompletionSource<bool> _pongTcs;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<WebSocketMessage> OnSignalReceived;
        
        private WebSocketRequestManager _webSocketRequestManager;

        private WebSocketHandler()
        {
        }

        public async Task LoginAsync()
        {
            _cts = new();
            _token = string.Empty;

            while (string.IsNullOrEmpty(_token) && !_cts.IsCancellationRequested)
            {
                _token = await RequestLoginTokenAsync();
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            await ConnectWebSocketAsync();
        }

        private async Task<String> RequestLoginTokenAsync()
        {
            if (_cts.IsCancellationRequested) return "";
            
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
            if(_cts.IsCancellationRequested) return;
            _webSocket = new ClientWebSocket();

            try
            {
                Uri uri = new Uri($"{BASE_SOCKET_URL}ws?token={_token}");
                await _webSocket.ConnectAsync(uri, _cts.Token);
                Debug.Log("Connected to server.");
                
                _webSocketRequestManager = new WebSocketRequestManager(_webSocket);
                OnConnected?.Invoke();

                _ = Task.Run(PingLoopAsync, _cts.Token);
                _ = Task.Run(ListenLoopAsync, _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"WS connect failed: {ex.Message}");
                await ReconnectAsync();
            }
        }

        private async Task PingLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested &&
                   _webSocket.State == WebSocketState.Open)
            {
                _pongTcs = new TaskCompletionSource<bool>();
                await SendMessageAsync(new WebSocketMessage { Type = WebSocketMessageType.Ping });
                Debug.Log("Ping");

                var pongReceived = await Task.WhenAny(_pongTcs.Task
                    ,Task.Delay(TimeSpan.FromSeconds(PongTimeoutSeconds))) == _pongTcs.Task;

                if (!pongReceived)
                {
                    Debug.LogError("Heartbeat timeout. Reconnecting...");
                    await ReconnectAsync();
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(PingIntervalSeconds));
            }
        }
        
        private void OnPongReceived()
        {
            _pongTcs?.TrySetResult(true);
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
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        await ReconnectAsync();
                        return;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var msg = JsonConvert.DeserializeObject<WebSocketMessage>(json);

                    if (msg.Type == WebSocketMessageType.Pong)
                    {
                        OnPongReceived();
                        Debug.Log("Pong");
                        continue;
                    }

                    Debug.Log($"Message received => ID => {msg.RequestID} : Type => {msg.Type} : Data => {msg.Data}");

                    if (!string.IsNullOrEmpty(msg.RequestID))
                    {
                        _webSocketRequestManager.ProcessMessageForRespond(msg);
                        continue;
                    }
                    
                    OnSignalReceived?.Invoke(msg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Listen error: {ex.Message}");
                    await ReconnectAsync();
                    return;
                }
            }
        }

        private async Task ReconnectAsync()
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

            _cts = new();
            Debug.Log("Reconnecting...");
            await ConnectWebSocketAsync();
        }

        public async Task SendMessageAsync(WebSocketMessage msg)
        {
            var json = JsonConvert.SerializeObject(msg);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes),
                System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task<ServerResponse> SendMessageWithResponseAsync(WebSocketMessage msg, bool hasTimeOut = true ,float timeoutInSeconds = 10f)
        {
            var response =  await _webSocketRequestManager.RequestAsync(msg, hasTimeOut ,timeoutInSeconds);

            if (!response.Success)
            {
                Debug.LogError($"Error in request with type => {msg.Type} : Error => {response.Error}");
            }
            
            return response;
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

        public void Cancel()
        {
            _cts.Cancel();
        }
    }


    [Serializable]
    public class AuthRequestDto
    {
        public Guid DeviceGUID { get; set; }
    }

    public class WebSocketMessage
    {
        public string RequestID { get; set; }
        public WebSocketMessageType Type  { get; set; }
        public string Data  { get; set; }
        
        public string Error  { get; set; }

        public void SetRequestId()
        {
            RequestID = Guid.NewGuid().ToString();
        }
    }

    public enum WebSocketMessageType
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