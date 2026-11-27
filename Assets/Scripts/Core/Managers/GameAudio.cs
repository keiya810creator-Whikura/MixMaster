using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Core
{
    internal sealed class GameAudio : MonoBehaviour
    {
        private enum Music { None, Title, Town, Stage, Dungeon }

        private static GameAudio instance;
        private const string MusicVolumeKey = "MixMaster.Audio.BgmVolume";
        private const string EffectVolumeKey = "MixMaster.Audio.SeVolume";
        private static bool volumeSavePending;

        internal static int MusicVolume => Mathf.Clamp(PlayerPrefs.GetInt(MusicVolumeKey, 55), 0, 100);
        internal static int EffectVolume => Mathf.Clamp(PlayerPrefs.GetInt(EffectVolumeKey, 70), 0, 100);

        internal static void SetMusicVolume(int value) =>
            SetVolume(MusicVolumeKey, value, instance != null ? instance.music : null);

        internal static void SetEffectVolume(int value) =>
            SetVolume(EffectVolumeKey, value, instance != null ? instance.effects : null);

        private static void SetVolume(string key, int value, AudioSource source)
        {
            value = Mathf.Clamp(value, 0, 100);
            PlayerPrefs.SetInt(key, value);
            if (source != null) source.volume = value / 100f;
            volumeSavePending = true;
        }

        internal static void SaveVolume()
        {
            if (!volumeSavePending) return;
            PlayerPrefs.Save();
            volumeSavePending = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveVolume();
        }

        private void OnApplicationQuit() => SaveVolume();
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
            instance.music.volume = MusicVolume / 100f;
            instance.effects = root.AddComponent<AudioSource>();
            instance.effects.playOnAwake = false;
            instance.effects.spatialBlend = 0f;
            instance.effects.volume = EffectVolume / 100f;
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
