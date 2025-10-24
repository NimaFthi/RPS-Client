using UnityEngine;

public class Initializer : MonoBehaviour
{
    public async void Start()
    {
        await RPSClient.RPSClient.Instance.LoginAsync();
    }

    public void OnApplicationQuit()
    {
        RPSClient.RPSClient.Instance.Cancel();
    }
}
