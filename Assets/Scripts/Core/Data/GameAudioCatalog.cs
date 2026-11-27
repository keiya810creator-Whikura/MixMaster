using UnityEngine;

namespace MixMaster.Core
{
    internal enum SoundCue
    {
        Confirm, Back, Error, Sword, Magic, Hit, Critical, AttackSkill, Heal,
        Buff, Debuff, Material, RareReward, LevelUp, SkillGrowth, Teleport,
        Danger, Clear, BreedSuccess, BreedFailure, Reincarnation, Defeat, Count
    }

    public sealed class GameAudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioClip title;
        [SerializeField] private AudioClip town;
        [SerializeField] private AudioClip[] battle;
        [SerializeField] private AudioClip[] effects;

        internal AudioClip Title => title;
        internal AudioClip Town => town;
        internal AudioClip Battle(int index) => battle[index];
        internal int BattleCount => battle?.Length ?? 0;
        internal AudioClip Effect(SoundCue cue) =>
            effects != null && (uint)cue < (uint)effects.Length ? effects[(int)cue] : null;
    }
}
