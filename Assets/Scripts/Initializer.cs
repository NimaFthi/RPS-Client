using System.Threading.Tasks;
using ServerServices;
using TMPro;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _statusTMP;
    
    public async void Start()
    {
        _statusTMP.text = "Logging in";
        await Task.Delay(2000);
        await WebSocketHandler.Instance.LoginAsync();
        _statusTMP.text = "Logged in";
        var userProfile = await ServerServices.ServerServices.Instance.GetUserProfile();
        _statusTMP.text = "Received UserProfile";
        await Task.Delay(2000);
        _statusTMP.text = $"Welcome {userProfile.Username}";
    }

    public void OnApplicationQuit()
    {
        WebSocketHandler.Instance.Cancel();
    }
}
