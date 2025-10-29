using RPSClient;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    public async void Start()
    {
        await RPSClient.RPSClient.Instance.LoginAsync();
        await RPSClient.RPSClient.Instance.SendMessage(new WebSocketMessage
        {
            Type = WebSocketMessageTypes.GetUserProfile,
            Data = null
        });
    }

    public void OnApplicationQuit()
    {
        RPSClient.RPSClient.Instance.Cancel();
    }
}
