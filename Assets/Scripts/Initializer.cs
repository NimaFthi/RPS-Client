using System.Threading.Tasks;
using Data;
using ServerServices;
using TMPro;
using UI;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    public async void Start()
    {
        MainMenuManager.Instance.SetStatusText("Logging in");
        await WebSocketHandler.Instance.LoginAsync();
        MainMenuManager.Instance.SetStatusText("Successfully logged in");
        GameData.UserProfile = await ServerServices.ServerServices.Instance.GetUserProfile();
        MainMenuManager.Instance.SetStatusText("Received UserProfile");
        GameData.InitData = await ServerServices.ServerServices.Instance.GetInitData();
        MainMenuManager.Instance.SetStatusText("Received InitData");
        MainMenuManager.Instance.SetupMatchUI(GameData.InitData.AvailableMatchData);
        MainMenuManager.Instance.SetStatusText($"Welcome {GameData.UserProfile.Username}");
    }

    public void OnApplicationQuit()
    {
        WebSocketHandler.Instance.Cancel();
    }
}
