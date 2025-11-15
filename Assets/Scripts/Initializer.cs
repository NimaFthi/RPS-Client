using System.Threading;
using System.Threading.Tasks;
using Data;
using ServerServices;
using UI;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    private CancellationTokenSource _getInitDataCts;
    
    public async void Start()
    {
        _getInitDataCts = new CancellationTokenSource();
        
        MainMenuManager.Instance.SetStatusText("Logging in");
        await WebSocketHandler.Instance.LoginAsync();
        MainMenuManager.Instance.SetStatusText("Successfully logged in");

        await GetInitData();
    }

    private async Task GetInitData()
    {
        if (_getInitDataCts.Token.IsCancellationRequested)
        {
            MainMenuManager.Instance.SetStatusText("");
            Debug.Log("GetInitData cancelled");
            return;
        }
        MainMenuManager.Instance.SetStatusText("Getting init data ...");
        
        var userProfile = await ServerServices.ServerServices.Instance.GetUserProfile();

        if (userProfile == null)
        {
            Debug.LogError("Failed to get user profile"); 
            MainMenuManager.Instance.SetStatusText("Failed to get user profile, trying again ...");
        
            await Task.Delay(5000);
            await GetInitData();
            return;
        }
        MainMenuManager.Instance.SetStatusText("Received UserProfile");
        GameData.UserProfile = userProfile;
        
        var initData = await ServerServices.ServerServices.Instance.GetInitData();
        if(initData == null)
        {
            Debug.LogError("Failed to get init data");
            MainMenuManager.Instance.SetStatusText("Failed to get init data, trying again ...");
            
            await Task.Delay(5000);
            await GetInitData();
            return;
        }
        
        MainMenuManager.Instance.SetStatusText("Received InitData");
        GameData.InitData = initData;
        
        MainMenuManager.Instance.SetupMatchUI(GameData.InitData.AvailableMatchData);
        MainMenuManager.Instance.SetStatusText($"Welcome {GameData.UserProfile.Username}");
    }

    public void OnApplicationQuit()
    {
        WebSocketHandler.Instance.Cancel();
        _getInitDataCts.Cancel();
    }
}
