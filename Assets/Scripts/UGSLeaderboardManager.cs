using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

/// <summary>
/// Manager for interacting with Unity Gaming Services leaderboards.
/// Supports both the existing single-player time leaderboard and a new online wins leaderboard.
/// </summary>
public static class UGSLeaderboardManager
{
    // Existing offline leaderboard id (unchanged).
    private const string OfflineLeaderboardId = "SinglePlayerTimes";

    // New online leaderboard id (must match UGS dashboard exactly).
    private const string OnlineLeaderboardId = "online_wins";

    /// <summary>
    /// Type of leaderboard entry.
    /// </summary>
    public enum LeaderboardType
    {
        OfflineTime,
        OnlineWins
    }

    /// <summary>
    /// Structured data for a single leaderboard entry.
    /// UI scripts should consume this instead of preformatted strings.
    /// </summary>
    public class LeaderboardEntryData
    {
        public LeaderboardType Type;
        public int Rank;              // 1-based
        public string PlayerId;
        public string PlayerName;

        // Offline-specific
        public float TimeSeconds;

        // Online-specific
        public int Wins;
        public int Losses;

        public float WinPercentage
        {
            get
            {
                int total = Wins + Losses;
                if (total <= 0) return 0f;
                return Mathf.Round((Wins / (float)total) * 100f);
            }
        }
    }

    #region Offline (Single Player) API

    /// <summary>
    /// Existing API used by the offline game to submit a single-player time.
    /// </summary>
    public static async Task SubmitScore(float timeInSeconds)
    {
        Debug.Log($"[UGSLeaderboardManager] SubmitScore called with time={timeInSeconds:0.000}s");
        try
        {
            await EnsureInitializedAsync();
            await LeaderboardsService.Instance.AddPlayerScoreAsync(OfflineLeaderboardId, (double)timeInSeconds);
            Debug.Log("[UGSLeaderboardManager] SubmitScore completed successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UGSLeaderboardManager] SubmitScore failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// New structured API: fetch offline leaderboard entries.
    /// </summary>
    public static async Task<List<LeaderboardEntryData>> FetchOfflineEntriesAsync(int limit = 10)
    {
        Debug.Log($"[UGSLeaderboardManager] FetchOfflineEntriesAsync called with limit={limit}.");
        await EnsureInitializedAsync();

        var list = new List<LeaderboardEntryData>();

        try
        {
            var response = await LeaderboardsService.Instance.GetScoresAsync(
                OfflineLeaderboardId,
                new GetScoresOptions { Limit = limit });

            if (response?.Results == null)
            {
                Debug.Log("[UGSLeaderboardManager] FetchOfflineEntriesAsync returned 0 results.");
                return list;
            }

            foreach (var entry in response.Results)
            {
                list.Add(new LeaderboardEntryData
                {
                    Type = LeaderboardType.OfflineTime,
                    Rank = entry.Rank + 1,
                    PlayerId = entry.PlayerId,
                    PlayerName = GetSafePlayerName(entry),
                    TimeSeconds = (float)entry.Score
                });
            }

            Debug.Log($"[UGSLeaderboardManager] FetchOfflineEntriesAsync parsed {list.Count} entries.");
            return list;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UGSLeaderboardManager] FetchOfflineEntriesAsync failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Legacy string-based API used by the current UI.
    /// This is preserved for backward compatibility, but now builds from structured data.
    /// </summary>
    public static async Task<string> FetchTopScores()
    {
        Debug.Log("[UGSLeaderboardManager] FetchTopScores (legacy) called.");
        var entries = await FetchOfflineEntriesAsync();
        if (entries == null || entries.Count == 0)
            return "No scores yet.";

        var sb = new StringBuilder();
        foreach (var e in entries)
        {
            sb.AppendLine($"{e.Rank}. {e.PlayerName} - {FormatTime(e.TimeSeconds)}");
        }
        return sb.ToString().TrimEnd();
    }

    public static string FormatTime(float timeInSeconds)
    {
        int minutes = (int)(timeInSeconds / 60f);
        int seconds = (int)(timeInSeconds % 60f);
        return $"{minutes:D2}:{seconds:D2}";
    }

    #endregion

    #region Online (Multiplayer) API

    /// <summary>
    /// Submit a multiplayer record. Wins are the primary leaderboard score.
    /// Losses are stored as metadata on the score entry.
    /// </summary>
    public static async Task SubmitOnlineResultAsync(int wins, int losses)
    {
        Debug.Log($"[UGSLeaderboardManager] SubmitOnlineResultAsync called with wins={wins}, losses={losses}.");
        try
        {
            await EnsureInitializedAsync();

            // UGS expects Metadata as a key/value map; it will serialize this
            // to a JSON object like: {"losses": "1"}.
            var metadata = new Dictionary<string, string>
            {
                { "losses", Mathf.Max(0, losses).ToString() }
            };

            var options = new AddPlayerScoreOptions
            {
                Metadata = metadata
            };

            await LeaderboardsService.Instance.AddPlayerScoreAsync(OnlineLeaderboardId, wins, options);
            Debug.Log("[UGSLeaderboardManager] SubmitOnlineResultAsync completed successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UGSLeaderboardManager] SubmitOnlineResultAsync failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Convenience overload used by game code: pass true for a win, false for a loss.
    /// Fetches the current cloud record first, then increments wins or losses.
    /// </summary>
    public static async Task SubmitOnlineResultAsync(bool isWin)
    {
        await EnsureInitializedAsync();

        int wins = 0;
        int losses = 0;

        try
        {
            // Get the player's current leaderboard entry (if any) so we can increment.
            var currentEntry = await LeaderboardsService.Instance.GetPlayerScoreAsync(
                OnlineLeaderboardId,
                new GetPlayerScoreOptions { IncludeMetadata = true });
            if (currentEntry != null)
            {
                wins = (int)currentEntry.Score;

                if (!string.IsNullOrEmpty(currentEntry.Metadata))
                {
                    try
                    {
                        var meta = JsonUtility.FromJson<UGSMetadata>(currentEntry.Metadata);
                        if (!string.IsNullOrEmpty(meta.losses) && int.TryParse(meta.losses, out var parsedLosses))
                        {
                            losses = Mathf.Max(0, parsedLosses);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UGSLeaderboardManager] Failed to parse existing online losses metadata: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If there is no existing score yet (404), we treat this as 0-0 and continue.
            Debug.LogWarning($"[UGSLeaderboardManager] GetPlayerScoreAsync for online_wins failed; starting from 0-0. Exception: {ex.Message}");
        }

        if (isWin)
            wins++;
        else
            losses++;

        Debug.Log($"[UGSLeaderboardManager] SubmitOnlineResultAsync(bool) called. isWin={isWin}, newTotals: wins={wins}, losses={losses}.");
        await SubmitOnlineResultAsync(wins, losses);
    }

    /// <summary>
    /// Fetch structured entries for the online "wins" leaderboard.
    /// </summary>
    public static async Task<List<LeaderboardEntryData>> FetchOnlineEntriesAsync(int limit = 10)
    {
        Debug.Log($"[UGSLeaderboardManager] FetchOnlineEntriesAsync called with limit={limit}.");
        await EnsureInitializedAsync();

        var list = new List<LeaderboardEntryData>();

        try
        {
            var response = await LeaderboardsService.Instance.GetScoresAsync(
                OnlineLeaderboardId,
                new GetScoresOptions { Limit = limit, IncludeMetadata = true });

            if (response?.Results == null)
            {
                Debug.Log("[UGSLeaderboardManager] FetchOnlineEntriesAsync returned 0 results.");
                return list;
            }

            foreach (var entry in response.Results)
            {
                int wins = (int)entry.Score;
                int losses = 0;

                try
                {
                    // Metadata is stored as JSON like: {"losses": "1"}
                    if (!string.IsNullOrEmpty(entry.Metadata))
                    {
                        var meta = JsonUtility.FromJson<UGSMetadata>(entry.Metadata);
                        if (!string.IsNullOrEmpty(meta.losses) && int.TryParse(meta.losses, out var parsedLosses))
                        {
                            losses = Mathf.Max(0, parsedLosses);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to parse losses metadata for leaderboard entry: {ex.Message}");
                }

                list.Add(new LeaderboardEntryData
                {
                    Type = LeaderboardType.OnlineWins,
                    Rank = entry.Rank + 1,
                    PlayerId = entry.PlayerId,
                    PlayerName = GetSafePlayerName(entry),
                    Wins = Mathf.Max(0, wins),
                    Losses = Mathf.Max(0, losses)
                });
            }

            Debug.Log($"[UGSLeaderboardManager] FetchOnlineEntriesAsync parsed {list.Count} entries.");
            return list;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UGSLeaderboardManager] FetchOnlineEntriesAsync failed: {ex}");
            throw;
        }
    }

    #endregion

    private static bool s_Initialized;

    /// <summary>
    /// Ensures Unity Services are initialized and the cloud player name matches the local saved name (if any).
    /// </summary>
    private static async Task EnsureInitializedAsync()
    {
        if (s_Initialized)
        {
            await EnsurePlayerNameSyncedAsync();
            return;
        }

        Debug.Log("[UGSLeaderboardManager] EnsureInitializedAsync called.");
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("[UGSLeaderboardManager] UnityServices is Uninitialized. Initializing now...");
                await UnityServices.InitializeAsync();
            }

            // Ensure we have an auth token before calling any Leaderboards APIs.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[UGSLeaderboardManager] AuthenticationService not signed in. Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("[UGSLeaderboardManager] Anonymous sign-in completed.");
            }

            s_Initialized = UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
            Debug.Log($"[UGSLeaderboardManager] UnityServices initialization state: {UnityServices.State}, signedIn={AuthenticationService.Instance.IsSignedIn}, s_Initialized={s_Initialized}.");

            // After services + auth are ready, make sure the cloud player name matches the local one.
            await EnsurePlayerNameSyncedAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UGSLeaderboardManager] EnsureInitializedAsync failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// If there is a local PlayerPrefs name and it differs from the cloud name, push it to UGS.
    /// This runs safely on all platforms (including WebGL/guests).
    /// </summary>
    private static async Task EnsurePlayerNameSyncedAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            return;

        var savedName = PlayerPrefs.GetString("PlayerName", string.Empty);
        if (string.IsNullOrWhiteSpace(savedName))
            return;

        // If UGS already has this name, nothing to do.
        if (string.Equals(AuthenticationService.Instance.PlayerName, savedName, StringComparison.Ordinal))
            return;

        try
        {
            Debug.Log($"[UGSLeaderboardManager] Syncing cloud player name to '{savedName}'.");
            await AuthenticationService.Instance.UpdatePlayerNameAsync(savedName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UGSLeaderboardManager] EnsurePlayerNameSyncedAsync failed: {ex.Message}");
        }
    }

    [Serializable]
    public struct UGSMetadata
    {
        // Matches dashboard JSON: {"losses": "1"}
        public string losses;
    }

    private static string GetSafePlayerName(LeaderboardEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.PlayerName))
            return entry.PlayerName;

        // If no explicit name has been set yet, show a friendly label instead of the raw ID.
        if (!string.IsNullOrEmpty(entry.PlayerId))
            return "Guest";

        return "New Player";
    }
}
