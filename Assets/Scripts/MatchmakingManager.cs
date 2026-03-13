using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using TMPro;

public class MatchmakingManager : MonoBehaviour
{
    public static MatchmakingManager Instance { get; private set; }

    /// <summary>Set by MainMenuManager so we can update status text.</summary>
    public static TMP_Text MatchmakingStatusText { get; set; }

    /// <summary>Invoked when matchmaking ends (disconnect, cancel, or error). MainMenuManager resets UI.</summary>
    public static Action OnMatchmakingEnded;

    private const string RelayJoinCodeKey = "RelayJoinCode";
    private static bool s_DisconnectListenersRegistered;
    private static bool s_CleaningUp;
    private Coroutine _timeoutCoroutine;
    private const float MatchmakingTimeoutSeconds = 15f;
    private static bool s_ConnectListenersRegistered;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public async void FindMatch()
    {
        if (NetworkManager.Singleton == null)
        {
            SetStatus("NetworkManager not found.");
            OnMatchmakingEnded?.Invoke();
            return;
        }
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("NetworkManager is already running. Aborting duplicate connection attempt.");
            OnMatchmakingEnded?.Invoke();
            return;
        }

        RegisterDisconnectListenersIfNeeded();
        RegisterConnectListenersIfNeeded();

        // Start a timeout so the Online button resets if nothing happens.
        if (_timeoutCoroutine != null)
            StopCoroutine(_timeoutCoroutine);
        _timeoutCoroutine = StartCoroutine(MatchmakingTimeoutRoutine());

        try
        {
            var lobby = await LobbyService.Instance.QuickJoinLobbyAsync();

            // Client path: joined a lobby
            if (lobby?.Data != null && lobby.Data.TryGetValue(RelayJoinCodeKey, out var dataObj) && !string.IsNullOrEmpty(dataObj?.Value))
            {
                SetStatus("Match Found! Connecting...");
                string joinCode = dataObj.Value;
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                // Always use WebSockets ("wss") so Relay configuration matches the UnityTransport Protocol.
                var relayServerData = joinAllocation.ToRelayServerData("wss");
                if (NetworkManager.Singleton == null) { SetStatus("NetworkManager not found."); OnMatchmakingEnded?.Invoke(); return; }
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                    transport.SetRelayServerData(relayServerData);
                NetworkManager.Singleton.StartClient();
                return;
            }

            SetStatus("Invalid lobby data.");
            OnMatchmakingEnded?.Invoke();
        }
        catch (LobbyServiceException)
        {
            // Host path: no lobby found, create one
            await StartAsHost();
        }
        catch (Exception ex)
        {
            SetStatus("Error: " + ex.Message);
            Debug.LogException(ex);
            OnMatchmakingEnded?.Invoke();
        }
    }

    private async System.Threading.Tasks.Task StartAsHost()
    {
        if (NetworkManager.Singleton == null) { SetStatus("NetworkManager not found."); OnMatchmakingEnded?.Invoke(); return; }
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("NetworkManager is already running. Aborting duplicate connection attempt.");
            OnMatchmakingEnded?.Invoke();
            return;
        }

        SetStatus("Hosting Lobby. Waiting for victim...");

        RegisterDisconnectListenersIfNeeded();
        RegisterConnectListenersIfNeeded();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var createOptions = new CreateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
            }
        };
        await LobbyService.Instance.CreateLobbyAsync("1v1 Lobby", 2, createOptions);

        // Always use WebSockets ("wss") so Relay configuration matches the UnityTransport Protocol.
        var relayServerData = allocation.ToRelayServerData("wss");
        if (NetworkManager.Singleton == null) { SetStatus("NetworkManager not found."); OnMatchmakingEnded?.Invoke(); return; }
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
            transport.SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartHost();
    }

    private static void RegisterDisconnectListenersIfNeeded()
    {
        if (s_DisconnectListenersRegistered || NetworkManager.Singleton == null) return;
        s_DisconnectListenersRegistered = true;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectOrStop;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
    }

    private static void RegisterConnectListenersIfNeeded()
    {
        if (s_ConnectListenersRegistered || NetworkManager.Singleton == null) return;
        s_ConnectListenersRegistered = true;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    private static void HandleClientConnected(ulong _)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsIds.Count == 2)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                "OnlineGameScene",
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }
    }

    private static void OnDisconnectOrStop(ulong _)
    {
        CleanupMatchmaking();
    }

    private static void OnServerStopped(bool _)
    {
        CleanupMatchmaking();
    }

    /// <summary>Shuts down the network session and notifies UI to reset. Call on disconnect or cancel.</summary>
    public static void CleanupMatchmaking(string finalStatus = "")
    {
        if (s_CleaningUp) return;
        s_CleaningUp = true;
        try
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                NetworkManager.Singleton.Shutdown();
            if (!string.IsNullOrEmpty(finalStatus))
                SetStatus(finalStatus);
            else
                SetStatus("");
            OnMatchmakingEnded?.Invoke();
        }
        finally
        {
            s_CleaningUp = false;
        }
    }

    /// <summary>Call to cancel matchmaking or allow the player to try again after disconnecting.</summary>
    public static void CancelMatchmaking()
    {
        CleanupMatchmaking();
    }

    private static void SetStatus(string text)
    {
        if (MatchmakingStatusText != null)
            MatchmakingStatusText.text = text;
    }

    private IEnumerator MatchmakingTimeoutRoutine()
    {
        yield return new WaitForSeconds(MatchmakingTimeoutSeconds);

        // If after 15 seconds we still don't have two connected clients, show a failure message briefly,
        // then reset the UI and shutdown networking so the player can try again.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count < 2)
        {
            SetStatus("Connection failed. Try again.");
            yield return new WaitForSeconds(2f);
            CleanupMatchmaking();
        }
    }
}
