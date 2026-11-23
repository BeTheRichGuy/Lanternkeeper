using System.Collections.Generic;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>Movement smoothness (2026-11-19 stutter investigation).</summary>
    public class MovementTests
    {
        /// <summary>360° movement (희승, 2026-11-19): across an open room the path is one straight line, not a grid staircase.</summary>
        [Test]
        public void Open_room_path_is_a_single_straight_segment_at_any_angle()
        {
            var f = new Floor(30, 30);
            f.Carve(new GridRect(2, 2, 25, 25));
            var path = Pathfinder.Find(f, new Vec2(4.5f, 4.5f), new Vec2(20.5f, 11.5f)); // ≈24° off the grid axis
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path[0], Is.EqualTo(new Vec2(20.5f, 11.5f)));
        }

        [Test]
        public void Smoothed_path_never_cuts_through_a_wall()
        {
            var f = new Floor(30, 30);
            f.Carve(new GridRect(2, 2, 10, 10));   // room A
            f.Carve(new GridRect(16, 2, 10, 10));  // room B
            f.Carve(new GridRect(11, 9, 6, 2));    // low doorway between them
            var start = new Vec2(4.5f, 3.5f);
            var path = Pathfinder.Find(f, start, new Vec2(24.5f, 3.5f));
            Assert.That(path.Count, Is.GreaterThan(1), "must bend through the doorway");
            var a = start;
            foreach (var p in path) { Assert.That(Pathfinder.ClearLine(f, a, p), Is.True, $"{a} → {p}"); a = p; }
        }

        /// <summary>
        /// 동선 (희승 11-22: "빈 방들 굳이굳이 들어가서 동선낭비"): a fought-out room on a side branch counts as cleared
        /// without a visit, and the hero walks straight for the room that still has a monster.
        /// </summary>
        [Test]
        public void Hero_skips_an_empty_side_room_and_goes_for_the_pack()
        {
            var f = new Floor(40, 30);
            var a = new GridRect(2, 2, 8, 8);     // start
            var c = new GridRect(24, 2, 8, 8);    // the pack
            var b = new GridRect(2, 18, 8, 8);    // empty side room
            var boss = new GridRect(34, 3, 4, 4);
            f.Carve(a); f.Carve(c); f.Carve(b); f.Carve(boss);
            f.Carve(new GridRect(10, 5, 14, 2));  // A–C
            f.Carve(new GridRect(5, 10, 2, 8));   // A–B
            f.Carve(new GridRect(32, 4, 2, 2));   // C–boss
            f.Rooms.Add(new Room { Id = 0, Kind = RoomKind.Start, Rect = a });
            f.Rooms.Add(new Room { Id = 1, Kind = RoomKind.Combat, Rect = b });
            f.Rooms.Add(new Room { Id = 2, Kind = RoomKind.Combat, Rect = c });
            f.Rooms.Add(new Room { Id = 3, Kind = RoomKind.Boss, Rect = boss });
            var w = new SimWorld(1) { EnemyBrainEnabled = false };
            w.UseFloorForTest(f);
            w.Hero.Pos = w.Hero.PrevPos = new Vec2(5.5f, 5.5f);
            var e = w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 1e6f, Damage = 0f, MoveSpeed = 0f, Xp = 0 }, new Vec2(28.5f, 5.5f));
            e.HomeRoom = 2;
            bool reached = false;
            for (int i = 0; i < 200 && !reached; i++)
            {
                w.Tick();
                Assert.That(f.Rooms[1].Visited, Is.False, "walked into the empty side room");
                reached = f.RoomAt(w.Hero.Pos) == f.Rooms[2];
            }
            Assert.That(reached, Is.True, "went for the room with the pack");
            Assert.That(f.Rooms[1].Cleared, Is.True, "a room with nothing left counts as cleared unvisited");
        }

        [Test]
        public void Walking_heading_is_not_locked_to_four_directions()
        {
            var w = new SimWorld(1);
            var headings = new HashSet<int>();
            for (int i = 0; i < 3000; i++)
            {
                w.Tick();
                var d = w.Hero.Pos - w.Hero.PrevPos;
                if (d.LengthSq < 1e-6f) continue;
                int deg = (int)System.Math.Round(System.Math.Atan2(d.Y, d.X) * 180.0 / System.Math.PI / 15.0) * 15; // 15° buckets
                headings.Add(deg);
            }
            Assert.That(headings.Count, Is.GreaterThan(8), "free movement should use many angles, not just 4 grid directions");
        }
        /// <summary>
        /// While walking (not on the last step of a walk), every tick should cover the full stride.
        /// Before the fix the hero stopped dead on each path waypoint and lost the rest of the stride,
        /// so ≈10% of walking ticks were short and the speed visibly pulsed.
        /// </summary>
        [Test]
        public void Hero_walks_at_a_steady_stride_through_waypoints()
        {
            var w = new SimWorld(1);
            var steps = new List<(float len, float stride)>();
            Vec2 prevDelta = Vec2.Zero;
            float prevStride = 0f;
            for (int i = 0; i < 3000; i++)
            {
                w.Tick();
                if (w.Events.Exists(e => e.Type == SimEventType.FloorCleared || e.Type == SimEventType.HeroDied)) { prevDelta = Vec2.Zero; continue; }
                var d = w.Hero.Pos - w.Hero.PrevPos;
                // A walking tick followed by another walking tick is "mid-walk": it must be a full stride.
                if (prevDelta.LengthSq > 1e-6f && d.LengthSq > 1e-6f) steps.Add((prevDelta.Length, prevStride));
                prevDelta = d;
                prevStride = w.Hero.Stats.MoveSpeed * SimWorld.Dt;
            }
            int short_ = steps.FindAll(s => s.len < s.stride * 0.9f).Count;
            Assert.That(steps.Count, Is.GreaterThan(500));
            Assert.That(short_ / (float)steps.Count, Is.LessThan(0.02f), $"{short_} of {steps.Count} mid-walk steps were short");
        }
    }
}
