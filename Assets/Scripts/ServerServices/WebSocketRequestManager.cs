using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ServerServices
{
    public class WebSocketRequestManager
    {
        private ClientWebSocket _socket;
        private Dictionary<string, TaskCompletionSource<ServerResponse>> _waitingForResponseRequests = new();

        public WebSocketRequestManager(ClientWebSocket socket)
        {
            _socket = socket;
        }

        public async Task<ServerResponse> RequestAsync(WebSocketMessage webSocketMessage, float timeout)
        {
            webSocketMessage.SetRequestId();
            TaskCompletionSource<ServerResponse> tcs = new();
            _waitingForResponseRequests.Add(webSocketMessage.RequestID, tcs);
            
            var json = JsonConvert.SerializeObject(webSocketMessage);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(new ArraySegment<byte>(bytes),
                System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            
            return await tcs.Task;
        } 
        
        public void ProcessMessageForRespond(WebSocketMessage webSocketMessage)
        {
            if (_waitingForResponseRequests.TryGetValue(webSocketMessage.RequestID, out var tcs))
            {
                tcs.TrySetResult(new ServerResponse()
                {
                    Data = webSocketMessage.Data,
                    Error = webSocketMessage.Error
                });
            }
            else
            {
                Debug.LogWarning($"Received WebSocketMessage with invalid request ID : ID => {webSocketMessage.RequestID} ,Type => {webSocketMessage.Type} , Data => {webSocketMessage.Data}");
            }
        }
    }
}