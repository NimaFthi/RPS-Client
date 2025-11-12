using System.Threading.Tasks;
using Data;
using ServerServices;
using TMPro;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _statusTMP;
    
    public async void Start()
    {
        _statusTMP.text = "Logging in";
        await WebSocketHandler.Instance.LoginAsync();
        _statusTMP.text = "Successfully logged in";
        GameData.UserProfile = await ServerServices.ServerServices.Instance.GetUserProfile();
        _statusTMP.text = "Received UserProfile";
        GameData.InitData = await ServerServices.ServerServices.Instance.GetInitData();
        _statusTMP.text = "Received InitData";
        await Task.Delay(2000);
        _statusTMP.text = $"Welcome {GameData.UserProfile.Username}";
    }

    public void OnApplicationQuit()
    {
        WebSocketHandler.Instance.Cancel();
    }
}
