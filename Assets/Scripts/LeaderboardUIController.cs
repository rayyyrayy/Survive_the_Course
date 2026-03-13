using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic leaderboard UI controller that supports both Offline (SinglePlayerTimes)
/// and Online (OnlineWins) leaderboards using layout groups and a dropdown toggle.
/// </summary>
public class LeaderboardUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Dropdown leaderboardDropdown;
    [SerializeField] private RectTransform rowsContainer;
    [SerializeField] private GameObject rowPrefab;

    [Header("Options")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private int maxEntries = 10;

    private enum ViewType
    {
        Offline,
        Online
    }

    private void Awake()
    {
        if (leaderboardDropdown != null)
        {
            leaderboardDropdown.onValueChanged.RemoveAllListeners();
            leaderboardDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }
    }

    private void Start()
    {
        SetupDropdownOptions();

        if (loadOnStart)
        {
            // Default to Offline (index 0)
            LoadOfflineAsync().Forget();
        }
    }

    private void SetupDropdownOptions()
    {
        if (leaderboardDropdown == null)
            return;

        leaderboardDropdown.ClearOptions();
        leaderboardDropdown.AddOptions(new List<string> { "Offline", "Online" });
        leaderboardDropdown.value = 0;
    }

    private void OnDropdownChanged(int index)
    {
        var view = (ViewType)index;
        switch (view)
        {
            case ViewType.Offline:
                LoadOfflineAsync().Forget();
                break;
            case ViewType.Online:
                LoadOnlineAsync().Forget();
                break;
        }
    }

    private async Task LoadOfflineAsync()
    {
        ClearRows();

        // First row is always the header row for this view.
        AddHeaderRow(ViewType.Offline);

        List<UGSLeaderboardManager.LeaderboardEntryData> entries;
        try
        {
            entries = await UGSLeaderboardManager.FetchOfflineEntriesAsync(maxEntries);
        }
        catch
        {
            entries = new List<UGSLeaderboardManager.LeaderboardEntryData>();
        }

        if (entries == null || entries.Count == 0)
        {
            AddSingleRow("No scores yet.");
            return;
        }

        foreach (var e in entries)
        {
            AddRow(
                col1: e.Rank.ToString(),
                col2: e.PlayerName,
                col3: UGSLeaderboardManager.FormatTime(e.TimeSeconds)
            );
        }
    }

    private async Task LoadOnlineAsync()
    {
        ClearRows();

        // First row is always the header row for this view.
        AddHeaderRow(ViewType.Online);

        List<UGSLeaderboardManager.LeaderboardEntryData> entries;
        try
        {
            entries = await UGSLeaderboardManager.FetchOnlineEntriesAsync(maxEntries);
        }
        catch
        {
            entries = new List<UGSLeaderboardManager.LeaderboardEntryData>();
        }

        if (entries == null || entries.Count == 0)
        {
            AddSingleRow("No online records yet.");
            return;
        }

        foreach (var e in entries)
        {
            string record = $"{e.Wins}-{e.Losses}";
            string winPct = $"{e.WinPercentage:0}%";

            AddRow(
                col1: e.PlayerName,
                col2: record,
                col3: winPct
            );
        }
    }

    private void ClearRows()
    {
        if (rowsContainer == null)
            return;

        for (int i = rowsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(rowsContainer.GetChild(i).gameObject);
        }
    }

    private void AddSingleRow(string text)
    {
        if (rowsContainer == null || rowPrefab == null)
            return;

        var row = Instantiate(rowPrefab, rowsContainer);
        var texts = row.GetComponentsInChildren<TMP_Text>();
        if (texts.Length > 0)
        {
            texts[0].text = text;
        }
    }

    private void AddRow(string col1, string col2, string col3)
    {
        if (rowsContainer == null || rowPrefab == null)
            return;

        var row = Instantiate(rowPrefab, rowsContainer);
        var texts = row.GetComponentsInChildren<TMP_Text>();

        // Expecting 3 columns in the row prefab; if fewer, fill what we can.
        if (texts.Length > 0) texts[0].text = col1;
        if (texts.Length > 1) texts[1].text = col2;
        if (texts.Length > 2) texts[2].text = col3;
    }

    /// <summary>
    /// Creates the header row as the first child in the rows container.
    /// Uses the same row prefab but with header labels instead of data.
    /// </summary>
    private void AddHeaderRow(ViewType viewType)
    {
        if (rowsContainer == null || rowPrefab == null)
            return;

        var headerRow = Instantiate(rowPrefab, rowsContainer);
        var texts = headerRow.GetComponentsInChildren<TMP_Text>();
        if (texts.Length < 3) return;

        switch (viewType)
        {
            case ViewType.Offline:
                texts[0].text = "Rank";
                texts[1].text = "Player";
                texts[2].text = "Time";
                break;
            case ViewType.Online:
                texts[0].text = "Player";
                texts[1].text = "Record (W-L)";
                texts[2].text = "Win %";
                break;
        }
    }
}

internal static class TaskExtensions
{
    /// <summary>
    /// Fire-and-forget helper for async void-style usage in Unity (swallows exceptions).
    /// </summary>
    public static async void Forget(this Task task)
    {
        try
        {
            await task;
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}

