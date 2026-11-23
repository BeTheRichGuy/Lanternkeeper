using System;

namespace Lanternkeeper.Sim
{
    /// <summary>GDD §5.2 원정 지령: how the hero picks the next room.</summary>
    public enum ExpeditionOrder { ClearAll, BossRush, TreasureHunt, XpFirst }

    /// <summary>
    /// GDD §6.2 event rooms (M2 Task 3). Walking in offers a choice; nobody has to answer: after 20 s the default is
    /// taken, and the hero never stops exploring meanwhile (the idle flow is never blocked).
    /// </summary>
    public sealed partial class SimWorld
    {
        public const int EventDecisionTicks = 200;   // 20 s
        public const int MerchantPrice = 30;         // ash shards for one rare item

        public sealed class OfferedEvent
        {
            public Room Room;
            public EventKind Kind;
            public int DeadlineTick, Default;
            public string Title;
            public string[] Options;
        }

        /// <summary>The event waiting for a choice, or null.</summary>
        public OfferedEvent PendingEvent { get; private set; }

        /// <summary>Shrine blessing until the floor ends: 0 none, 1 damage +25%, 2 move +25%, 3 regen +1%/s.</summary>
        public int ShrineBuff { get; private set; }

        public static string EventTitle(EventKind k)
        {
            switch (k)
            {
                case EventKind.Shrine: return "잊힌 사당";
                case EventKind.CursedChest: return "저주받은 궤짝";
                case EventKind.Champion: return "떠도는 용사";
                case EventKind.Merchant: return "갈림길의 행상인";
                case EventKind.OilSpring: return "등불 기름 샘";
                default: return "";
            }
        }

        void ResetEvents()
        {
            PendingEvent = null;
            ShrineBuff = 0;
        }

        /// <summary>Roll what an event room holds (floor stream, so a floor always rebuilds the same).</summary>
        void SetUpEventRoom(Room room, float hpMul, float dmgMul)
        {
            room.Event = (EventKind)_rng.Range(1, 6);
            switch (room.Event)
            {
                case EventKind.Shrine: room.EventDefault = _rng.Range(0, 3); break;   // a random blessing
                case EventKind.CursedChest: room.EventDefault = 0; break;           // open (no cautious stance yet)
                case EventKind.Merchant: room.EventDefault = 1; break;              // walk past
                case EventKind.Champion:
                {
                    var c = Spawn(ActNow.Elite, room, hpMul * 5f, dmgMul * 1.5f);
                    c.Pos = c.PrevPos = CenterOf(room);
                    c.Champion = true;
                    c.Elite = true;
                    RollAffixes(c);
                    for (int i = 0; i < 6; i++) Spawn(ActNow.Ranks[ActNow.PatternFor(Zone, FloorInZone)[0]], room, hpMul * PackHp, dmgMul * PackDmg);
                    room.EventResolved = true; // nothing to choose: it fights you
                    break;
                }
            }
        }

        void OfferEvent(Room room)
        {
            if (room.Event == EventKind.None || room.EventResolved) return;
            string[] options;
            switch (room.Event)
            {
                case EventKind.Shrine: options = new[] { "피해 +25%", "이동 속도 +25%", "초당 생명력 회복 +1%" }; break;
                case EventKind.CursedChest: options = new[] { "연다 (정예 무리 → 희귀 이상 2개)", "지나친다" }; break;
                case EventKind.Merchant: options = new[] { $"잿 파편 {MerchantPrice}개로 희귀 장비 1개", "지나친다" }; break;
                default: options = null; break;
            }
            Events.Add(new SimEvent { Type = SimEventType.EventOffered, TargetId = room.Id, Pos = ChestPos(Floor, room), Value = (int)room.Event });
            if (options == null) { Resolve(room, 0); return; } // the oil spring is simply collected
            PendingEvent = new OfferedEvent
            {
                Room = room, Kind = room.Event, DeadlineTick = TickCount + EventDecisionTicks, Default = room.EventDefault,
                Title = EventTitle(room.Event), Options = options,
            };
        }

        /// <summary>Player choice (1/2/3 keys). Ignored when nothing is offered or the index is out of range.</summary>
        public bool ChooseEvent(int option)
        {
            var p = PendingEvent;
            if (p == null || option < 0 || option >= p.Options.Length) return false;
            PendingEvent = null;
            Resolve(p.Room, option);
            return true;
        }

        void TickEvents()
        {
            var p = PendingEvent;
            if (p != null && TickCount >= p.DeadlineTick)
            {
                PendingEvent = null;
                Resolve(p.Room, p.Default);
            }
        }

        void Resolve(Room room, int option)
        {
            room.EventResolved = true;
            var at = ChestPos(Floor, room);
            switch (room.Event)
            {
                case EventKind.Shrine:
                    ShrineBuff = option + 1;
                    break;
                case EventKind.CursedChest when option == 0:
                {
                    // The curse wakes a pack of elites; clearing them pays two rare-or-better items.
                    room.CursedOpen = true;
                    room.Cleared = false;
                    var (hpMul, dmgMul) = FloorScaling();
                    for (int i = 0; i < 3; i++)
                    {
                        var e = Spawn(ActNow.Elite, room, hpMul * 2.5f, dmgMul * 1.2f);
                        e.Elite = true;
                        e.Aggro = true;
                        RollAffixes(e);
                    }
                    var ranks = ActNow.PatternFor(Zone, FloorInZone);
                    for (int i = 0; i < 6; i++) Spawn(ActNow.Ranks[ranks[_rng.Range(0, ranks.Length)]], room, hpMul * PackHp, dmgMul * PackDmg).Aggro = true;
                    break;
                }
                case EventKind.Merchant when option == 0 && Bag.AshShards >= MerchantPrice:
                    Bag.AshShards -= MerchantPrice;
                    DropItem(at, Rarity.Rare);
                    break;
                case EventKind.OilSpring:
                    Bag.LanternOil += 3 + Math.Min(Zone, 3);
                    break;
            }
            Events.Add(new SimEvent { Type = SimEventType.EventResolved, TargetId = room.Id, Pos = at, Value = (int)room.Event, Depth = option });
        }

        /// <summary>Called when a room is cleared: a broken curse pays out.</summary>
        void OnRoomCleared(Room room)
        {
            if (room.Kind != RoomKind.Event || !room.CursedOpen || room.Rewarded) return;
            room.Rewarded = true;
            var at = ChestPos(Floor, room);
            Events.Add(new SimEvent { Type = SimEventType.ChestOpened, TargetId = room.Id, Pos = at });
            for (int i = 0; i < 2; i++) DropItem(at, Rarity.Rare);
        }

        float ShrineDamagePct => ShrineBuff == 1 ? 0.25f : 0f;
        float ShrineMoveMul => ShrineBuff == 2 ? 1.25f : 1f;
        float ShrineRegen => ShrineBuff == 3 ? 0.01f : 0f;
    }
}
