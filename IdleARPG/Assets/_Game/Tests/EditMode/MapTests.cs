using System.Collections.Generic;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class MapTests
    {
        [Test]
        public void SameSeedSameFloor()
        {
            var a = FloorGenerator.Generate(42, 5);
            var b = FloorGenerator.Generate(42, 5);
            Assert.That(a.Rooms.Count, Is.EqualTo(b.Rooms.Count));
            for (int i = 0; i < a.Rooms.Count; i++)
                Assert.That(a.Rooms[i].Rect, Is.EqualTo(b.Rooms[i].Rect));
        }

        [TestCase(1, 3)]
        [TestCase(7, 5)]
        [TestCase(99, 12)]
        public void AllRoomsReachable(int seed, int roomCount)
        {
            var floor = FloorGenerator.Generate(seed, roomCount);
            var start = floor.Rooms[0].Rect.Center;
            var seen = new bool[floor.Width * floor.Height];
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(start);
            seen[start.y * floor.Width + start.x] = true;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!floor.IsWalkable(nx, ny) || seen[ny * floor.Width + nx]) continue;
                    seen[ny * floor.Width + nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
            foreach (var room in floor.Rooms)
            {
                var c = room.Rect.Center;
                Assert.That(seen[c.y * floor.Width + c.x], Is.True, $"room {room.Id} unreachable");
            }
        }

        [TestCase(3, 6)]
        [TestCase(8, 9)]
        [TestCase(21, 12)]
        public void HasOneStartAndOneBossRoom(int seed, int count)
        {
            var floor = FloorGenerator.Generate(seed, count);
            Assert.That(floor.Rooms.FindAll(r => r.Kind == RoomKind.Boss).Count, Is.EqualTo(1));
            Assert.That(floor.Rooms[0].Kind, Is.EqualTo(RoomKind.Start));
        }

        [TestCase(6)]
        [TestCase(9)]
        [TestCase(12)]
        public void Room_count_matches_request(int count)
        {
            for (int seed = 0; seed < 20; seed++)
                Assert.That(FloorGenerator.Generate(seed, count).Rooms.Count, Is.EqualTo(count), $"seed {seed}");
        }

        [Test]
        public void Rooms_are_separated_by_at_least_two_wall_cells()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var f = FloorGenerator.Generate(seed, 9);
                for (int i = 0; i < f.Rooms.Count; i++)
                for (int j = i + 1; j < f.Rooms.Count; j++)
                {
                    var a = f.Rooms[i].Rect; var b = f.Rooms[j].Rect;
                    // Grow a by 2: any overlap means fewer than two cells of wall between the rooms.
                    bool overlap = a.X - 2 < b.X + b.W && b.X < a.X + a.W + 2 && a.Y - 2 < b.Y + b.H && b.Y < a.Y + a.H + 2;
                    Assert.That(overlap, Is.False, $"seed {seed}: rooms {i} and {j} too close");
                }
            }
        }

        [Test]
        public void Branch_rooms_include_treasure_or_rest_on_big_floors()
        {
            int special = 0;
            for (int seed = 0; seed < 20; seed++)
                special += FloorGenerator.Generate(seed, 9).Rooms.FindAll(r => r.Kind == RoomKind.Treasure || r.Kind == RoomKind.Rest).Count;
            Assert.That(special, Is.GreaterThan(20), "about one special room per floor on average");
        }

        /// <summary>Organic terrain (희승, 2026-11-19): rooms are irregular blobs, not filled rectangles.</summary>
        [Test]
        public void Rooms_are_organic_not_rectangles()
        {
            float total = 0f; int n = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var f = FloorGenerator.Generate(seed, 9);
                foreach (var r in f.Rooms)
                {
                    int open = 0;
                    for (int y = r.Rect.Y; y < r.Rect.Y + r.Rect.H; y++)
                    for (int x = r.Rect.X; x < r.Rect.X + r.Rect.W; x++)
                        if (f.IsWalkable(x, y)) open++;
                    total += open / (float)(r.Rect.W * r.Rect.H);
                    n++;
                }
            }
            Assert.That(total / n, Is.LessThan(0.9f));
        }

        [Test]
        public void Every_walkable_cell_is_reachable_from_the_start()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var f = FloorGenerator.Generate(seed, 9);
                var (sx, sy) = f.Rooms[0].Rect.Center;
                var seen = new bool[f.Width * f.Height];
                var q = new System.Collections.Generic.Queue<(int x, int y)>();
                q.Enqueue((sx, sy)); seen[sy * f.Width + sx] = true;
                while (q.Count > 0)
                {
                    var (x, y) = q.Dequeue();
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (!f.IsWalkable(nx, ny) || seen[ny * f.Width + nx]) continue;
                        seen[ny * f.Width + nx] = true; q.Enqueue((nx, ny));
                    }
                }
                for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                    if (f.IsWalkable(x, y)) Assert.That(seen[y * f.Width + x], Is.True, $"seed {seed}: pocket at {x},{y}");
            }
        }

        [Test]
        public void HasHordeRoomWhenRoomCountAtLeast3()
        {
            var floor = FloorGenerator.Generate(5, 3);
            Assert.That(floor.Rooms.Exists(r => r.Kind == RoomKind.Horde), Is.True);
        }
    }
}
