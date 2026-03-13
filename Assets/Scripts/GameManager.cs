using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField]
    private Text timerText;

    [SerializeField]
    private GameObject playAgainButton;

    [SerializeField]
    private Button pauseButton;

    [SerializeField]
    private GameObject pausePanel;

    [SerializeField]
    private Button gameOverMainMenuButton;

    [Header("Spawn Points (Offline)")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform enemySpawnPoint;
    [SerializeField] private float spawnYOffset = 1.2f;

    private float currentTime = 0f;
    private bool isGameOver;
    private bool isPaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;

        if (timerText == null)
        {
            timerText = FindFirstObjectByType<Text>();
        }

        if (playAgainButton != null)
        {
            playAgainButton.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (gameOverMainMenuButton != null && gameOverMainMenuButton.gameObject != null)
        {
            gameOverMainMenuButton.gameObject.SetActive(false);
        }

        // Ensure we have visible spawn markers if they were not wired in the scene.
        if (playerSpawnPoint == null)
        {
            var pos = new Vector3(-10f, spawnYOffset, 0f);
            playerSpawnPoint = CreateSpawnMarker("PlayerSpawnPoint", Color.blue, pos);
        }

        if (enemySpawnPoint == null)
        {
            var pos = new Vector3(10f, spawnYOffset, 0f);
            enemySpawnPoint = CreateSpawnMarker("EnemySpawnPoint", Color.red, pos);
        }
    }

    /// <summary>Creates a simple colored cylinder marker for a spawn point.</summary>
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
            Destroy(collider);

        return go.transform;
    }

    private void Start()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
        }
        else
        {
            var go = GameObject.Find("PauseButton");
            if (go != null && go.TryGetComponent<Button>(out var btn))
            {
                pauseButton = btn;
                pauseButton.onClick.AddListener(TogglePause);
            }
        }

        if (pausePanel != null)
        {
            var resume = pausePanel.transform.Find("ResumeButton")?.GetComponent<Button>();
            if (resume != null) resume.onClick.AddListener(TogglePause);
            var restart = pausePanel.transform.Find("RestartButton")?.GetComponent<Button>();
            if (restart != null) restart.onClick.AddListener(RestartGame);
            var mainMenu = pausePanel.transform.Find("MainMenuButton")?.GetComponent<Button>();
            if (mainMenu != null) mainMenu.onClick.AddListener(LoadMainMenu);
        }

        if (playAgainButton != null && playAgainButton.TryGetComponent<Button>(out var playAgainBtn))
        {
            playAgainBtn.onClick.AddListener(RestartGame);
        }

        if (gameOverMainMenuButton != null)
        {
            gameOverMainMenuButton.onClick.AddListener(LoadMainMenu);
        }
        else
        {
            var go = GameObject.Find("GameOverMainMenuButton");
            if (go != null && go.TryGetComponent<Button>(out var btn))
            {
                gameOverMainMenuButton = btn;
                gameOverMainMenuButton.onClick.AddListener(LoadMainMenu);
            }
        }

        PlacePlayerAndEnemyAtSpawns();

        // Enable mobile controls UI in this scene based on saved setting (default OFF).
        bool useMobile = PlayerPrefs.GetInt("MobileControls", 0) == 1;
        var mobileUi = GameObject.Find("MobileControlsUI") ?? GameObject.Find("MobileControlsCanvas");
        if (mobileUi != null)
            mobileUi.SetActive(useMobile);
    }

    private void PlacePlayerAndEnemyAtSpawns()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        GameObject enemyObj = GameObject.FindWithTag("Enemy");
        if (playerObj == null || enemyObj == null) return;

        Vector3 playerPos = playerSpawnPoint != null
            ? playerSpawnPoint.position
            : new Vector3(-20f, spawnYOffset, 0f);
        Vector3 enemyPos = enemySpawnPoint != null
            ? enemySpawnPoint.position
            : new Vector3(20f, spawnYOffset, 0f);

        playerObj.transform.position = playerPos;
        var rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = playerPos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var agent = enemyObj.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.Warp(enemyPos);
        else
            enemyObj.transform.position = enemyPos;
    }

    private void Update()
    {
        if (!isPaused && !isGameOver)
        {
            currentTime += Time.deltaTime;
            if (timerText != null)
            {
                timerText.text = $"Time: {currentTime:0.0}s";
            }
        }
    }

    public void HandlePlayerHit()
    {
        if (isGameOver)
            return;

        isGameOver = true;

        // Disable player movement
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        // Disable enemy pathfinding
        GameObject enemy = GameObject.FindWithTag("Enemy");
        if (enemy != null)
        {
            UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        if (timerText != null)
        {
            timerText.text = $"Game Over\nFinal Time: {currentTime:0.0}s";
        }

        if (playAgainButton != null)
        {
            playAgainButton.SetActive(true);
        }

        if (gameOverMainMenuButton != null && gameOverMainMenuButton.gameObject != null)
        {
            gameOverMainMenuButton.gameObject.SetActive(true);
        }

        _ = UGSLeaderboardManager.SubmitScore(currentTime);
    }

    public void TogglePause()
    {
        if (isGameOver) return;

        isPaused = !isPaused;
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null && player.TryGetComponent<PlayerController>(out var controller))
        {
            controller.enabled = !isPaused;
        }

        var enemy = GameObject.FindWithTag("Enemy");
        if (enemy != null && enemy.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
        {
            agent.enabled = !isPaused;
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
