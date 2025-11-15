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

        public async Task<ServerResponse> RequestAsync(WebSocketMessage webSocketMessage, bool hasTimeOut ,float timeoutInSeconds)
        {
            webSocketMessage.SetRequestId();
            TaskCompletionSource<ServerResponse> tcs = new();
            _waitingForResponseRequests.Add(webSocketMessage.RequestID, tcs);
            
            var json = JsonConvert.SerializeObject(webSocketMessage);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(new ArraySegment<byte>(bytes),
                System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            
            Debug.Log($"Sending request to server => ID : {webSocketMessage.RequestID} || Type : {webSocketMessage.Type} || Data : {json}");

            if (!hasTimeOut)
            {
                return await tcs.Task;
            }
            
            var receivedRespondFromServer = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutInSeconds))) == tcs.Task;

            if (receivedRespondFromServer)
            {
                return tcs.Task.Result;
            }
            else
            {
                _waitingForResponseRequests.Remove(webSocketMessage.RequestID);
                return new ServerResponse
                {
                    Data = null,
                    Error = "[Request Time out]"
                };
            }
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