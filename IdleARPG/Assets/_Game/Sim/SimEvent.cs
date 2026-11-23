namespace Lanternkeeper.Sim
{
    public enum SimEventType { Hit, Death, Cast, Resonance, ChainPop, RoomCleared, FloorCleared, HeroDied, LevelUp, Rested, ChestOpened, Drop, Equip, ZoneEntered, Shot, GrandResonance, Evolved, Mastery, Leap, Pull, Ground, EventOffered, EventResolved, CampUnlocked, RuneDrop, Runeword, Unlocked, Ignited, Telegraph, Slam, BossCast, Medal, Rebirth, Achievement, Letter, SkillLevelUp, WickLevelUp, AugmentOffered, AugmentChosen }

    /// <summary>Something that happened this tick. View reads these to spawn effects; Sim never reads them back.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int SourceId, TargetId;
        public Vec2 Pos;
        /// <summary>Where a chain hit came from (the pop's centre) or where a leap/pull started.</summary>
        public Vec2 From;
        public float Value;
        public Element Elem;
        /// <summary>Death: the element of the killing blow.</summary>
        public Element KillElem;
        public Resonance Res;
        public string SkillId;
        public bool FromChain;
        public int Depth;
        /// <summary>Display text for a callout (a boss technique's name).</summary>
        public string Text;
        /// <summary>For Drop / Equip events.</summary>
        public Item Item;
    }
}
