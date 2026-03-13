using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class CloudManager : MonoBehaviour
{
    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        string playerName = PlayerPrefs.GetString("PlayerName", "Unknown");
        playerName = playerName.Replace(" ", "");
        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        Debug.Log("Unity Gaming Services initialized and signed in anonymously.");
    }
}
