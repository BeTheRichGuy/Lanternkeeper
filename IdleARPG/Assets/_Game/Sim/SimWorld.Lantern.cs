namespace Lanternkeeper.Sim
{
    /// <summary>
    /// GDD §5.3 등불 점화: a small reward for a player who is watching. Click the lantern (or Space): for 30 s the light
    /// reaches farther and the hero hits 15% harder (the first kindling on a floor also finds a hidden cache), then 3 min to rekindle. Missing it costs nothing; offline never uses it.
    /// </summary>
    public sealed partial class SimWorld
    {
        public const float IgniteSeconds = 30f, AutoIgniteSeconds = 20f, IgniteCooldown = 180f, IgniteDamage = 0.15f;
        bool _igniteByHand;
        bool _igniteRequested;
        /// <summary>Floor whose hidden cache the lantern has already found (one per floor).</summary>
        int _cacheFloor = -1;

        public bool Ignited => Time < Hero.IgniteUntil;
        public float IgniteLeft => (float)System.Math.Max(0.0, Hero.IgniteUntil - Time);
        public float IgniteCooldownLeft => (float)System.Math.Max(0.0, Hero.IgniteReadyAt - Time);

        /// <summary>Asks for a kindling. Input arrives between ticks, so it lands on the next one. False while recharging.</summary>
        public bool Ignite()
        {
            if (IgniteCooldownLeft > 0f) return false;
            _igniteRequested = true;
            _igniteByHand = true;
            return true;
        }

        void TickIgnite()
        {
            if (!_igniteRequested && AutoIgnite && Time >= Hero.IgniteReadyAt && Hero.Alive && WorthKindling())
            {
                _igniteRequested = true;
                _igniteByHand = false;
            }
            if (!_igniteRequested) return;
            _igniteRequested = false;
            bool byHand = _igniteByHand;
            _igniteByHand = false;
            if (Time < Hero.IgniteReadyAt || !Hero.Alive) return;
            Hero.IgniteUntil = Time + (byHand ? IgniteSeconds : AutoIgniteSeconds);
            Hero.IgniteReadyAt = Time + IgniteCooldown;
            Events.Add(new SimEvent { Type = SimEventType.Ignited, SourceId = Hero.Id, Pos = Hero.Pos });
            // "숨은 방 탐지": the first kindling on a floor lights up a cache someone hid nearby (two items, rare or better).
            if (_cacheFloor != FloorIndex)
            {
                _cacheFloor = FloorIndex;
                Events.Add(new SimEvent { Type = SimEventType.ChestOpened, Pos = Hero.Pos, SkillId = "hidden_cache" });
                for (int i = 0; i < 2; i++) DropItem(Hero.Pos, Rarity.Rare);
            }
        }

        /// <summary>An elite or boss within 6 cells, or eight enemies within 5: a fight worth the lantern.</summary>
        bool WorthKindling()
        {
            int near = 0;
            foreach (var e in _enemies)
            {
                if (!e.Alive) continue;
                float d2 = Vec2.DistSq(e.Pos, Hero.Pos);
                if ((e.Boss || e.Elite) && d2 <= 36f) return true;
                if (d2 <= 25f && ++near >= 8) return true;
            }
            return false;
        }
    }
}
