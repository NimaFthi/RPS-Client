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
        
        private ClientWebSocket _webSocket;
        
        private const string BASE_URL = "http://localhost:5012/";
        private const string BASE_SOCKET_URL = "ws://localhost:5012/";
        
        private bool _pongReceived = false;
        private DateTime _lastPingTime = DateTime.MinValue;
        private float _pingIntervalInSeconds = 30f;
        private float _pongTimeoutInSeconds = 10f; 

        private RPSClient()
        {
            
        }
        public async Task Login()
        {
            using (var request = new UnityWebRequest(BASE_URL + "api/auth/login", "POST"))
            {
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
                    return;
                }
                
                var token = request.downloadHandler.text;
                Debug.Log($"Token => {token}");

                await ConnectWebsocket(token);
            }
        }
        
        private async Task ConnectWebsocket(string token)
        {
            _webSocket = new ClientWebSocket();
            
            try
            {
                await _webSocket.ConnectAsync(new Uri(BASE_SOCKET_URL + $"ws?token={token}"), CancellationToken.None);
                Debug.Log("Websocket is Connected");
                _ = StartPingLoop();
                await ListenLoop();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in connecting to socket {e.Message}");
            }
        }

        private async Task StartPingLoop()
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                if (_lastPingTime + TimeSpan.FromSeconds(_pingIntervalInSeconds) >= DateTime.UtcNow) continue;
                
                await SendMessage(new WebSocketMessage()
                {
                    Type = WebSocketMessageTypes.Ping
                });
                
                Debug.Log("Ping");
                
                _lastPingTime = DateTime.UtcNow;
                _pongReceived = false;

                await Task.Delay(TimeSpan.FromSeconds(_pongTimeoutInSeconds));

                if (!_pongReceived)
                {
                    Debug.Log("Websocket disconnected");
                }
            }
        }

        private async Task ListenLoop()
        {
            var buffer = new byte[4096];
            
            while (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonConvert.DeserializeObject<WebSocketMessage>(json);
                    Debug.Log($"Received: {json}");

                    if(message.Type == WebSocketMessageTypes.Pong)
                    {
                        Debug.Log("Pong");
                        _pongReceived = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in receiving data in websocket => {e.Message}");
                }
            }
        }

        public async Task SendMessage(WebSocketMessage message)
        {
            var json= JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            Debug.Log($"Sending web socket message => {json}");
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"Sent web socket message {json}");
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
}
