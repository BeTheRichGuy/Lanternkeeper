using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// 8-way BFS over walkable cells, then string pulling: any waypoint the hero can see past is dropped,
    /// so walks are straight lines at any angle (Nomad-Idle-style free movement) instead of grid staircases.
    /// Floors are small (a few thousand cells), so BFS is enough.
    /// </summary>
    public static class Pathfinder
    {
        static readonly (int dx, int dy)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        const float Clearance = 0.3f; // keep the body this far from walls when cutting corners

        /// <summary>Waypoints from start (exclusive) to goal (inclusive), straightened; empty if unreachable or already there.</summary>
        public static List<Vec2> Find(Floor floor, Vec2 from, Vec2 to)
        {
            var raw = FindCells(floor, from, to);
            return raw.Count == 0 ? raw : Smooth(floor, from, raw);
        }

        /// <summary>Unsmoothed cell-centre path (8-way; diagonals only when both side cells are open).</summary>
        public static List<Vec2> FindCells(Floor floor, Vec2 from, Vec2 to)
        {
            var path = new List<Vec2>();
            int sx = (int)from.X, sy = (int)from.Y, gx = (int)to.X, gy = (int)to.Y;
            if (!floor.IsWalkable(gx, gy) || (sx == gx && sy == gy)) return path;

            int w = floor.Width;
            var prev = new int[w * floor.Height];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var queue = new Queue<int>();
            int start = sy * w + sx, goal = gy * w + gx;
            if (start < 0 || start >= prev.Length) return path;
            prev[start] = -1;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (cur == goal) break;
                int cx = cur % w, cy = cur / w;
                foreach (var (dx, dy) in Dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!floor.IsWalkable(nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (!floor.IsWalkable(cx + dx, cy) || !floor.IsWalkable(cx, cy + dy))) continue;
                    int n = ny * w + nx;
                    if (prev[n] != -2) continue;
                    prev[n] = cur;
                    queue.Enqueue(n);
                }
            }
            if (prev[goal] == -2) return path;
            for (int c = goal; c != start && c >= 0; c = prev[c])
                path.Add(new Vec2(c % w + 0.5f, c / w + 0.5f));
            path.Reverse();
            return path;
        }

        /// <summary>String pulling: from each kept point, jump to the farthest waypoint still in clear sight.</summary>
        public static List<Vec2> Smooth(Floor floor, Vec2 from, List<Vec2> cells)
        {
            var result = new List<Vec2>();
            var anchor = from;
            int i = 0;
            while (i < cells.Count)
            {
                int far = i;
                for (int j = cells.Count - 1; j > i; j--)
                    if (ClearLine(floor, anchor, cells[j])) { far = j; break; }
                result.Add(cells[far]);
                anchor = cells[far];
                i = far + 1;
            }
            return result;
        }

        /// <summary>True if a body of radius <see cref="Clearance"/> can walk straight from a to b without touching a wall.</summary>
        public static bool ClearLine(Floor floor, Vec2 a, Vec2 b)
        {
            float len = Vec2.Dist(a, b);
            int steps = Math.Max(1, (int)Math.Ceiling(len / 0.2f));
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float x = a.X + (b.X - a.X) * t, y = a.Y + (b.Y - a.Y) * t;
                if (!floor.IsWalkable(new Vec2(x - Clearance, y - Clearance)) || !floor.IsWalkable(new Vec2(x + Clearance, y - Clearance)) ||
                    !floor.IsWalkable(new Vec2(x - Clearance, y + Clearance)) || !floor.IsWalkable(new Vec2(x + Clearance, y + Clearance)))
                    return false;
            }
            return true;
        }
    }
}
