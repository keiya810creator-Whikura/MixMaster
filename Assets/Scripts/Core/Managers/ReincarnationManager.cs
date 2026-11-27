using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class ReincarnationManager : MonoBehaviour
    {
        private readonly List<ReincarnationBookRecord> books = new List<ReincarnationBookRecord>();
        private MonsterManager monsters;
        private PlayerManager player;
        private bool subscribed;
        private float retryAt;
        private bool reincarnating;

        public IReadOnlyList<ReincarnationBookRecord> Books => books;
        public event Action BooksChanged;
        public event Action<ReincarnationBookRecord> BookUnlocked;
        public event Action Reincarnated;

        private void Awake() => EnsureHumanBook();
        private void OnEnable() => EnsureBindings();
        private void Start()
        {
            EnsureBindings();
            UnlockFromOwnedMonsters(false);
        }
        private void Update()
        {
            if (subscribed || Time.unscaledTime < retryAt) return;
            retryAt = Time.unscaledTime + 0.5f;
            EnsureBindings();
            UnlockFromOwnedMonsters(true);
        }
        private void OnDisable()
        {
            if (monsters != null && subscribed)
                monsters.MonsterIntimacyChanged -= HandleIntimacyChanged;
            subscribed = false;
        }
        private void EnsureBindings()
        {
            if (player == null) player = GetComponent<PlayerManager>();
            if (player == null) player = FindFirstObjectByType<PlayerManager>();
            if (monsters == null) monsters = GetComponent<MonsterManager>();
            if (monsters == null) monsters = FindFirstObjectByType<MonsterManager>();
            if (monsters != null && !subscribed)
            {
                monsters.MonsterIntimacyChanged += HandleIntimacyChanged;
                subscribed = true;
            }
        }

        private void EnsureHumanBook()
        {
            if (FindBook(ReincarnationRules.HumanBookId) == null)
                books.Insert(0, new ReincarnationBookRecord { bookId = ReincarnationRules.HumanBookId });
        }
        public ReincarnationBookRecord FindBook(string id) =>
            books.Find(b => b != null && string.Equals(b.bookId, id, StringComparison.Ordinal));
        public MonsterSO GetSpecies(string id)
        {
            if (id == ReincarnationRules.HumanBookId) return null;
            MonsterCatalogSO catalog = MonsterCatalogSO.Load();
            return catalog != null ? catalog.GetMonster(id) : null;
        }
        public string GetBookName(string id)
        {
            if (id == ReincarnationRules.HumanBookId) return "人間の書";
            MonsterSO species = GetSpecies(id);
            return (species != null ? species.displayName : id) + "の書";
        }
        public CharacterStats GetBookBaseStats(string id)
        {
            EnsureBindings();
            MonsterSO species = GetSpecies(id);
            return id == ReincarnationRules.HumanBookId
                ? (player != null ? player.HumanBaseStats : new CharacterStats())
                : (species != null ? ReincarnationRules.CopyStats(species.baseStats) : null);
        }

        private void HandleIntimacyChanged(OwnedMonsterRecord record)
        {
            if (TryUnlock(record)) SaveProgress();
        }
        private bool TryUnlock(OwnedMonsterRecord record)
        {
            if (record == null || MonsterIntimacyBalance.GetLevelFromTotalKills(record.intimacyKills) <
                ReincarnationRules.RequiredIntimacyLevel || string.IsNullOrWhiteSpace(record.monsterId) ||
                GetSpecies(record.monsterId) == null || FindBook(record.monsterId) != null)
                return false;
            var book = new ReincarnationBookRecord { bookId = record.monsterId };
            books.Add(book);
            BooksChanged?.Invoke();
            BookUnlocked?.Invoke(book);
            return true;
        }
        private void UnlockFromOwnedMonsters(bool save)
        {
            if (monsters == null) return;
            bool changed = false;
            foreach (OwnedMonsterRecord record in monsters.OwnedMonsters)
                changed |= TryUnlock(record);
            if (changed && save) SaveProgress();
        }

        public void RestoreBooks(IReadOnlyList<ReincarnationBookRecord> saved)
        {
            EnsureBindings();
            books.Clear();
            if (saved != null)
            {
                foreach (ReincarnationBookRecord source in saved)
                {
                    if (source == null || string.IsNullOrWhiteSpace(source.bookId)) continue;
                    ReincarnationBookRecord duplicate = FindBook(source.bookId);
                    if (duplicate != null) duplicate.isFavorite |= source.isFavorite;
                    else books.Add(new ReincarnationBookRecord { bookId = source.bookId, isFavorite = source.isFavorite });
                }
            }
            EnsureHumanBook();
            // Backfill books from old saves, without generating a save during load.
            UnlockFromOwnedMonsters(false);
            BooksChanged?.Invoke();
        }

        public bool ToggleFavorite(string id)
        {
            ReincarnationBookRecord book = FindBook(id);
            if (book == null) return false;
            book.isFavorite = !book.isFavorite;
            BooksChanged?.Invoke();
            SaveProgress();
            return true;
        }

        public List<ReincarnationBookRecord> GetSortedBooks(ReincarnationBookSort sort,
            bool descending, bool favoritesOnly)
        {
            var result = books.FindAll(b => b != null && (!favoritesOnly || b.isFavorite));
            if (sort == ReincarnationBookSort.Acquisition)
            {
                if (descending) result.Reverse();
                return result;
            }
            var scores = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (sort != ReincarnationBookSort.Name)
                foreach (var book in result)
                    scores[book.bookId] = SortValue(book, sort);
            result.Sort((a, b) =>
            {
                int comparison = sort == ReincarnationBookSort.Name
                    ? string.Compare(GetBookName(a.bookId), GetBookName(b.bookId), StringComparison.Ordinal)
                    : scores[a.bookId].CompareTo(scores[b.bookId]);
                if (descending) comparison = -comparison;
                return comparison != 0 ? comparison : string.Compare(a.bookId, b.bookId, StringComparison.Ordinal);
            });
            return result;
        }
        private decimal SortValue(ReincarnationBookRecord book, ReincarnationBookSort sort)
        {
            if (sort == ReincarnationBookSort.Favorite) return book.isFavorite ? 1 : 0;
            MonsterSO species = GetSpecies(book.bookId);
            CharacterStats stats = GetBookBaseStats(book.bookId);
            if (stats == null) return -1;
            switch (sort)
            {
                case ReincarnationBookSort.Hp: return stats.maxHp;
                case ReincarnationBookSort.Mp: return stats.maxMp;
                case ReincarnationBookSort.Attack: return stats.attack;
                case ReincarnationBookSort.Magic: return stats.magic;
                case ReincarnationBookSort.Defense: return stats.defense;
                case ReincarnationBookSort.MagicDefense: return stats.magicDefense;
                case ReincarnationBookSort.CriticalRate: return FiniteSortValue(stats.criticalRate);
                case ReincarnationBookSort.CriticalMultiplier: return FiniteSortValue(stats.criticalMultiplier);
                case ReincarnationBookSort.MoveSpeed: return FiniteSortValue(stats.moveSpeed);
                case ReincarnationBookSort.AttackSpeed: return FiniteSortValue(stats.attackSpeed);
                case ReincarnationBookSort.AttackRange: return FiniteSortValue(stats.attackRange);
                case ReincarnationBookSort.Element: return (int)stats.element;
                case ReincarnationBookSort.AttackType: return species != null ? (int)species.attackType : 0;
                case ReincarnationBookSort.Stage: return species != null ? species.appearanceStage : 0;
                case ReincarnationBookSort.BreedingValue: return species != null ? species.breedingValue : 0;
                default: return 0;
            }
        }

        private static decimal FiniteSortValue(float value) =>
            float.IsNaN(value) ? 0 : (decimal)Math.Max(-1e20f, Math.Min(1e20f, value));

        public bool TryReincarnate(string bookId, out string error)
        {
            EnsureBindings();
            error = string.Empty;
            if (reincarnating) { error = "転生処理中です。"; return false; }
            if (player == null) { error = "プレイヤーが見つかりません。"; return false; }
            if (FindBook(bookId) == null) { error = "この転生の書は未取得です。"; return false; }
            MonsterSO species = GetSpecies(bookId);
            if (bookId != ReincarnationRules.HumanBookId && (species == null || species.baseStats == null))
            { error = "転生先のモンスターデータが見つかりません。"; return false; }
            reincarnating = true;
            try
            {
                player.Reincarnate(species);
                GameAudio.Play(SoundCue.Reincarnation);
                Reincarnated?.Invoke();
                SaveProgress();
                return true;
            }
            finally { reincarnating = false; }
        }
        private void SaveProgress()
        {
            SaveManager save = GetComponent<SaveManager>();
            if (save == null) save = FindFirstObjectByType<SaveManager>();
            if (save != null) save.SaveGame();
        }
    }
}
