using MixMaster.Combat;
using MixMaster.Player;
using MixMaster.UI;
using MixMaster.World;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameManager), typeof(PlayerManager), typeof(SaveManager))]
    public sealed class PlayerDeathManager : MonoBehaviour
    {
        private GameManager game;
        private PlayerManager player;
        private SaveManager save;
        private PlayerDeathPanelUI panel;
        private bool awaitingReturn;
        private bool adPending;
        private int defeatVersion;
        private bool returning;
        private bool saveDefeat;
        private bool paused;
        private float previousTimeScale;

        private void OnEnable()
        {
            game = GetComponent<GameManager>();
            player = GetComponent<PlayerManager>();
            save = GetComponent<SaveManager>();
            player.HpChanged += HandleHealthChanged;
            game.GoldChanged += HandleGoldChanged;
            HandleHealthChanged(player.CurrentHp, 0L);
        }

        private void OnDisable()
        {
            if (player != null) player.HpChanged -= HandleHealthChanged;
            if (game != null) game.GoldChanged -= HandleGoldChanged;
            if (panel != null) panel.HideDecision();
            ReleasePause();
        }

        private void HandleHealthChanged(long current, long maximum)
        {
            if (returning) return;
            if (current > 0L)
            {
                if (!awaitingReturn) return;
                awaitingReturn = false;
                saveDefeat = false;
                if (panel != null) panel.HideDecision();
                ReleasePause();
                return;
            }

            if (awaitingReturn) return;
            awaitingReturn = true;
            defeatVersion++;
            saveDefeat = true;
        }

        private void LateUpdate()
        {
            if (!awaitingReturn) return;
            if (saveDefeat)
            {
                // Save outside the health event so that loading a defeated
                // character can finish restoring the rest of the save first.
                saveDefeat = false;
                GameAudio.Play(SoundCue.Defeat);
                if (SkillActor.PlayerActor != null) SkillActor.PlayerActor.ResetTransient();
                save?.SaveGame();
            }

            if (panel != null && panel.IsDecisionVisible) return;
            var body = FindFirstObjectByType<PlayerController>();
            if (body == null) return;
            panel = PlayerDeathPanelUI.FindOrCreate(body.gameObject.scene);
            if (panel == null) return;

            panel.ShowDecision(game.Gold / 2L, ReturnWithPenalty, ReviveWithAd, ReturnWithAd);
            panel.SetActionsEnabled(!adPending);
            if (!paused)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                paused = true;
            }
        }

        private void HandleGoldChanged(long gold)
        {
            if (awaitingReturn && !returning && panel != null && panel.IsDecisionVisible)
                panel.ShowProjectedLoss(gold / 2L);
        }

        private void ReturnWithPenalty()
        {
            if (!adPending) ReturnToTown(true);
        }

        private void ReviveWithAd() => WatchAd(Revive);

        private void ReturnWithAd() => WatchAd(() => ReturnToTown(false));

        private void WatchAd(System.Action reward)
        {
            if (!awaitingReturn || returning || adPending || player.CurrentHp > 0L) return;
            var ads = RewardedAdManager.Instance;
            if (ads == null)
            {
                panel.ShowStatus("広告を利用できません。");
                return;
            }

            int defeat = defeatVersion;
            adPending = true;
            panel.SetActionsEnabled(false);
            panel.ShowStatus(string.Empty);
            if (ads.TryShow(message =>
            {
                if (this == null) return;
                adPending = false;
                if (panel != null) panel.SetActionsEnabled(true);
                if (!awaitingReturn || defeat != defeatVersion || player.CurrentHp > 0L) return;
                if (message == null) reward();
                else if (panel != null) panel.ShowStatus(message);
            }, out string error)) return;

            adPending = false;
            panel.SetActionsEnabled(true);
            panel.ShowStatus(error);
        }

        private void Revive()
        {
            returning = true;
            try
            {
                player.RestoreFullResources();
                FinishRecovery("その場で復活しました\nHP・MPが全回復しました。");
            }
            finally { returning = false; }
        }

        private void ReturnToTown(bool loseGold)
        {
            if (!awaitingReturn || returning || player.CurrentHp > 0L) return;
            returning = true;
            try
            {
                if (!TryWarpToTown())
                {
                    if (panel != null) panel.ShowStatus("街へ帰還できませんでした。もう一度お試しください。");
                    return;
                }

                long lostGold = loseGold ? game.Gold / 2L : 0L;
                if (lostGold > 0L) game.TrySpendGold(lostGold);
                FinishRecovery(loseGold
                    ? $"街に帰還しました\n{lostGold:N0} Gを失いました。"
                    : "街に帰還しました\n所持金はそのままです。");
            }
            finally
            {
                returning = false;
            }
        }

        private void FinishRecovery(string message)
        {
            awaitingReturn = false;
            saveDefeat = false;
            save?.SaveGame();
            ReleasePause();
            if (panel != null) panel.ShowOutcomeNotice(message);
        }

        private static bool TryWarpToTown()
        {
            var points = FindObjectsByType<VillageSpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var point in points)
                if (point.TryRespawnPlayer(endDungeon: true))
                    return true;
            return false;
        }

        private void ReleasePause()
        {
            if (!paused) return;
            if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
            paused = false;
        }
    }
}
