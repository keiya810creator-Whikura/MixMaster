using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.UI
{
    /// <summary>Short, pooled 2D impact bursts, tinted by the damage popup.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageHitParticles : MonoBehaviour
    {
        private const int MaxActive = 32;
        private const int SparkCount = 10;
        private static readonly Stack<DamageHitParticles> pooled = new Stack<DamageHitParticles>();
        private static int activeCount;
        private static Material sharedMaterial;

        private ParticleSystem particles;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pooled.Clear();
            activeCount = 0;
            sharedMaterial = null;
        }

        public static void Show(Vector3 worldPosition, Color damageColor)
        {
            if (!Application.isPlaying || activeCount >= MaxActive) return;
            if (sharedMaterial == null)
                sharedMaterial = Resources.Load<Material>("Effects/DamageHitParticle");
            if (sharedMaterial == null) return;

            DamageHitParticles effect = null;
            // Ignore entries destroyed during a scene change.
            while (pooled.Count > 0 && effect == null)
                effect = pooled.Pop();
            if (effect == null)
            {
                GameObject go = new GameObject("_DamageHitParticles");
                effect = go.AddComponent<DamageHitParticles>();
            }

            effect.gameObject.SetActive(true);
            effect.Begin(worldPosition, damageColor);
        }

        private void Awake()
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.maxParticles = SparkCount + 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            colors.enabled = true;
            colors.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystem.SizeOverLifetimeModule sizes = particles.sizeOverLifetime;
            sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 2999;
        }

        private void Begin(Vector3 worldPosition, Color color)
        {
            // Stay independent of the victim so a killing blow finishes its burst.
            transform.position = worldPosition + new Vector3(0f, 0.35f, -0.18f);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play();

            float rotation = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = rotation + i * Mathf.PI * 2f / SparkCount;
                float speed = Random.Range(0.8f, 2.1f);
                var spark = new ParticleSystem.EmitParams
                {
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startColor = color
                };
                particles.Emit(spark, 1);
            }

            var flash = new ParticleSystem.EmitParams
            {
                velocity = Vector3.zero,
                startColor = color,
                startSize = 0.28f,
                startLifetime = 0.12f
            };
            particles.Emit(flash, 1);
            showing = true;
            activeCount++;
        }

        private void Update()
        {
            if (showing && !particles.IsAlive(true))
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!showing) return;
            showing = false;
            activeCount = Mathf.Max(0, activeCount - 1);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (pooled.Count < MaxActive)
                pooled.Push(this);
            else if (Application.isPlaying)
                Destroy(gameObject);
        }
    }
}
