using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardManager : MonoBehaviour
    {
        public const float DefaultRecordDurationSeconds = 30f;

        private OfflineRecordProfile recordingProfile;
        private float recordingStartedAt;

        public bool IsRecording { get; private set; }
        public OfflineRecordProfile LastCompletedProfile { get; private set; }

        public void RestoreLastCompletedProfile(OfflineRecordProfile profile)
        {
            LastCompletedProfile = profile;
            recordingProfile = null;
            IsRecording = false;
        }

        public void BeginRecording(string mapId)
        {
            recordingProfile = new OfflineRecordProfile
            {
                mapId = mapId ?? string.Empty,
                recordDurationSeconds = DefaultRecordDurationSeconds,
                gains = new List<OfflineGainRecord>()
            };

            recordingStartedAt = Time.realtimeSinceStartup;
            IsRecording = true;
        }

        public void RecordGain(string itemId, long amount)
        {
            if (!IsRecording || amount <= 0 || string.IsNullOrEmpty(itemId)) return;

            var gain = recordingProfile.gains.Find(x => x.itemId == itemId);
            if (gain == null)
            {
                gain = new OfflineGainRecord { itemId = itemId, amount = 0 };
                recordingProfile.gains.Add(gain);
            }

            gain.amount = LongMath.SaturatingAdd(gain.amount, amount);
        }

        public bool CanFinishRecording()
        {
            return IsRecording &&
                   Time.realtimeSinceStartup - recordingStartedAt >= recordingProfile.recordDurationSeconds;
        }

        public bool TryFinishRecording()
        {
            if (!CanFinishRecording()) return false;

            LastCompletedProfile = recordingProfile;
            recordingProfile = null;
            IsRecording = false;
            return true;
        }

        /// <summary>
        /// Calculates offline gains from the 30-second recorded profile.
        /// Ad confirmation should be handled by the UI/ad layer before granting these values.
        /// </summary>
        public List<OfflineGainRecord> CalculateOfflineGains(double offlineSeconds)
        {
            var result = new List<OfflineGainRecord>();
            var profile = LastCompletedProfile;

            if (profile == null || offlineSeconds <= 0d || profile.recordDurationSeconds <= 0f)
                return result;

            double multiplier = offlineSeconds / profile.recordDurationSeconds;

            foreach (var gain in profile.gains)
            {
                if (gain == null || gain.amount <= 0) continue;

                double calculated = gain.amount * multiplier;
                long amount = calculated >= long.MaxValue ? long.MaxValue : Math.Max(0L, (long)Math.Floor(calculated));

                result.Add(new OfflineGainRecord
                {
                    itemId = gain.itemId,
                    amount = amount
                });
            }

            return result;
        }
    }
}