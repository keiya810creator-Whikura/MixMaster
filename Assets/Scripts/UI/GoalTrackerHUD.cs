using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private const float DismissWidth = 40f;
        private readonly Button[] dismissButtons = new Button[GameJournalManager.TrackedGoalLimit];
        private readonly Vector4[] countMargins = new Vector4[GameJournalManager.TrackedGoalLimit];
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
        private void SetDismissVisible(int row, bool visible)
        {
            if (visible && dismissButtons[row] == null && rows[row] != null)
            {
                Button button = InventoryUiFactory.Button(rows[row].transform,
                    "RemoveCompletedGoal", "×", Vector2.zero, new Vector2(DismissWidth, 36f));
                RectTransform rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                rect.pivot = new Vector2(1f, .5f);
                rect.anchoredPosition = new Vector2(-4f, 0f);
                if (counts[row] != null)
                {
                    countMargins[row] = counts[row].margin;
                    button.GetComponentInChildren<TMP_Text>().font = counts[row].font;
                }
                button.onClick.AddListener(() => RemoveCompletedGoal(row));
                dismissButtons[row] = button;
            }

            Button dismiss = dismissButtons[row];
            if (dismiss == null) return;

            dismiss.gameObject.SetActive(visible);
            if (counts[row] != null)
            {
                Vector4 margin = countMargins[row];
                if (visible) margin.z += DismissWidth + 4f;
                counts[row].margin = margin;
            }
        }

        private void RemoveCompletedGoal(int row)
        {
            string goalId = boundIds[row];
            if (journal == null || journal != GameJournalManager.Instance || string.IsNullOrEmpty(goalId))
                return;

            foreach (GoalRecord goal in journal.Goals)
            {
                if (goal.goalId != goalId || !goal.showOnHud) continue;
                // The displayed count can be up to one refresh interval old.
                if (GameGuideContent.Owned(goal, monsters, equipment, materials) >= goal.targetCount &&
                    journal.DeleteGoal(goalId))
                    Refresh();
                break;
            }
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
                    bool completed = owned >= goal.targetCount;
                    SetDismissVisible(shown, completed);
                    if (counts[shown] != null)
                    {
                        counts[shown].text = GameGuideContent.Number(owned) + " / " + GameGuideContent.Number(goal.targetCount);
                        counts[shown].color = completed ? new Color(.45f, 1f, .65f, 1) : Color.white;
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
