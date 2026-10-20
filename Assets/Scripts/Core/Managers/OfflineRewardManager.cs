using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardManager : MonoBehaviour
    {
        public const float DefaultRecordDurationSeconds = OfflineRewardRules.RecordSeconds;
        private OfflineRecordProfile recordingProfile;
        private double recordingStartedAt;
        private DateTime checkpointUtc;
        private bool paused, focused = true, subscribed, applyingRewards;
        private GameManager game;
        private PlayerManager player;
        private MonsterManager monsters;
        private MaterialInventoryManager materials;
        private SaveManager save;
        private long observedGold;

        public bool IsReady { get; private set; }
        public bool IsRecording { get; private set; }
        public bool IsAway => paused || !focused;
        public OfflineRecordProfile LastCompletedProfile { get; private set; }
        public OfflineRecordProfile PendingProfile { get; private set; }
        public OfflineRewardReport LatestReport { get; private set; }
        public event Action Changed;
        public double RemainingSeconds => IsRecording
            ? Math.Max(0d, DefaultRecordDurationSeconds - (Time.realtimeSinceStartupAsDouble - recordingStartedAt)) : 0d;
        public string CheckpointUtcForSave =>
            (IsAway ? checkpointUtc : DateTime.UtcNow).ToString("o");

        private void CacheManagers()
        {
            if (game == null) game = GetComponent<GameManager>();
            if (player == null) player = GetComponent<PlayerManager>();
            if (monsters == null) monsters = GetComponent<MonsterManager>();
            if (materials == null) materials = GetComponent<MaterialInventoryManager>();
            if (save == null) save = GetComponent<SaveManager>();
        }

        private void Subscribe()
        {
            CacheManagers();
            if (game != null) observedGold = game.Gold;
            if (subscribed || game == null || player == null || materials == null) return;
            game.GoldChanged += HandleGoldChanged;
            materials.MaterialChanged += HandleMaterialChanged;
            player.ExperienceGained += HandleExperienceGained;
            subscribed = true;
        }

        private void OnEnable()
        {
            if (IsReady) Subscribe();
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                if (game != null) game.GoldChanged -= HandleGoldChanged;
                if (materials != null) materials.MaterialChanged -= HandleMaterialChanged;
                if (player != null) player.ExperienceGained -= HandleExperienceGained;
            }
            subscribed = false;
            IsRecording = false;
            recordingProfile = null;
        }

        private void Update()
        {
            if (!IsReady) return;
            // If Unity continues running while unfocused, those frames already
            // produce normal rewards. Count only the actual suspended gap.
            if (IsAway) { checkpointUtc = DateTime.UtcNow; return; }
            if (IsRecording) TryFinishRecording();
        }

        public void RestoreLastCompletedProfile(OfflineRecordProfile profile)
        {
            RestoreProgress(profile, null, string.Empty);
        }

        public void RestoreProgress(OfflineRecordProfile profile, OfflineRecordProfile pending, string utc)
        {
            LastCompletedProfile = OfflineRewardRules.CopyProfile(profile);
            PendingProfile = OfflineRewardRules.CopyProfile(pending);
            recordingProfile = null;
            IsRecording = false;
            LatestReport = null;
            IsReady = false;
            if (!OfflineRewardRules.TryParseUtc(utc, out checkpointUtc))
                checkpointUtc = DateTime.UtcNow;
        }

        // Called only after every inventory, player and party has loaded.
        public void InitializeAfterLoad()
        {
            Subscribe();
            IsReady = game != null && player != null && materials != null && save != null;
            if (!IsReady) return;
            ApplyOfflineRewards(DateTime.UtcNow);
            Changed?.Invoke();
        }

        public bool TryBeginRecording(string mapId, out string error)
        {
            error = null;
            if (!IsReady || IsAway || applyingRewards) { error = "記録を開始できません。"; return false; }
            if (IsRecording || PendingProfile != null) { error = "現在の記録結果を先に確認してください。"; return false; }
            recordingProfile = new OfflineRecordProfile
            {
                mapId = mapId ?? string.Empty,
                recordDurationSeconds = DefaultRecordDurationSeconds
            };
            recordingStartedAt = Time.realtimeSinceStartupAsDouble;
            IsRecording = true;
            Changed?.Invoke();
            return true;
        }

        public void BeginRecording(string mapId) => TryBeginRecording(mapId, out _);

        public void CancelRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            recordingProfile = null;
            Changed?.Invoke();
        }

        public void RecordGain(string itemId, long amount) =>
            RecordGain(OfflineGainKind.Material, itemId, amount);

        private void RecordGain(OfflineGainKind kind, string id, long amount)
        {
            if (!IsRecording || IsAway || applyingRewards || amount <= 0L) return;
            // Events later in the deadline frame must not extend the sample.
            if (CanFinishRecording()) { TryFinishRecording(); return; }
            OfflineRewardRules.AddGain(recordingProfile.gains, kind, id, amount);
        }

        private void HandleGoldChanged(long balance)
        {
            long added = balance > observedGold ? balance - observedGold : 0L;
            observedGold = balance;
            RecordGain(OfflineGainKind.Gold, null, added);
        }
        private void HandleMaterialChanged(string id, long delta, long total) => RecordGain(id, delta);
        private void HandleExperienceGained(long amount) => RecordGain(OfflineGainKind.Experience, null, amount);

        public bool CanFinishRecording() => IsRecording &&
            Time.realtimeSinceStartupAsDouble - recordingStartedAt >= DefaultRecordDurationSeconds;

        public bool TryFinishRecording()
        {
            if (!CanFinishRecording()) return false;
            IsRecording = false;
            PendingProfile = OfflineRewardRules.CopyProfile(recordingProfile);
            recordingProfile = null;
            SaveProgress();
            Changed?.Invoke();
            return true;
        }

        public bool TryAcceptPendingProfile(out string error)
        {
            error = null;
            if (!IsReady || IsAway || PendingProfile == null) { error = "確認する記録がありません。"; return false; }
            OfflineRecordProfile old = LastCompletedProfile, pending = PendingProfile;
            DateTime previousCheckpoint = checkpointUtc;
            LastCompletedProfile = OfflineRewardRules.CopyProfile(pending);
            PendingProfile = null;
            checkpointUtc = DateTime.UtcNow;
            if (!save.SaveGame())
            {
                LastCompletedProfile = old;
                PendingProfile = pending;
                checkpointUtc = previousCheckpoint;
                error = "保存に失敗しました。もう一度お試しください。";
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryDiscardPendingProfile(out string error)
        {
            error = null;
            OfflineRecordProfile pending = PendingProfile;
            PendingProfile = null;
            if (save != null && !save.SaveGame())
            {
                PendingProfile = pending;
                error = "保存に失敗しました。もう一度お試しください。";
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public List<OfflineGainRecord> CalculateOfflineGains(double seconds) =>
            OfflineRewardRules.Calculate(LastCompletedProfile, seconds);

        public void SetApplicationPaused(bool value)
        {
            bool wasAway = IsAway;
            paused = value;
            HandleActivityTransition(wasAway);
        }
        public void SetApplicationFocused(bool value)
        {
            bool wasAway = IsAway;
            focused = value;
            HandleActivityTransition(wasAway);
        }
        private void HandleActivityTransition(bool wasAway)
        {
            if (!IsReady || wasAway == IsAway) return;
            if (IsAway)
            {
                checkpointUtc = DateTime.UtcNow;
                if (!TryFinishRecording())
                {
                    CancelRecording();
                    SaveProgress();
                }
            }
            else ApplyOfflineRewards(DateTime.UtcNow);
        }

        public void PrepareForQuit()
        {
            if (!TryFinishRecording()) CancelRecording();
            if (!IsAway) checkpointUtc = DateTime.UtcNow;
        }

        private void ApplyOfflineRewards(DateTime now)
        {
            if (!IsReady || applyingRewards) return;
            double seconds = Math.Max(0d, (now - checkpointUtc).TotalSeconds);
            checkpointUtc = now; // Consume once before publishing any resource events.
            List<OfflineGainRecord> calculated = CalculateOfflineGains(seconds);
            if (calculated.Count == 0) { SaveProgress(); return; }
            applyingRewards = true;
            try
            {
                var report = new OfflineRewardReport { elapsedSeconds = seconds };
                foreach (OfflineGainRecord gain in calculated)
                {
                    long actual = 0L;
                    switch (gain.kind)
                    {
                        case OfflineGainKind.Gold:
                            long before = game.Gold;
                            game.AddGold(gain.amount);
                            actual = Math.Max(0L, game.Gold - before);
                            break;
                        case OfflineGainKind.Experience:
                            actual = AwardExperience(gain.amount);
                            break;
                        case OfflineGainKind.Material:
                            actual = materials.AddMaterial(gain.itemId, gain.amount);
                            break;
                    }
                    OfflineRewardRules.AddGain(report.gains, gain.kind, gain.itemId, actual);
                }
                if (report.gains.Count > 0)
                {
                    if (LatestReport == null) LatestReport = report;
                    else
                    {
                        LatestReport.elapsedSeconds += report.elapsedSeconds;
                        foreach (OfflineGainRecord gain in report.gains)
                            OfflineRewardRules.AddGain(LatestReport.gains, gain.kind, gain.itemId, gain.amount);
                    }
                }
                // Rewards and consumed checkpoint are captured in the same save.
                SaveProgress();
            }
            finally { applyingRewards = false; }
            Changed?.Invoke();
        }

        private long AwardExperience(long amount)
        {
            long awarded = 0L;
            var party = monsters != null
                ? new List<string>(monsters.PartyMonsterUniqueIds) : new List<string>();
            if (player.Level < ExperienceProgression.MaxLevel)
            {
                player.AddExperience(amount);
                awarded = amount;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in party)
            {
                if (!seen.Add(id)) continue;
                OwnedMonsterRecord record = monsters.FindOwnedMonster(id);
                if (record == null || record.level >= ExperienceProgression.MaxLevel) continue;
                monsters.AddExperience(id, amount);
                awarded = amount; // One shared EXP rate, never multiplied by party size.
            }
            return awarded;
        }

        public void AcknowledgeReport()
        {
            LatestReport = null;
            Changed?.Invoke();
        }

        private void SaveProgress()
        {
            if (save != null && !save.SaveGame())
                Debug.LogWarning("[OfflineReward] 保存を確認できませんでした。次の自動保存で再試行します。", this);
        }
    }
}
