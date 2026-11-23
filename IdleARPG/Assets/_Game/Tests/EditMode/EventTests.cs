using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §6.2 event rooms (M2 Task 3).</summary>
    public class EventTests
    {
        static (SimWorld w, Room room) WithEvent(EventKind kind, int def = 0)
        {
            var w = new SimWorld(31) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            var c = w.Hero.Pos;
            var room = new Room { Id = 2, Kind = RoomKind.Event, Rect = new GridRect((int)c.X - 3, (int)c.Y - 3, 7, 7), Event = kind, EventDefault = def };
            w.Floor.Rooms.Insert(0, room); // first in the list, so RoomAt finds it before the arena-wide start room
            return (w, room);
        }

        [Test]
        public void Unanswered_event_takes_its_default_after_twenty_seconds()
        {
            var (w, _) = WithEvent(EventKind.Shrine, def: 1);
            w.Tick();
            Assert.That(w.PendingEvent, Is.Not.Null);
            for (int i = 0; i < SimWorld.EventDecisionTicks - 2; i++) w.Tick();
            Assert.That(w.ShrineBuff, Is.EqualTo(0), "not yet");
            for (int i = 0; i < 3; i++) w.Tick();
            Assert.That(w.PendingEvent, Is.Null);
            Assert.That(w.ShrineBuff, Is.EqualTo(2), "default option 1 = move speed");
        }

        [Test]
        public void Choosing_resolves_at_once_and_the_blessing_ends_with_the_floor()
        {
            var (w, _) = WithEvent(EventKind.Shrine, def: 2);
            w.Tick();
            Assert.That(w.ChooseEvent(0), Is.True);
            Assert.That(w.ShrineBuff, Is.EqualTo(1));
            Assert.That(w.ChooseEvent(0), Is.False, "nothing left to choose");
            w.GoToFloor(w.FloorIndex + 1);
            Assert.That(w.ShrineBuff, Is.EqualTo(0));
        }

        [Test]
        public void Opened_cursed_chest_pays_two_rare_items_once_its_guards_fall()
        {
            var (w, room) = WithEvent(EventKind.CursedChest);
            w.Tick();
            w.ChooseEvent(0);
            Assert.That(w.Enemies.Count(e => e.Alive && e.Elite), Is.EqualTo(3));
            bool paid = false;
            for (int i = 0; i < 10 && !paid; i++)
            {
                // Keep cutting them down: a 증식하는 guard splits into copies on the next tick.
                foreach (var e in w.Enemies.Where(e => e.Alive).ToList()) w.ApplyHit(e, 1e9f, Element.None);
                w.Tick();
                paid = w.Events.Any(ev => ev.Type == SimEventType.ChestOpened);
            }
            Assert.That(paid, Is.True);
            Assert.That(room.Rewarded, Is.True);
        }

        [Test]
        public void Merchant_sells_a_rare_for_ash_shards()
        {
            var (w, _) = WithEvent(EventKind.Merchant);
            w.Bag.AshShards = 40;
            w.Tick();
            w.ChooseEvent(0);
            Assert.That(w.Bag.AshShards, Is.EqualTo(40 - SimWorld.MerchantPrice));
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.Drop && ev.Item != null && ev.Item.Rarity >= Rarity.Rare), Is.True);
        }

        [Test]
        public void Merchant_without_shards_sells_nothing()
        {
            var (w, _) = WithEvent(EventKind.Merchant);
            w.Bag.AshShards = 5;
            w.Tick();
            w.ChooseEvent(0);
            Assert.That(w.Bag.AshShards, Is.EqualTo(5));
        }

        [Test]
        public void Oil_spring_is_collected_without_asking()
        {
            var (w, _) = WithEvent(EventKind.OilSpring);
            w.Tick();
            Assert.That(w.PendingEvent, Is.Null);
            Assert.That(w.Bag.LanternOil, Is.GreaterThanOrEqualTo(3));
        }

        // ---------- GDD §5.2 원정 지령 (M2 Task 4) ----------

        static (int ticks, int kills) FirstFloor(ExpeditionOrder order, int seed)
        {
            var w = new SimWorld(seed) { Order = order };
            for (int i = 1; i <= 12000; i++)
            {
                w.Tick();
                if (w.Events.Any(e => e.Type == SimEventType.FloorCleared)) return (i, w.Kills);
            }
            return (12000, w.Kills);
        }

        [Test]
        public void Boss_rush_clears_a_floor_sooner_and_with_fewer_kills_than_clear_all()
        {
            int faster = 0, fewer = 0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var all = FirstFloor(ExpeditionOrder.ClearAll, seed);
                var rush = FirstFloor(ExpeditionOrder.BossRush, seed);
                if (rush.ticks < all.ticks) faster++;
                if (rush.kills < all.kills) fewer++;
            }
            Assert.That(faster, Is.GreaterThanOrEqualTo(2));
            Assert.That(fewer, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Floor_lock_repeats_the_floor_after_its_boss()
        {
            var w = new SimWorld(1) { FloorLock = true };
            int floor = w.FloorIndex;
            bool cleared = false;
            for (int i = 0; i < 12000 && !cleared; i++)
            {
                w.Tick();
                cleared = w.Events.Any(e => e.Type == SimEventType.FloorCleared);
            }
            Assert.That(cleared, Is.True);
            Assert.That(w.FloorIndex, Is.EqualTo(floor));
        }

        [Test]
        public void Floors_have_event_rooms_and_they_survive_a_save()
        {
            int events = 0;
            for (int seed = 0; seed < 20; seed++)
                events += FloorGenerator.Generate(seed, 9).Rooms.Count(r => r.Kind == RoomKind.Event);
            Assert.That(events, Is.GreaterThan(10));

            var w = new SimWorld(8);
            w.Bag.LanternOil = 7;
            w.Order = ExpeditionOrder.TreasureHunt;
            w.FloorLock = true;
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Bag.LanternOil, Is.EqualTo(7));
            Assert.That(b.Order, Is.EqualTo(ExpeditionOrder.TreasureHunt));
            Assert.That(b.FloorLock, Is.True);
        }
    }
}
