using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class OnlineGameManager : NetworkBehaviour
{
    public static OnlineGameManager Instance { get; private set; }

    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(90f);
    public NetworkVariable<int> currentRound = new NetworkVariable<int>(1);

    public bool IsPlaying { get; private set; } = false;

    private Canvas _canvas;
    private TMP_Text _timerText;
    private TMP_Text _runnerLiveScoreText;
    private TMP_Text _chaserLiveScoreText;

    private GameObject _statusPanel;
    private TMP_Text _statusText;
    private Button _endButton;

    private GameObject _scoreboardPanel;
    private TMP_Text _scoreboardText;

    private ulong _chaserClientId;
    private ulong _runnerClientId;

    private Coroutine _serverGameFlow;

    [Header("Spawn Points (Online)")]
    [SerializeField] private Transform runnerSpawnPoint;
    [SerializeField] private Transform chaserSpawnPoint;
    [SerializeField] private float spawnYOffset = 1.2f;

    /// <summary>
    /// Creates a simple colored cylinder marker that acts as a visual spawn point.
    /// Collider is removed so it does not affect gameplay.
    /// </summary>
    private static Transform CreateSpawnMarker(string name, Color color, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(2f, 0.1f, 2f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        return go.transform;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure we have visible spawn markers if they were not wired in the scene.
        if (runnerSpawnPoint == null)
        {
            var pos = new Vector3(-10f, spawnYOffset, 0f);
            runnerSpawnPoint = CreateSpawnMarker("RunnerSpawnPoint", Color.blue, pos);
        }

        if (chaserSpawnPoint == null)
        {
            var pos = new Vector3(10f, spawnYOffset, 0f);
            chaserSpawnPoint = CreateSpawnMarker("ChaserSpawnPoint", Color.red, pos);
        }

        BuildUI();

        // Enable mobile controls UI in this scene based on saved setting (default OFF).
        bool useMobile = PlayerPrefs.GetInt("MobileControls", 0) == 1;
        var mobileUi = GameObject.Find("MobileControlsUI") ?? GameObject.Find("MobileControlsCanvas");
        if (mobileUi != null)
            mobileUi.SetActive(useMobile);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // Clean up the dynamically created canvas when leaving OnlineGameScene.
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = 90f;
            currentRound.Value = 1;
            if (_serverGameFlow != null)
                StopCoroutine(_serverGameFlow);
            _serverGameFlow = StartCoroutine(ServerGameFlow());
        }
    }

    private void Update()
    {
        UpdateUI();

        if (!IsServer)
            return;

        if (!IsPlaying)
            return;

        timeRemaining.Value = Mathf.Max(0f, timeRemaining.Value - Time.deltaTime);
        if (timeRemaining.Value <= 0f)
        {
            ServerEndRound(timeout: true);
        }
    }

    private void BuildUI()
    {
        // Ensure there is an EventSystem using the new Input System so buttons are clickable.
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        _canvas = new GameObject("OnlineGameUI").AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;
        DontDestroyOnLoad(_canvas.gameObject);

        var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _canvas.gameObject.AddComponent<GraphicRaycaster>();

        _timerText = CreateText(
            name: "TimerText",
            parent: _canvas.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -20f),
            fontSize: 44,
            color: Color.white,
            alignment: TextAlignmentOptions.Center
        );

        _runnerLiveScoreText = CreateText(
            name: "RunnerLiveScoreText",
            parent: _canvas.transform,
            anchorMin: new Vector2(0f, 1f),
            anchorMax: new Vector2(0f, 1f),
            pivot: new Vector2(0f, 1f),
            anchoredPos: new Vector2(20f, -20f),
            fontSize: 36,
            color: new Color(0.2f, 0.6f, 1f, 1f),
            alignment: TextAlignmentOptions.Left
        );

        _chaserLiveScoreText = CreateText(
            name: "ChaserLiveScoreText",
            parent: _canvas.transform,
            anchorMin: new Vector2(1f, 1f),
            anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(1f, 1f),
            anchoredPos: new Vector2(-20f, -20f),
            fontSize: 36,
            color: new Color(1f, 0.2f, 0.2f, 1f),
            alignment: TextAlignmentOptions.Right
        );

        _statusPanel = new GameObject("StatusPanel");
        _statusPanel.transform.SetParent(_canvas.transform, false);
        var rt = _statusPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = _statusPanel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);
        // Status overlay should not block clicks on the Exit button.
        img.raycastTarget = false;

        _statusText = CreateText(
            name: "StatusText",
            parent: _statusPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: Vector2.zero,
            fontSize: 72,
            color: Color.white,
            alignment: TextAlignmentOptions.Center
        );
        _statusText.enableWordWrapping = true;
        _statusText.text = "WAITING FOR PLAYERS...";

        _scoreboardPanel = new GameObject("ScoreboardPanel");
        _scoreboardPanel.transform.SetParent(_canvas.transform, false);
        var srt = _scoreboardPanel.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = Vector2.zero;
        var sbg = _scoreboardPanel.AddComponent<Image>();
        sbg.color = new Color(0f, 0f, 0f, 0.75f);
        // Let clicks pass through the scoreboard background so the bottom Exit button is clickable.
        sbg.raycastTarget = false;
        _scoreboardText = CreateText(
            name: "ScoreboardText",
            parent: _scoreboardPanel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: Vector2.zero,
            fontSize: 52,
            color: Color.white,
            alignment: TextAlignmentOptions.Center
        );
        _scoreboardText.enableWordWrapping = true;
        _scoreboardPanel.SetActive(false);

        _endButton = CreateButton(
            name: "EndButton",
            parent: _canvas.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            pivot: new Vector2(0.5f, 0f),
            anchoredPos: new Vector2(0f, 30f),
            size: new Vector2(420f, 90f),
            label: "Exit to Main Menu"
        );
        _endButton.gameObject.SetActive(false);
        _endButton.onClick.RemoveAllListeners();
        _endButton.onClick.AddListener(DisconnectAndReturnToMainMenu);
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(1200f, 140f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.text = "";
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        string label
    )
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        var button = go.AddComponent<Button>();

        var labelText = CreateText(
            name: "Label",
            parent: go.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: Vector2.zero,
            fontSize: 32,
            color: Color.white,
            alignment: TextAlignmentOptions.Center
        );
        labelText.rectTransform.sizeDelta = new Vector2(size.x, size.y);
        labelText.text = label;

        return button;
    }

    private void UpdateUI()
    {
        if (_timerText != null)
            _timerText.text = $"Time: {Mathf.CeilToInt(timeRemaining.Value)}";

        float runnerLive = 90f - timeRemaining.Value;
        float chaserLive = timeRemaining.Value;

        if (_runnerLiveScoreText != null)
            _runnerLiveScoreText.text = $"Runner: {Mathf.FloorToInt(runnerLive)}";
        if (_chaserLiveScoreText != null)
            _chaserLiveScoreText.text = $"Chaser: {Mathf.CeilToInt(chaserLive)}";
    }

    private IEnumerator ServerGameFlow()
    {
        while (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClientsIds.Count < 2)
            {
                IsPlaying = false;
                SetStatusClientRpc("WAITING FOR PLAYERS...", showPanel: true, showEndButton: false, isPlaying: false);
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Round 1
            currentRound.Value = 1;
            yield return ServerStartRound(roleRevealSeconds: 5f);
            while (IsPlaying) yield return null;

            // Round 1 scoreboard (5 seconds)
            yield return ServerShowScoreboard("Round 1 Over!", 5f);

            // Round 2 (swap roles)
            currentRound.Value = 2;
            yield return ServerStartRound(roleRevealSeconds: 5f);
            while (IsPlaying) yield return null;

            // Final scoreboard + exit
            yield return ServerShowFinalScoreboard();

            yield break;
        }
    }

    private IEnumerator ServerStartRound(float roleRevealSeconds)
    {
        IsPlaying = false;
        timeRemaining.Value = 90f;

        AssignRolesForRound();
        ServerPlacePlayersAtSpawns();

        // Role reveal: send specific instructions to each client.
        SendRoleInstructionToClient(_chaserClientId, $"You are the CHASER (Red) - Catch the Runner!\n\n<size=48>Round {currentRound.Value}</size>");
        SendRoleInstructionToClient(_runnerClientId, $"You are the RUNNER (Blue) - Survive!\n\n<size=48>Round {currentRound.Value}</size>");
        yield return new WaitForSeconds(roleRevealSeconds);

        // Unfreeze & hide panel
        SetStatusClientRpc("", showPanel: false, showEndButton: false, isPlaying: true);
        IsPlaying = true;
    }

    private void AssignRolesForRound()
    {
        var ids = NetworkManager.Singleton.ConnectedClientsIds;
        if (ids.Count < 2) return;

        ulong a = ids[0];
        ulong b = ids[1];

        // Round 1: random chaser. Round 2: swap roles.
        if (currentRound.Value == 1)
        {
            bool aIsChaser = Random.value > 0.5f;
            _chaserClientId = aIsChaser ? a : b;
            _runnerClientId = aIsChaser ? b : a;
        }
        else
        {
            // swap from previous
            var prevChaser = _chaserClientId;
            _chaserClientId = _runnerClientId;
            _runnerClientId = prevChaser;
        }

        SetClientRole(_chaserClientId, true);
        SetClientRole(_runnerClientId, false);
    }

    private void ServerPlacePlayersAtSpawns()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        Vector3 runnerPos = runnerSpawnPoint != null
            ? runnerSpawnPoint.position
            : new Vector3(-20f, spawnYOffset, 0f);
        Vector3 chaserPos = chaserSpawnPoint != null
            ? chaserSpawnPoint.position
            : new Vector3(20f, spawnYOffset, 0f);

        PlaceClientPlayer(_runnerClientId, runnerPos);
        PlaceClientPlayer(_chaserClientId, chaserPos);
    }

    private static void PlaceClientPlayer(ulong clientId, Vector3 position)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject == null) return;

        var t = client.PlayerObject.transform;
        t.position = position;

        var rb = client.PlayerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private static void SetClientRole(ulong clientId, bool chaser)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject == null) return;

        var np = client.PlayerObject.GetComponent<NetworkPlayer>();
        if (np == null) return;

        np.isChaser.Value = chaser;
    }

    private void SendRoleInstructionToClient(ulong clientId, string text)
    {
        var sendParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
        ShowStatusForLocalClientClientRpc(text, showPanel: true, showEndButton: false, isPlaying: false, sendParams);
    }

    [ClientRpc]
    private void ShowStatusForLocalClientClientRpc(string text, bool showPanel, bool showEndButton, bool isPlaying, ClientRpcParams clientRpcParams = default)
    {
        if (_statusText != null && !string.IsNullOrEmpty(text))
            _statusText.text = text;

        if (_statusPanel != null)
            _statusPanel.SetActive(showPanel);

        if (_endButton != null)
            _endButton.gameObject.SetActive(showEndButton);

        IsPlaying = isPlaying;
    }

    [ClientRpc]
    private void SetStatusClientRpc(string text, bool showPanel, bool showEndButton, bool isPlaying)
    {
        ShowStatusForLocalClientClientRpc(text, showPanel, showEndButton, isPlaying);
    }

    public void ChaserCaughtRunner()
    {
        if (!IsServer) return;
        ServerEndRound(timeout: false);
    }

    private void ServerEndRound(bool timeout)
    {
        if (!IsServer) return;
        if (!IsPlaying) return;

        IsPlaying = false;

        int runnerLive = Mathf.FloorToInt(90f - timeRemaining.Value);
        int chaserLive = Mathf.CeilToInt(timeRemaining.Value);

        AddLiveScoreToTotals(_runnerClientId, runnerLive);
        AddLiveScoreToTotals(_chaserClientId, chaserLive);

        // The server coroutine controls when the scoreboard / next round starts.
    }

    private static void AddLiveScoreToTotals(ulong clientId, int liveScore)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject == null) return;

        var np = client.PlayerObject.GetComponent<NetworkPlayer>();
        if (np == null) return;

        np.totalScore.Value += liveScore;
    }

    private static string GetPlayerLabel(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return $"Player {clientId}";
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return $"Player {clientId}";

        var np = client.PlayerObject.GetComponent<NetworkPlayer>();
        if (np == null) return $"Player {clientId}";

        var name = np.playerName.Value.ToString();
        return string.IsNullOrWhiteSpace(name) ? $"Player {clientId}" : name;
    }

    private string BuildScoreboardText(string title)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClientsIds.Count < 2)
            return title;

        var aId = NetworkManager.Singleton.ConnectedClientsIds[0];
        var bId = NetworkManager.Singleton.ConnectedClientsIds[1];

        int aScore = GetTotalScore(aId);
        int bScore = GetTotalScore(bId);

        return $"{title}\n\n{GetPlayerLabel(aId)}: {aScore}\n{GetPlayerLabel(bId)}: {bScore}";
    }

    private static int GetTotalScore(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return 0;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return 0;
        if (client.PlayerObject == null) return 0;

        var np = client.PlayerObject.GetComponent<NetworkPlayer>();
        return np != null ? np.totalScore.Value : 0;
    }

    private IEnumerator ServerShowScoreboard(string title, float seconds)
    {
        var text = BuildScoreboardText(title);
        ShowScoreboardClientRpc(text, seconds, showExit: false);
        yield return new WaitForSeconds(seconds);
        HideScoreboardClientRpc();
    }

    private IEnumerator ServerShowFinalScoreboard()
    {
        string text = BuildScoreboardText("Final Scoreboard");

        // Winner line + per-player online leaderboard submission.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
        {
            var aId = NetworkManager.Singleton.ConnectedClientsIds[0];
            var bId = NetworkManager.Singleton.ConnectedClientsIds[1];
            int aScore = GetTotalScore(aId);
            int bScore = GetTotalScore(bId);

            bool aWon = false;
            bool bWon = false;
            string winner;

            if (aScore == bScore)
            {
                winner = "TIE!";
                Debug.Log($"[OnlineGameManager] Final scoreboard tie. aId={aId}, bId={bId}, score={aScore}.");
            }
            else if (aScore > bScore)
            {
                aWon = true;
                winner = $"{GetPlayerLabel(aId)} WINS!";
                Debug.Log($"[OnlineGameManager] Final winner: {GetPlayerLabel(aId)} (client {aId}) with score {aScore} vs {bScore}.");
            }
            else
            {
                bWon = true;
                winner = $"{GetPlayerLabel(bId)} WINS!";
                Debug.Log($"[OnlineGameManager] Final winner: {GetPlayerLabel(bId)} (client {bId}) with score {bScore} vs {aScore}.");
            }

            // Ask each client to submit its own online result to UGS using its local player identity.
            SubmitOnlineResultForClient(aId, aWon);
            SubmitOnlineResultForClient(bId, bWon);

            text = $"{text}\n\n{winner}";
        }

        ShowScoreboardClientRpc(text, 0f, showExit: true);
        yield break;
    }

    [ClientRpc]
    private void ShowScoreboardClientRpc(string text, float _secondsUnused, bool showExit)
    {
        if (_scoreboardText != null)
            _scoreboardText.text = text;

        if (_scoreboardPanel != null)
            _scoreboardPanel.SetActive(true);

        if (_endButton != null)
            _endButton.gameObject.SetActive(showExit);

        // Freeze during scoreboard display
        IsPlaying = false;
    }

    [ClientRpc]
    private void HideScoreboardClientRpc()
    {
        if (_scoreboardPanel != null)
            _scoreboardPanel.SetActive(false);
    }

    private void SubmitOnlineResultForClient(ulong clientId, bool isWin)
    {
        var sendParams = new ClientRpcSendParams
        {
            TargetClientIds = new[] { clientId }
        };
        var clientParams = new ClientRpcParams { Send = sendParams };

        Debug.Log($"[OnlineGameManager] Instructing client {clientId} to submit online result. isWin={isWin}.");
        SubmitOnlineResultClientRpc(isWin, clientParams);
    }

    [ClientRpc]
    private void SubmitOnlineResultClientRpc(bool isWin, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[OnlineGameManager] SubmitOnlineResultClientRpc received on client. isWin={isWin}.");
        _ = UGSLeaderboardManager.SubmitOnlineResultAsync(isWin);
    }

    private void DisconnectAndReturnToMainMenu()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        // Scene name may be adjusted later; this matches the project’s menu scene naming.
        SceneManager.LoadScene("StartScene", LoadSceneMode.Single);
    }
}

