using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.UI
{
    /// <summary>Scene-owned positions for code-built UI. Native prefab UI keeps its RectTransforms.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RuntimeUiLayoutSettings : MonoBehaviour
    {
        [Serializable]
        public sealed class Placement
        {
            public string label;
            [Tooltip("End of the UI hierarchy path, e.g. GameGuidePanel/SafeArea/Card")]
            public string pathSuffix;
            public Vector2 positionOffset;
            [NonSerialized] internal string[] parts;
        }
        [SerializeField] private Placement[] placements = Array.Empty<Placement>();
        private sealed class Binding { public RectTransform target; public Vector2 original; public Placement placement; }
        private static RuntimeUiLayoutSettings instance;
        private static readonly HashSet<RectTransform> pending = new HashSet<RectTransform>();
        private readonly Dictionary<RectTransform, Binding> bindings = new Dictionary<RectTransform, Binding>();
        private bool dirty;
        private readonly HashSet<RectTransform> scanned = new HashSet<RectTransform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; pending.Clear(); }
        private void OnEnable()
        {
            instance = this; dirty = true;
            if (!Application.isPlaying) return;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                    foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true)) Queue(rect);
        }
        private void OnDisable()
        {
            if (instance == this) { instance = null; pending.Clear(); }
            bindings.Clear();
        }
        private void OnValidate()
        {
            dirty = true;
            if (placements != null) foreach (var item in placements) if (item != null) item.parts = null;
        }
        public static void Queue(RectTransform target)
        {
            if (Application.isPlaying && instance != null && target != null) pending.Add(target);
        }
        private void LateUpdate()
        {
            if (!Application.isPlaying || instance != this) return;
            if (pending.Count > 0)
            {
                scanned.Clear();
                foreach (var rect in pending)
                    for (Transform t = rect; t != null; t = t.parent)
                        if (t is RectTransform candidate && scanned.Add(candidate) && !bindings.ContainsKey(candidate)) Bind(candidate);
                pending.Clear();
            }
            if (!dirty) return;
            dirty = false;
            foreach (var binding in bindings.Values)
                if (binding.target != null) binding.target.anchoredPosition = binding.original + binding.placement.positionOffset;
        }
        private void Bind(RectTransform target)
        {
            if (placements == null) return;
            foreach (var placement in placements)
            {
                if (placement == null || string.IsNullOrWhiteSpace(placement.pathSuffix) || !Matches(target, placement.parts ??= placement.pathSuffix.Split('/'))) continue;
                var binding = new Binding { target = target, original = target.anchoredPosition, placement = placement };
                bindings.Add(target, binding); target.anchoredPosition = binding.original + placement.positionOffset;
                break;
            }
        }
        private static bool Matches(Transform target, string[] names)
        {
            for (int i = names.Length - 1; i >= 0; i--)
            {
                if (target == null || target.name != names[i]) return false;
                target = target.parent;
            }
            return true;
        }
    }
}
