using MixMaster.Core;
using UnityEngine;

namespace MixMaster.Combat
{
    /// <summary>Hides rendering only; projectile scripts, timing and hits keep running.</summary>
    [DisallowMultipleComponent]
    public sealed class SkillEffectVisibility : MonoBehaviour
    {
        private Renderer[] renderers;
        private bool[] originalRenderingOff;
        private DisplaySettingsManager settings;

        public static void Track(GameObject effect)
        {
            if (effect == null) return;
            var visibility = effect.GetComponent<SkillEffectVisibility>();
            if (visibility == null)
            {
                visibility = effect.AddComponent<SkillEffectVisibility>();
                // OnEnable has already cached an active, newly added effect.
                if (visibility.renderers != null) return;
            }
            visibility.CacheRenderers();
            visibility.Bind();
            visibility.Refresh();
        }

        private void OnEnable()
        {
            CacheRenderers(); Bind(); Refresh();
        }

        private void OnDisable()
        {
            if (settings != null) settings.Changed -= Refresh;
            settings = null;
            RestoreRendering();
        }

        private void OnDestroy() => OnDisable();

        private void Bind()
        {
            if (settings == DisplaySettingsManager.Instance) return;
            if (settings != null) settings.Changed -= Refresh;
            settings = DisplaySettingsManager.Instance;
            if (settings != null) settings.Changed += Refresh;
        }

        private void CacheRenderers()
        {
            var found = GetComponentsInChildren<Renderer>(true);
            var originals = new bool[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                int previous = renderers != null ? System.Array.IndexOf(renderers, found[i]) : -1;
                originals[i] = previous >= 0 ? originalRenderingOff[previous] : found[i].forceRenderingOff;
            }
            renderers = found; originalRenderingOff = originals;
        }

        private void Refresh()
        {
            if (renderers == null) return;
            bool hidden = DisplaySettingsManager.IsDisabled(DisplayOption.SkillEffects);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].forceRenderingOff = hidden || originalRenderingOff[i];
        }

        private void RestoreRendering()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].forceRenderingOff = originalRenderingOff[i];
        }
    }
}
