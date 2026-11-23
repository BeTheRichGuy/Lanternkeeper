using System;

namespace Lanternkeeper.Sim
{
    /// <summary>2D vector in grid space (1 unit = 1 cell).</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        public float LengthSq => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSq);

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len < 1e-6f ? Zero : new Vec2(X / len, Y / len);
            }
        }

        public static float DistSq(Vec2 a, Vec2 b) => (a - b).LengthSq;
        public static float Dist(Vec2 a, Vec2 b) => (a - b).Length;

        /// <summary>Move toward target by at most maxStep.</summary>
        public static Vec2 MoveToward(Vec2 from, Vec2 to, float maxStep)
        {
            var d = to - from;
            float len = d.Length;
            if (len <= maxStep || len < 1e-6f) return to;
            return from + d * (maxStep / len);
        }

        public bool Equals(Vec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }
}
