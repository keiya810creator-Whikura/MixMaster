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
                if (SkillActor.PlayerActor != null) SkillActor.PlayerActor.ResetTransient();
                save?.SaveGame();
            }

            if (panel != null && panel.IsDecisionVisible) return;
            var body = FindFirstObjectByType<PlayerController>();
            if (body == null) return;
            panel = PlayerDeathPanelUI.FindOrCreate(body.gameObject.scene);
            if (panel == null) return;

            panel.ShowDecision(game.Gold / 2L, ReturnToTown);
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

        private void ReturnToTown()
        {
            if (!awaitingReturn || returning || player.CurrentHp > 0L) return;
            returning = true;
            try
            {
                if (!TryWarpToTown())
                {
                    panel.ShowReturnFailed();
                    return;
                }

                long lostGold = game.Gold / 2L;
                if (lostGold > 0L) game.TrySpendGold(lostGold);

                awaitingReturn = false;
                saveDefeat = false;
                save?.SaveGame();
                ReleasePause();
                if (panel != null) panel.ShowReturnNotice(lostGold);
            }
            finally
            {
                returning = false;
            }
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
