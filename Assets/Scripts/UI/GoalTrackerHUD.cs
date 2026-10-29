using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.UI
{
    /// <summary>Seven fixed rows, no per-frame scene queries or inventory rebuilds.</summary>
    [DisallowMultipleComponent]
    public sealed class GoalTrackerHUD : MonoBehaviour
    {
        public const string ResourcePath = "UI/Goals/GoalTrackerHUD";
        [SerializeField] private RectTransform panel, viewport, contentRoot;
        [SerializeField] private GameObject[] rows;
        [SerializeField] private TMP_Text[] names, counts;
        [SerializeField, Min(1)] private float rowHeight = 44f, headerHeight = 52f;
        private GameJournalManager journal;
        private MonsterManager monsters;
        private EquipmentManager equipment;
        private MaterialInventoryManager materials;
        private GameGuideContent definitions;
        private readonly string[] boundIds = new string[GameJournalManager.TrackedGoalLimit];
        private readonly long[] boundCounts = new long[GameJournalManager.TrackedGoalLimit], boundTargets = new long[GameJournalManager.TrackedGoalLimit];
        private bool dirty = true;
        private float refreshAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            EnsureHUD();
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureHUD();
        private static void EnsureHUD()
        {
            if (FindFirstObjectByType<GoalTrackerHUD>(FindObjectsInactive.Include) != null) return;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab != null) Instantiate(prefab, canvas.transform, false);
                return;
            }
        }
        private void OnEnable() { Bind(); dirty = true; }
        private void OnDisable() { Unbind(); }
        private void OnDestroy() { Unbind(); }
        private void Bind()
        {
            var current = GameJournalManager.Instance;
            if (journal == current && journal != null) return;
            Unbind(); journal = current;
            if (journal == null) return;
            monsters = journal.GetComponent<MonsterManager>();
            equipment = journal.GetComponent<EquipmentManager>();
            materials = journal.GetComponent<MaterialInventoryManager>();
            journal.Changed += MarkDirty;
            definitions = new GameGuideContent(MonsterCatalogSO.Load(), EquipmentCatalogSO.Load(), MapCatalogSO.Load());
            for (int i = 0; i < boundIds.Length; i++) boundIds[i] = null;
        }
        private void Unbind()
        {
            if (journal != null) journal.Changed -= MarkDirty;
            journal = null;
        }
        private void MarkDirty() { dirty = true; }
        private void LateUpdate()
        {
            if (!dirty && Time.unscaledTime < refreshAt) return;
            Bind(); dirty = false; refreshAt = Time.unscaledTime + 1f;
            Refresh();
        }
        private void Refresh()
        {
            if (panel == null || rows == null || names == null || counts == null) return;
            int shown = 0;
            if (journal != null) for (int i = 0; i < journal.Goals.Count && shown < GameJournalManager.TrackedGoalLimit; i++)
            {
                GoalRecord goal = journal.Goals[i];
                if (!goal.showOnHud) continue;
                if (shown >= rows.Length || shown >= names.Length || shown >= counts.Length) break;
                long owned = GameGuideContent.Owned(goal, monsters, equipment, materials);
                if (boundIds[shown] != goal.goalId)
                {
                    boundIds[shown] = goal.goalId;
                    if (names[shown] != null) { names[shown].text = definitions.Resolve(goal).name; names[shown].richText = false; names[shown].overflowMode = TextOverflowModes.Ellipsis; }
                    boundCounts[shown] = -1;
                }
                if (boundCounts[shown] != owned || boundTargets[shown] != goal.targetCount)
                {
                    boundCounts[shown] = owned; boundTargets[shown] = goal.targetCount;
                    if (counts[shown] != null)
                    {
                        counts[shown].text = GameGuideContent.Number(owned) + " / " + GameGuideContent.Number(goal.targetCount);
                        counts[shown].color = owned >= goal.targetCount ? new Color(.45f, 1f, .65f, 1) : Color.white;
                    }
                }
                if (rows[shown] != null) rows[shown].SetActive(true);
                shown++;
            }
            for (int i = shown; i < rows.Length; i++)
            {
                if (rows[i] != null) rows[i].SetActive(false);
                if (i < boundIds.Length) boundIds[i] = null;
            }
            panel.gameObject.SetActive(shown > 0);
            if (shown == 0) return;
            float height = shown * rowHeight;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, headerHeight + height);
            if (viewport != null) viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, height);
            if (contentRoot != null) contentRoot.sizeDelta = new Vector2(contentRoot.sizeDelta.x, height);
        }
    }
}
