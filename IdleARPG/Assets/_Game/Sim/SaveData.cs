using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// Everything needed to resume a delve (GDD §19 Save/). Plain public fields so JsonUtility can write it;
    /// missing fields in an older file keep these defaults, and <see cref="Version"/> drives migrations.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int Seed;
        public long SavedAtUnix;

        public string ClassId = "ashknight";
        public int Level = 1, Xp;
        /// <summary>GDD §11.5 (old saves: 0 / 0 / 0 / −1).</summary>
        public int EmberLevel, Rebirths, DeepestActCleared = -1;
        public long OverflowXp;
        /// <summary>심지 레벨 and its XP (old saves: 0).</summary>
        public int WickLevel;
        public long WickXp;
        public float HpFraction = 1f;
        public int FloorIndex, Deaths, Kills, NextItemUid = 1, AshShards, LanternOil;
        public bool FirstLegendaryGiven;
        public int Filter = (int)FilterPreset.Normal;
        /// <summary>자동 분해: drops of this rarity or lower go to ash as they fall (−1 off); see <see cref="SimWorld.AutoSalvage"/>.</summary>
        public int AutoSalvage = (int)Rarity.Common;
        /// <summary>환생 증강 카드: chosen cards, the waiting deal (or null), its re-deal spent, deals this life, branch skill sets.</summary>
        public List<string> Augments = new List<string>();
        public List<string> AugOffer;
        public bool AugRerolled;
        public int AugDeals;
        public List<string> BranchSets = new List<string>();
        public bool AugSwitchSets = true;
        /// <summary>직접 filter rules, three ints each: condition, value, action.</summary>
        public List<int> CustomRules = new List<int>();
        /// <summary>True once a save has written <see cref="CustomRules"/> (an empty list then means "no rules").</summary>
        public bool CustomRulesSet;
        public bool AutoEquip = true;

        public List<EquippedEntry> Equipped = new List<EquippedEntry>();
        public List<Item> Bag = new List<Item>();
        public List<int> RecentDropUids = new List<int>();

        public string LootRng = "", CombatRng = "";

        /// <summary>Rolling efficiency for offline progress (GDD §13).</summary>
        public float KillsPerMinute, XpPerMinute, DropsPerMinute;

        // Skill system (docs/specs/2026-11-19-skill-rune-synergy-design.md): ring preset, gauge, uses, evolutions, mastery.
        public int Preset;
        public int TalentFocus = -1;
        public List<int> RuneCounts = new List<int>();
        public List<string> RunewordsFound = new List<string>();
        /// <summary>적 도감: parallel lists (JsonUtility has no dictionaries).</summary>
        public List<string> KillIds = new List<string>();
        public List<int> KillCounts = new List<int>();
        public List<string> Achieved = new List<string>();
        public List<string> Letters = new List<string>(), LettersRead = new List<string>();
        public int GrandCount, MaxChainDepth, IgniteCount, LanternColor;
        public bool UseCustomRing;
        /// <summary>Skill set the save was made with; below <see cref="SimWorld.SkillSetVersion"/> the hero starts again on its
        /// class's signature build (휠윈드 · 속사 · 축복의 망치 · 원소, 11-21).</summary>
        public int SkillSet;
        public List<string> CustomSkills = new List<string>();
        public List<int> CustomSigils = new List<int>();
        public int Order;
        public bool FloorLock;
        public bool CodexFirst, TopLoop;
        /// <summary>자동 점화 option (old saves: off).</summary>
        public bool AutoIgnite;
        public int Stance;
        public bool CampUnlocked;
        public int OilStoreLevel, CartographerLevel;
        public float Gauge;
        public List<string> SkillUseIds = new List<string>();
        public List<int> SkillUseCounts = new List<int>();
        public List<string> Evolved = new List<string>();
        /// <summary>기억의 나무 (11-21). Old saves read 0: their evolutions become legacy and the tree grows by 자동 분배.</summary>
        public int TreeVersion;
        public List<string> TreeNodes = new List<string>();
        public bool TreeAuto = true;
        public List<string> LegacyEvolved = new List<string>();
        public List<int> ResonanceCounts = new List<int>();
    }

    [Serializable]
    public sealed class EquippedEntry
    {
        public int Spot;
        public Item Item;
    }
}
