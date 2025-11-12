using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DTOs;
using Newtonsoft.Json;
using Unity.VisualScripting;
using UnityEngine;

namespace ServerServices
{
    public class ServerServices
    {
        private static ServerServices _instance;

        public static ServerServices Instance
        {
            get => _instance ??= new ServerServices();
        }
        
        private readonly WebSocketHandler _webSocketHandler;
        private Dictionary <WebSocketMessageType, Dictionary<Type,Action<ServerSignal>>> _signalHandlers = new();
        
        private ServerServices()
        {
            _webSocketHandler = WebSocketHandler.Instance;
            _webSocketHandler.OnSignalReceived += HandleSignal;
        }
        
        private T ExtractDataFromResponse<T>(ServerResponse response) where T : BaseDto
        {
            if (response == null || !response.Success || response.Data == null)
            {
                return null;
            }
            
            return JsonConvert.DeserializeObject<T>(response.Data);
        }

        public void SubscribeToSignal(WebSocketMessageType messageType, Type listenerType ,Action<ServerSignal> handler)
        {
            _signalHandlers[messageType][listenerType] = handler;
        }

        public void UnsubscribeToSignal(WebSocketMessageType messageType, Type listenerType)
        {
            if (!_signalHandlers.TryGetValue(messageType, out var listeners))
            {
                Debug.LogWarning($"No listener was found for message type: {messageType}");
                return;
            }

            listeners.Remove(listenerType);
        }

        private void HandleSignal(WebSocketMessage signal)
        {
            if(!_signalHandlers.TryGetValue(signal.Type, out var listeners)) return;

            foreach (var pair in listeners)
            {
                pair.Value?.Invoke(new ServerSignal
                {
                    Data = signal.Data
                });
            }
        }

        #region Services

        public async Task<UserProfile> GetUserProfile()
        {
            var response = await _webSocketHandler.SendMessageWithResponseAsync(new WebSocketMessage
            {
                Type = WebSocketMessageType.GetUserProfile,
                Data = null,
            });

            return ExtractDataFromResponse<UserProfile>(response);
        }

        #endregion
    }

    public class ServerSignal
    {
        public string Data {get; set;}
    }
    
    public class ServerResponse
    {
        public string Data {get; set;}
        public string Error {get; set;}
        public bool Success => string.IsNullOrEmpty(Error);
    }
}