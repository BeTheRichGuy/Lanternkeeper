using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// Builds a floor as a room graph on a coarse slot grid (GDD §6.1): a main path from the start room to the
    /// boss room, plus dead-end branches (treasure, rest, combat).
    /// Terrain is organic (희승, 2026-11-19: "사각형 말고 구불구불"): each room is a noise-warped blob inside its
    /// 16×16 slot, rooms are joined by meandering corridors of varying width, edges are smoothed, and anything
    /// not reachable from the start is filled back in. Rooms stay inside their slots and corridors stay inside
    /// their two slots' shared row/column band, so unrelated rooms are always several wall cells apart
    /// (no fighting through thin walls). Deterministic per seed.
    /// </summary>
    public static class FloorGenerator
    {
        public const int Slot = 16;
        const int Margin = 2, Rows = 3;

        public static Floor Generate(int seed, int roomCount)
        {
            if (roomCount < 2) throw new ArgumentOutOfRangeException(nameof(roomCount), "need at least start + boss");
            var rng = new SimRng((ulong)seed);

            int branches = roomCount >= 5 ? roomCount / 3 : 0;
            int mainLen = roomCount - branches;

            // 1. Main path: walk right, sometimes stepping up or down a row.
            var slots = new List<(int x, int y)>();
            var used = new HashSet<(int, int)>();
            var cur = (x: 0, y: rng.Range(0, Rows));
            slots.Add(cur); used.Add(cur);
            while (slots.Count < mainLen)
            {
                var options = new List<(int x, int y)> { (cur.x + 1, cur.y) };
                if (cur.y > 0 && !used.Contains((cur.x, cur.y - 1))) options.Add((cur.x, cur.y - 1));
                if (cur.y < Rows - 1 && !used.Contains((cur.x, cur.y + 1))) options.Add((cur.x, cur.y + 1));
                // Prefer moving right (keeps floors wide, not tangled).
                var next = rng.Chance(0.6f) || options.Count == 1 ? options[0] : options[rng.Range(1, options.Count)];
                slots.Add(next); used.Add(next); cur = next;
            }
            var links = new List<(int a, int b)>();
            for (int i = 1; i < mainLen; i++) links.Add((i - 1, i));

            // 2. Branches: dead ends hanging off middle main-path rooms into free neighbour slots.
            int attempts = 0;
            while (slots.Count < roomCount && attempts++ < 200)
            {
                int parent = attempts < 60 ? rng.Range(1, Math.Max(2, mainLen - 1)) : rng.Range(0, slots.Count);
                if (parent == mainLen - 1) continue;
                var p = slots[parent];
                var dirs = new[] { (0, -1), (0, 1), (1, 0), (-1, 0) };
                var (dx, dy) = dirs[rng.Range(0, dirs.Length)];
                var s = (p.x + dx, p.y + dy);
                if (s.Item1 < 0 || s.Item2 < 0 || s.Item2 >= Rows || used.Contains(s)) continue;
                used.Add(s);
                slots.Add(s);
                links.Add((parent, slots.Count - 1));
            }
            roomCount = slots.Count;

            int maxX = 0;
            foreach (var s in slots) maxX = Math.Max(maxX, s.x);
            var floor = new Floor((maxX + 1) * Slot + Margin * 2, Rows * Slot + Margin * 2);

            // 3. Rooms: organic blobs centred in their slots (the rect is the blob's bounding box).
            int hordeAt = mainLen >= 4 ? 2 : 1;
            for (int i = 0; i < roomCount; i++)
            {
                bool boss = i == mainLen - 1;
                bool main = i < mainLen;
                int w = boss ? 13 : rng.Range(9, 14), h = boss ? 13 : rng.Range(9, 14);
                var (sx, sy) = slots[i];
                var rect = new GridRect(Margin + sx * Slot + (Slot - w) / 2, Margin + sy * Slot + (Slot - h) / 2, w, h);
                RoomKind kind;
                if (i == 0) kind = RoomKind.Start;
                else if (boss) kind = RoomKind.Boss;
                else if (main) kind = i == hordeAt ? RoomKind.Horde : RoomKind.Combat;
                else
                {
                    float roll = rng.NextFloat();
                    kind = roll < 0.35f ? RoomKind.Treasure : roll < 0.55f ? RoomKind.Rest : roll < 0.85f ? RoomKind.Event : RoomKind.Combat;
                }
                floor.Rooms.Add(new Room { Id = i, Kind = kind, Rect = rect });
                CarveBlob(floor, rect, rng, boss);
            }

            // 4. Meandering corridors between linked rooms.
            foreach (var (a, b) in links)
            {
                floor.Rooms[a].Links.Add(b);
                floor.Rooms[b].Links.Add(a);
                CarveMeander(floor, SlotCentre(slots[a]), SlotCentre(slots[b]), rng);
            }

            // 5. Tidy edges, then make sure what is left is one connected cave.
            Smooth(floor);
            EnsureConnected(floor, slots, links);
            return floor;
        }

        static (float x, float y) SlotCentre((int x, int y) s) => (Margin + s.x * Slot + Slot / 2f, Margin + s.y * Slot + Slot / 2f);

        /// <summary>A rough ellipse whose radius wobbles with two sine harmonics: no two rooms share a silhouette.</summary>
        static void CarveBlob(Floor f, GridRect r, SimRng rng, bool boss)
        {
            float cx = r.X + r.W / 2f, cy = r.Y + r.H / 2f, rx = r.W / 2f, ry = r.H / 2f;
            float a1 = boss ? 0.08f : rng.Range(0.10f, 0.22f), a2 = rng.Range(0.04f, 0.12f);
            int k1 = rng.Range(2, 5), k2 = rng.Range(5, 8);
            float p1 = rng.Range(0f, 6.283f), p2 = rng.Range(0f, 6.283f);
            for (int y = r.Y; y < r.Y + r.H; y++)
            for (int x = r.X; x < r.X + r.W; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float rad = (float)Math.Sqrt(dx * dx + dy * dy);
                float ang = (float)Math.Atan2(dy, dx);
                float limit = 0.95f + a1 * (float)Math.Sin(k1 * ang + p1) + a2 * (float)Math.Sin(k2 * ang + p2);
                if (rad <= limit) f.Set(x, y, true);
            }
            // The middle is always open (spawns, chests and the explore target sit there).
            var (mx, my) = r.Center;
            for (int y = my - 1; y <= my + 1; y++) for (int x = mx - 1; x <= mx + 1; x++) f.Set(x, y, true);
        }

        /// <summary>
        /// A corridor that wanders: midpoint displacement sideways (kept inside the two slots' shared band),
        /// carved with a brush whose radius drifts between 1.0 and 1.9 cells (about 2–4 cells wide).
        /// </summary>
        static void CarveMeander(Floor f, (float x, float y) a, (float x, float y) b, SimRng rng)
        {
            var pts = new List<(float x, float y)> { a, b };
            bool horizontal = Math.Abs(b.x - a.x) > Math.Abs(b.y - a.y);
            const float maxOffset = Slot / 2f - 4f; // brush (≤1.9) + offset stays ≥ 2 cells inside the band
            float amp = 3.5f;
            for (int level = 0; level < 3; level++, amp *= 0.55f)
            {
                var next = new List<(float x, float y)>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    next.Add(pts[i]);
                    float mx = (pts[i].x + pts[i + 1].x) / 2f, my = (pts[i].y + pts[i + 1].y) / 2f;
                    float off = rng.Range(-amp, amp);
                    if (horizontal) my = Clamp(my + off, a.y - maxOffset, a.y + maxOffset);
                    else mx = Clamp(mx + off, a.x - maxOffset, a.x + maxOffset);
                    next.Add((mx, my));
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }
            float brush = rng.Range(1.0f, 1.6f);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var (x0, y0) = pts[i];
                var (x1, y1) = pts[i + 1];
                float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                int steps = Math.Max(1, (int)(len / 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    brush = Clamp(brush + rng.Range(-0.08f, 0.08f), 1.0f, 1.9f);
                    Stamp(f, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, brush);
                }
            }
        }

        static void Stamp(Floor f, float px, float py, float radius)
        {
            int r = (int)Math.Ceiling(radius);
            for (int y = (int)py - r; y <= (int)py + r; y++)
            for (int x = (int)px - r; x <= (int)px + r; x++)
            {
                float dx = x + 0.5f - px, dy = y + 0.5f - py;
                if (dx * dx + dy * dy <= radius * radius) f.Set(x, y, true);
            }
        }

        /// <summary>One cellular-automaton pass: fill pin-holes, shave one-cell spikes.</summary>
        static void Smooth(Floor f)
        {
            var flips = new List<(int x, int y, bool v)>();
            for (int y = 1; y < f.Height - 1; y++)
            for (int x = 1; x < f.Width - 1; x++)
            {
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && f.IsWalkable(x + dx, y + dy)) n++;
                bool w = f.IsWalkable(x, y);
                if (!w && n >= 6) flips.Add((x, y, true));
                else if (w && n <= 2) flips.Add((x, y, false));
            }
            foreach (var (x, y, v) in flips) f.Set(x, y, v);
        }

        /// <summary>If smoothing cut a link, re-stamp it straight; then fill in pockets the start can't reach.</summary>
        static void EnsureConnected(Floor f, List<(int x, int y)> slots, List<(int a, int b)> links)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var seen = Flood(f, f.Rooms[0].Rect.Center);
                bool repaired = false;
                foreach (var (a, b) in links)
                {
                    var ca = f.Rooms[a].Rect.Center;
                    var cb = f.Rooms[b].Rect.Center;
                    if (seen[ca.y * f.Width + ca.x] && seen[cb.y * f.Width + cb.x]) continue;
                    var (ax, ay) = SlotCentre(slots[a]);
                    var (bx, by) = SlotCentre(slots[b]);
                    for (int s = 0; s <= 40; s++) { float t = s / 40f; Stamp(f, ax + (bx - ax) * t, ay + (by - ay) * t, 1.2f); }
                    repaired = true;
                }
                if (repaired) continue;
                for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                    if (f.IsWalkable(x, y) && !seen[y * f.Width + x]) f.Set(x, y, false);
                return;
            }
        }

        static bool[] Flood(Floor f, (int x, int y) start)
        {
            var seen = new bool[f.Width * f.Height];
            var q = new Queue<(int, int)>();
            q.Enqueue(start);
            seen[start.y * f.Width + start.x] = true;
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                foreach (var (dx, dy) in dirs)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!f.IsWalkable(nx, ny) || seen[ny * f.Width + nx]) continue;
                    seen[ny * f.Width + nx] = true;
                    q.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
