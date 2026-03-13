using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public GameObject cloudLeaderboardPanel;
    public TMP_Text leaderboardScoresText;

    private GameObject backgroundDimmer;
    private GameObject mainMenuContainer;
    private GameObject modalsContainer;
    private GameObject matchmakingStatusContainer;

    private Button settingsButton;
    private GameObject settingsPanel;
    private Button closeSettingsButton;
    private Slider volumeSlider;
    private Button onlineButton;
    private TMP_Text matchmakingStatusText;
    private GameObject statusBackground;

    [Header("Mobile Controls")]
    [SerializeField] private Toggle mobileControlsToggle;
    [SerializeField] private GameObject mobileControlsCanvas;

    private static GameObject FindInActiveSceneIncludingInactive(string name)
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindInChildrenIncludingInactive(roots[i].transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindInChildrenIncludingInactive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindInChildrenIncludingInactive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private void Start()
    {
        mainMenuContainer = FindInActiveSceneIncludingInactive("MainMenuContainer");
        modalsContainer = FindInActiveSceneIncludingInactive("ModalsContainer");
        backgroundDimmer = FindInActiveSceneIncludingInactive("UI_BackgroundDimmer");
        matchmakingStatusContainer = FindInActiveSceneIncludingInactive("MatchmakingStatusContainer");

        if (nameInputField == null)
        {
            var t = FindInActiveSceneIncludingInactive("NameInputField");
            if (t != null) nameInputField = t.GetComponent<TMP_InputField>();
        }
        if (nameInputField != null && PlayerPrefs.HasKey("PlayerName"))
        {
            nameInputField.text = PlayerPrefs.GetString("PlayerName");
        }
        Debug.Log("Loaded Name: " + PlayerPrefs.GetString("PlayerName"));

        if (cloudLeaderboardPanel == null)
            cloudLeaderboardPanel = FindInActiveSceneIncludingInactive("CloudLeaderboardPanel");
        if (leaderboardScoresText == null && cloudLeaderboardPanel != null)
        {
            var scoresGo = cloudLeaderboardPanel.transform.Find("ScoresText");
            if (scoresGo != null) leaderboardScoresText = scoresGo.GetComponent<TMP_Text>();
        }

        var offlineBtn = FindInActiveSceneIncludingInactive("OfflineButton")?.GetComponent<Button>();
        if (offlineBtn != null) offlineBtn.onClick.AddListener(PlayOffline);
        onlineButton = FindInActiveSceneIncludingInactive("OnlineButton")?.GetComponent<Button>();
        if (onlineButton != null)
        {
            onlineButton.onClick.RemoveAllListeners();
            onlineButton.onClick.AddListener(StartMatchmaking);
        }

        var globalLeaderboardBtn = FindInActiveSceneIncludingInactive("GlobalLeaderboardButton")?.GetComponent<Button>();
        if (globalLeaderboardBtn != null) globalLeaderboardBtn.onClick.AddListener(OnGlobalLeaderboardClicked);

        if (cloudLeaderboardPanel != null)
        {
            var closeBtn = cloudLeaderboardPanel.transform.Find("CloseButton")?.GetComponent<Button>();
            if (closeBtn != null) closeBtn.onClick.AddListener(CloseLeaderboardPanel);
        }

        var settingsBtnGo = FindInActiveSceneIncludingInactive("SettingsButton");
        if (settingsBtnGo != null) settingsButton = settingsBtnGo.GetComponent<Button>();
        settingsPanel = FindInActiveSceneIncludingInactive("SettingsPanel");
        if (settingsPanel != null)
        {
            var closeBtnT = settingsPanel.transform.Find("CloseSettingsButton");
            if (closeBtnT != null) closeSettingsButton = closeBtnT.GetComponent<Button>();
            var contentT = settingsPanel.transform.Find("Content");
            if (contentT != null)
            {
                var sliderT = contentT.Find("VolumeSlider");
                if (sliderT != null) volumeSlider = sliderT.GetComponent<Slider>();
            }
        }

        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsPanel);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettingsPanel);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);

        statusBackground = FindInActiveSceneIncludingInactive("MatchmakingStatusBackground");
        if (statusBackground != null)
        {
            statusBackground.SetActive(false);
            var statusT = statusBackground.transform.Find("MatchmakingStatusText");
            if (statusT != null) matchmakingStatusText = statusT.GetComponent<TMP_Text>();
        }
        if (matchmakingStatusText == null)
        {
            var statusGo = FindInActiveSceneIncludingInactive("MatchmakingStatusText");
            if (statusGo != null) matchmakingStatusText = statusGo.GetComponent<TMP_Text>();
        }
        if (MatchmakingManager.Instance != null && matchmakingStatusText != null)
            MatchmakingManager.MatchmakingStatusText = matchmakingStatusText;

        if (volumeSlider != null)
            volumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (cloudLeaderboardPanel != null)
            cloudLeaderboardPanel.SetActive(false);
        if (modalsContainer != null)
            modalsContainer.SetActive(false);
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(false);
        if (matchmakingStatusContainer != null)
            matchmakingStatusContainer.SetActive(false);

        MatchmakingManager.OnMatchmakingEnded += ResetMatchmakingUI;

        // Wire up Mobile Controls toggle & canvas.
        if (settingsPanel != null && mobileControlsToggle == null)
        {
            var contentT = settingsPanel.transform.Find("Content");
            if (contentT != null)
            {
                var toggleT = contentT.Find("MobileControlsToggle");
                if (toggleT != null)
                    mobileControlsToggle = toggleT.GetComponent<Toggle>();
            }
        }

        if (mobileControlsCanvas == null)
        {
            // In the main menu scene we only care about the local mobile UI object.
            var found = FindInActiveSceneIncludingInactive("MobileControlsUI");
            if (found == null)
                found = FindInActiveSceneIncludingInactive("MobileControlsCanvas");
            mobileControlsCanvas = found;
        }

        if (mobileControlsToggle != null)
            mobileControlsToggle.onValueChanged.AddListener(OnMobileControlsToggleChanged);

        // Apply saved preference (default OFF).
        int mobilePref = PlayerPrefs.GetInt("MobileControls", 0);
        bool mobileOn = mobilePref == 1;
        if (mobileControlsToggle != null)
            mobileControlsToggle.isOn = mobileOn;
        ApplyMobileControls(mobileOn);

    }

    private void OnDestroy()
    {
        MatchmakingManager.OnMatchmakingEnded -= ResetMatchmakingUI;
    }

    private void ResetMatchmakingUI()
    {
        if (onlineButton != null)
            onlineButton.interactable = true;
        if (statusBackground != null)
            statusBackground.SetActive(false);
        if (matchmakingStatusContainer != null)
            matchmakingStatusContainer.SetActive(false);
    }

    private void CloseAllUI()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (cloudLeaderboardPanel != null)
            cloudLeaderboardPanel.SetActive(false);
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(false);
        if (modalsContainer != null)
            modalsContainer.SetActive(false);
        if (matchmakingStatusContainer != null)
            matchmakingStatusContainer.SetActive(false);
        if (statusBackground != null)
            statusBackground.SetActive(false);
        if (mainMenuContainer != null)
            mainMenuContainer.SetActive(true);
    }

    private void OpenSettingsPanel()
    {
        CloseAllUI();
        if (mainMenuContainer != null)
            mainMenuContainer.SetActive(false);
        if (modalsContainer != null)
            modalsContainer.SetActive(true);
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(true);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void CloseSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(false);
        if (modalsContainer != null)
            modalsContainer.SetActive(false);
        if (mainMenuContainer != null)
            mainMenuContainer.SetActive(true);
    }

    private void StartMatchmaking()
    {
        if (onlineButton != null)
            onlineButton.interactable = false;
        if (matchmakingStatusContainer != null)
            matchmakingStatusContainer.SetActive(true);
        if (statusBackground != null)
            statusBackground.SetActive(true);
        if (matchmakingStatusText != null)
            matchmakingStatusText.text = "Connecting to server...";
        if (MatchmakingManager.Instance != null)
            MatchmakingManager.Instance.FindMatch();
    }

    private void OnVolumeSliderChanged(float value)
    {
        if (AudioManager.instance != null)
            AudioManager.instance.SetVolume(value);
    }

    private void OnMobileControlsToggleChanged(bool isOn)
    {
        ApplyMobileControls(isOn);
        PlayerPrefs.SetInt("MobileControls", isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyMobileControls(bool isOn)
    {
        if (mobileControlsCanvas == null)
            return;

        mobileControlsCanvas.SetActive(isOn);
    }

    private void CloseLeaderboardPanel()
    {
        if (cloudLeaderboardPanel != null)
            cloudLeaderboardPanel.SetActive(false);
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(false);
        if (modalsContainer != null)
            modalsContainer.SetActive(false);
        if (mainMenuContainer != null)
            mainMenuContainer.SetActive(true);
    }

    public async void OnGlobalLeaderboardClicked()
    {
        CloseAllUI();
        if (mainMenuContainer != null)
            mainMenuContainer.SetActive(false);
        if (modalsContainer != null)
            modalsContainer.SetActive(true);
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(true);
        if (leaderboardScoresText != null)
            leaderboardScoresText.text = "Loading...";
        try
        {
            string result = await UGSLeaderboardManager.FetchTopScores();
            if (leaderboardScoresText != null)
                leaderboardScoresText.text = result;
        }
        catch (System.Exception e)
        {
            if (leaderboardScoresText != null)
                leaderboardScoresText.text = "Could not load leaderboard.\n" + e.Message;
        }
        if (cloudLeaderboardPanel != null)
            cloudLeaderboardPanel.SetActive(true);
    }

    public void PlayOffline()
    {
        if (nameInputField != null)
        {
            PlayerPrefs.SetString("PlayerName", nameInputField.text);
            PlayerPrefs.Save();
        }
        SceneManager.LoadScene(1);
    }

}
