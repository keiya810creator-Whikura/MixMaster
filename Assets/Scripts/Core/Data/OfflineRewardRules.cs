using System;
using System.Collections.Generic;
using System.Globalization;

namespace MixMaster.Core
{
    public enum OfflineGainKind { Material = 0, Gold = 1, Experience = 2 }

    public sealed class OfflineRewardReport
    {
        public double elapsedSeconds;
        public readonly List<OfflineGainRecord> gains = new List<OfflineGainRecord>();
    }

    public static class OfflineRewardRules
    {
        public const float RecordSeconds = 180f;
        public const double Efficiency = 0.5d;

        public static OfflineRecordProfile CopyProfile(OfflineRecordProfile source)
        {
            if (source == null || float.IsNaN(source.recordDurationSeconds) ||
                float.IsInfinity(source.recordDurationSeconds) || source.recordDurationSeconds < 1f ||
                source.recordDurationSeconds > 86400f)
                return null;
            var copy = new OfflineRecordProfile
            {
                mapId = source.mapId ?? string.Empty,
                recordDurationSeconds = source.recordDurationSeconds
            };
            if (source.gains != null)
                foreach (OfflineGainRecord gain in source.gains)
                    if (gain != null) AddGain(copy.gains, gain.kind, gain.itemId, gain.amount);
            return copy;
        }

        public static void AddGain(List<OfflineGainRecord> gains, OfflineGainKind kind, string id, long amount)
        {
            if (amount <= 0L || !Enum.IsDefined(typeof(OfflineGainKind), kind) ||
                (kind == OfflineGainKind.Material && string.IsNullOrWhiteSpace(id))) return;
            id = kind == OfflineGainKind.Material ? id : string.Empty;
            OfflineGainRecord existing = gains.Find(x => x.kind == kind && x.itemId == id);
            if (existing == null)
            {
                existing = new OfflineGainRecord { kind = kind, itemId = id };
                gains.Add(existing);
            }
            existing.amount = LongMath.SaturatingAdd(existing.amount, amount);
        }

        public static double PerMinute(OfflineRecordProfile profile, OfflineGainRecord gain)
        {
            return profile != null && gain != null && profile.recordDurationSeconds > 0f
                ? gain.amount * (60d / profile.recordDurationSeconds) : 0d;
        }

        public static decimal ComparisonRate(OfflineRecordProfile profile,
            OfflineGainKind kind, string itemId = null)
        {
            if (profile == null || float.IsNaN(profile.recordDurationSeconds) ||
                float.IsInfinity(profile.recordDurationSeconds) ||
                profile.recordDurationSeconds < 1f || profile.recordDurationSeconds > 86400f ||
                profile.gains == null) return 0m;
            long amount = 0;
            foreach (OfflineGainRecord gain in profile.gains)
                if (gain != null && gain.kind == kind &&
                    (kind != OfflineGainKind.Material || string.Equals(gain.itemId,
                        itemId, StringComparison.Ordinal)))
                    amount = LongMath.SaturatingAdd(amount, Math.Max(0L, gain.amount));
            return (decimal)amount * 60m / (decimal)profile.recordDurationSeconds;
        }

        public static decimal PerMinuteDifference(OfflineRecordProfile current,
            OfflineRecordProfile previous, OfflineGainKind kind, string itemId = null) =>
            ComparisonRate(current, kind, itemId) - ComparisonRate(previous, kind, itemId);

        public static List<OfflineGainRecord> Calculate(OfflineRecordProfile profile, double seconds)
        {
            var result = new List<OfflineGainRecord>();
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0d) return result;
            profile = CopyProfile(profile);
            if (profile == null) return result;
            foreach (OfflineGainRecord gain in profile.gains)
            {
                // Preserve the recording precision; round only the final payout.
                long amount;
                try
                {
                    decimal value = decimal.Floor((decimal)gain.amount * (decimal)seconds /
                        (decimal)profile.recordDurationSeconds * (decimal)Efficiency);
                    amount = value >= long.MaxValue ? long.MaxValue : Math.Max(0L, (long)value);
                }
                catch (OverflowException)
                {
                    // A valid sample lasts at most one day; overflowing its
                    // numerator already guarantees a payout above the long cap.
                    amount = long.MaxValue;
                }
                if (amount > 0L)
                    result.Add(new OfflineGainRecord { kind = gain.kind, itemId = gain.itemId, amount = amount });
            }
            return result;
        }

        public static bool TryParseUtc(string text, out DateTime utc)
        {
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                utc = parsed.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : parsed.ToUniversalTime();
                return true;
            }
            utc = default;
            return false;
        }
    }
}
