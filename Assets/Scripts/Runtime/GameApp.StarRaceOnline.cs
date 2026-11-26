using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        const string StarRaceLeaderboardId = "star-race-weekly";
        const int RankingLimit = 100;
        const int RankingFetchSize = 50;
        readonly SemaphoreSlim starRaceRequests = new SemaphoreSlim(1);
        IReadOnlyList<LeaderboardEntry> starRaceEntries = Array.Empty<LeaderboardEntry>();
        LeaderboardEntry starRacePlayerEntry;
        Vector2 rankingScroll;
        bool rankingDragging;
        bool starRaceLoading;
        string starRaceError;
        GameMode? rankingMode;
        readonly Dictionary<string, (string period, int score, string name, int avatar)> rankingSubmissions =
            new Dictionary<string, (string, int, string, int)>();
        static readonly GameMode?[] RankingTabs =
        {
            null, GameMode.Classic, GameMode.Daily, GameMode.ArrowSwitch, GameMode.ArrowWorkshop, GameMode.ArrowChameleon
        };

        [Serializable]
        sealed class StarRaceProfile
        {
            public string name;
            public int avatar;
        }

        void OpenStarRace()
        {
            if (!FeatureUnlocks.StarRaceUnlocked(Progress))
            {
                Notify("Unlock at level " + FeatureUnlocks.StarRaceLevel);
                return;
            }
            OpenRankings(null);
        }

        void OpenRankings(GameMode? mode)
        {
            starRaceOpen = true;
            SelectRanking(Array.IndexOf(RankingTabs, mode) >= 0 ? mode : GameMode.Classic);
        }

        void SelectRanking(GameMode? mode)
        {
            rankingMode = mode;
            starRacePlayerEntry = null;
            starRaceEntries = Array.Empty<LeaderboardEntry>();
            rankingScroll = Vector2.zero;
            rankingDragging = false;
            _ = RefreshStarRaceAsync();
        }

        static string RankingId(GameMode? mode) => mode switch
        {
            null => StarRaceLeaderboardId,
            GameMode.Classic => "classic-all-time",
            GameMode.Daily => "daily-fastest",
            GameMode.ArrowSwitch => "arrow-switch-all-time",
            GameMode.ArrowWorkshop => "arrow-workshop-all-time",
            GameMode.ArrowChameleon => "arrow-chameleon-all-time",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        static string RankingPeriod(GameMode? mode, DateTime utc) => mode switch
        {
            null => StarRace.WeekKey(utc),
            GameMode.Daily => utc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            _ => ""
        };

        void RecordRankedWin()
        {
            if (Mode == GameMode.Daily)
            {
                // A puzzle started before midnight belongs to the previous board.
                if (dailyDate != DateTime.UtcNow.Date) return;
                int time = Mathf.Max(1, Mathf.RoundToInt(elapsed * 1000));
                if (Progress.rankedDailyDate != DailyDateKey || time < Progress.rankedDailyMilliseconds)
                {
                    Progress.rankedDailyDate = DailyDateKey;
                    Progress.rankedDailyMilliseconds = time;
                }
            }
            else if (Array.IndexOf(RankingTabs, (GameMode?)Mode) >= 0)
                Progress.rankedLevels[(int)Mode] = Mathf.Max(Progress.rankedLevels[(int)Mode], LevelIndex + 1);
        }

        int RankingScore(GameMode? mode, string period) => mode switch
        {
            null => CurrentStarRaceStars(period),
            GameMode.Daily => Progress.rankedDailyDate == period ? Progress.rankedDailyMilliseconds : 0,
            GameMode value => Progress.rankedLevels[(int)value]
        };

        async Task SyncRankedWinAsync(GameMode mode)
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId) || Array.IndexOf(RankingTabs, (GameMode?)mode) < 0) return;
            await SyncRankingAsync(mode);
            if (this && mode == GameMode.Classic && FeatureUnlocks.StarRaceUnlocked(Progress))
                await SyncRankingAsync(null);
        }

        async Task SyncRankingAsync(GameMode? mode)
        {
            await starRaceRequests.WaitAsync();
            try
            {
                if (this) await SubmitRankingScoreAsync(mode);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Ranking score not synced: " + error.Message);
            }
            finally { starRaceRequests.Release(); }
        }

        static async Task ConnectStarRaceAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        async Task SubmitRankingScoreAsync(GameMode? mode)
        {
            string period = RankingPeriod(mode, DateTime.UtcNow);
            int score = RankingScore(mode, period);
            if (score <= 0) return;
            string name = NormalizeProfileName(Progress.playerName);
            int avatar = Progress.avatarIndex;
            await ConnectStarRaceAsync();
            if (!this || period != RankingPeriod(mode, DateTime.UtcNow)) return;
            string id = RankingId(mode);
            string key = AuthenticationService.Instance.PlayerId + "/" + id;
            var snapshot = (period, score, name, avatar);
            if (rankingSubmissions.TryGetValue(key, out var submitted) && snapshot == submitted) return;
            await LeaderboardsService.Instance.AddPlayerScoreAsync(id, score,
                new AddPlayerScoreOptions { Metadata = new StarRaceProfile { name = name, avatar = avatar } });
            rankingSubmissions[key] = snapshot;
        }

        async Task RefreshStarRaceAsync()
        {
            if (starRaceLoading) return;
            starRaceLoading = true;
            starRaceError = null;
            var mode = rankingMode;
            await starRaceRequests.WaitAsync();
            try
            {
                if (!this) return;
                if (string.IsNullOrEmpty(Application.cloudProjectId))
                {
                    starRaceError = "Online rankings are not configured yet.";
                    return;
                }
                await ConnectStarRaceAsync();
                string id = RankingId(mode);
                await SubmitRankingScoreAsync(mode);
                var entries = new List<LeaderboardEntry>(RankingLimit);
                while (entries.Count < RankingLimit)
                {
                    var page = await LeaderboardsService.Instance.GetScoresAsync(id,
                        new GetScoresOptions { Offset = entries.Count, Limit = RankingFetchSize, IncludeMetadata = true });
                    if (!this || rankingMode != mode) return;
                    entries.AddRange(page.Results);
                    if (page.Results.Count < RankingFetchSize || entries.Count >= page.Total) break;
                }
                LeaderboardEntry playerEntry = null;
                try
                {
                    playerEntry = await LeaderboardsService.Instance.GetPlayerScoreAsync(id);
                }
                catch (LeaderboardsException error) when (error.Reason == LeaderboardsExceptionReason.EntryNotFound ||
                    error.Reason == LeaderboardsExceptionReason.ScoreSubmissionRequired)
                {
                    // The player can browse the leaderboard before submitting a score.
                }
                if (!this || rankingMode != mode) return;
                starRaceEntries = entries;
                starRacePlayerEntry = playerEntry;
            }
            catch (Exception error)
            {
                if (this)
                {
                    starRaceError = "Could not load rankings. Tap Refresh to try again.";
                    Debug.LogWarning("Star Race rankings: " + error.Message);
                }
            }
            finally
            {
                starRaceLoading = false;
                starRaceRequests.Release();
                if (this && starRaceOpen && rankingMode != mode) _ = RefreshStarRaceAsync();
            }
        }
    }
}
