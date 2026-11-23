using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    public enum RoomKind { Start, Combat, Horde, Boss, Treasure, Rest, Event }

    /// <summary>GDD §6.2 event rooms.</summary>
    public enum EventKind { None, Shrine, CursedChest, Champion, Merchant, OilSpring }

    public readonly struct GridRect : IEquatable<GridRect>
    {
        public readonly int X, Y, W, H;
        public GridRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public (int x, int y) Center => (X + W / 2, Y + H / 2);
        public bool Contains(int x, int y) => x >= X && y >= Y && x < X + W && y < Y + H;
        public bool Contains(Vec2 p) => Contains((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
        public bool Equals(GridRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is GridRect r && Equals(r);
        public override int GetHashCode() => ((X * 31 + Y) * 31 + W) * 31 + H;
        public override string ToString() => $"[{X},{Y} {W}x{H}]";
    }

    public sealed class Room
    {
        public int Id;
        public RoomKind Kind;
        public GridRect Rect;
        public readonly List<int> Links = new List<int>();
        /// <summary>The hero has stepped inside; a room only counts as cleared after a visit.</summary>
        public bool Visited;
        public bool Cleared;
        /// <summary>Event rooms: what waits inside, the option taken when nobody chooses, and how it went.</summary>
        public EventKind Event;
        public int EventDefault;
        public bool EventResolved, CursedOpen, Rewarded;
    }

    /// <summary>One dungeon floor: a walkable cell grid plus the room graph on top of it.</summary>
    public sealed class Floor
    {
        public readonly int Width, Height;
        readonly bool[] _walk;
        public readonly List<Room> Rooms = new List<Room>();

        public Floor(int width, int height)
        {
            Width = width;
            Height = height;
            _walk = new bool[width * height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsWalkable(int x, int y) => InBounds(x, y) && _walk[y * Width + x];
        public bool IsWalkable(Vec2 p) => IsWalkable((int)Math.Floor(p.X), (int)Math.Floor(p.Y));

        public void Set(int x, int y, bool walkable)
        {
            if (InBounds(x, y)) _walk[y * Width + x] = walkable;
        }

        public void Carve(GridRect r)
        {
            for (int y = r.Y; y < r.Y + r.H; y++)
            for (int x = r.X; x < r.X + r.W; x++)
                if (InBounds(x, y)) _walk[y * Width + x] = true;
        }

        /// <summary>
        /// Walkable cells of a room with a wall right behind them on screen (−x = up-left, −y = up-right):
        /// where chests and decor stand. Row-major order (deterministic).
        /// </summary>
        public List<(int x, int y)> BackEdge(Room room)
        {
            var list = new List<(int x, int y)>();
            var r = room.Rect;
            for (int y = r.Y; y < r.Y + r.H; y++)
            for (int x = r.X; x < r.X + r.W; x++)
                if (IsWalkable(x, y) && (!IsWalkable(x - 1, y) || !IsWalkable(x, y - 1))) list.Add((x, y));
            return list;
        }

        public Room RoomAt(Vec2 p)
        {
            foreach (var r in Rooms) if (r.Rect.Contains(p)) return r;
            return null;
        }
    }
}
