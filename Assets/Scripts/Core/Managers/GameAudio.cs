using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Core
{
    internal sealed class GameAudio : MonoBehaviour
    {
        private enum Music { None, Title, Town, Stage, Dungeon }

        private static GameAudio instance;
        private GameAudioCatalog catalog;
        private AudioSource music, effects;
        private Music current;
        private string stageId;
        private int lastBattle = -1;
        private readonly System.Random musicRandom = new System.Random();
        private readonly float[] readyAt = new float[(int)SoundCue.Count];
        private float nextCombatCue;
        private float combatWindowStart;
        private int combatCueCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            var library = Resources.Load<GameAudioCatalog>("Audio/GameAudioCatalog");
            if (library == null) return;

            var root = new GameObject("GameAudio");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameAudio>();
            instance.catalog = library;
            instance.music = root.AddComponent<AudioSource>();
            instance.music.playOnAwake = false;
            instance.music.loop = true;
            instance.music.spatialBlend = 0f;
            instance.music.volume = .55f;
            instance.effects = root.AddComponent<AudioSource>();
            instance.effects.playOnAwake = false;
            instance.effects.spatialBlend = 0f;
            instance.effects.volume = .7f;
            SceneManager.activeSceneChanged += instance.OnActiveSceneChanged;
            instance.OnActiveSceneChanged(default, SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            instance = null;
        }

        private void OnActiveSceneChanged(Scene previous, Scene currentScene)
        {
            string name = currentScene.name;
            if (string.Equals(name, "TitleScene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Title", StringComparison.OrdinalIgnoreCase))
                SetMusic(Music.Title, null);
            else
                SetMusic(Music.None, null);
        }

        internal static void EnterTown() => instance?.SetMusic(Music.Town, null);
        internal static void EnterStage(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                instance?.SetMusic(Music.Stage, id);
        }

        internal static void EnterDungeon() => instance?.SetMusic(Music.Dungeon, null);

        private void SetMusic(Music next, string id)
        {
            if (current == next && stageId == id) return;
            current = next;
            stageId = id;

            AudioClip clip = next == Music.Title ? catalog.Title : next == Music.Town ? catalog.Town : null;
            if (next == Music.Stage || next == Music.Dungeon)
            {
                int count = catalog.BattleCount;
                if (count > 0)
                {
                    int index = musicRandom.Next(lastBattle >= 0 && count > 1 ? count - 1 : count);
                    if (lastBattle >= 0 && count > 1 && index >= lastBattle) index++;
                    lastBattle = index;
                    clip = catalog.Battle(index);
                }
            }

            music.Stop();
            music.clip = clip;
            if (clip != null) music.Play();
        }

        internal static void Play(SoundCue cue)
        {
            if (instance != null) instance.PlayCue(cue);
        }

        private void PlayCue(SoundCue cue)
        {
            AudioClip clip = catalog.Effect(cue);
            if (clip == null) return;

            float now = Time.unscaledTime;
            int index = (int)cue;
            if (now < readyAt[index]) return;
            bool combat = cue is SoundCue.Sword or SoundCue.Magic or SoundCue.Hit or
                SoundCue.Critical or SoundCue.AttackSkill or SoundCue.Heal or SoundCue.Buff or SoundCue.Debuff;
            if (combat)
            {
                if (now >= combatWindowStart + 1f)
                {
                    combatWindowStart = now;
                    combatCueCount = 0;
                }
                bool critical = cue == SoundCue.Critical;
                if (combatCueCount >= (critical ? 14 : 12) || (!critical && now < nextCombatCue))
                    return;
                combatCueCount++;
                nextCombatCue = now + .045f;
            }

            readyAt[index] = now + (cue switch
            {
                SoundCue.Material => .25f,
                SoundCue.RareReward or SoundCue.LevelUp => .4f,
                SoundCue.Hit or SoundCue.Sword or SoundCue.Magic or SoundCue.Critical => .12f,
                _ => .18f
            });
            effects.PlayOneShot(clip, cue switch
            {
                SoundCue.Hit => .28f,
                SoundCue.Sword or SoundCue.Magic or SoundCue.Material => .45f,
                SoundCue.Confirm or SoundCue.Back => .55f,
                _ => .85f
            });
        }
    }
}
