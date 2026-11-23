using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>Grid cell space → Unity world space for 2:1 isometric, 64×32 tiles at PPU 64.</summary>
    public static class IsoProjection
    {
        public const float PPU = 64f;

        public static Vector3 ToWorld(Vec2 p) => ToWorld(p.X, p.Y);

        public static Vector3 ToWorld(float x, float y) => new Vector3((x - y) * 0.5f, -(x + y) * 0.25f, 0f);

        /// <summary>Lower on screen draws in front.</summary>
        public static int SortOrder(Vector3 world, int bias = 0) => Mathf.RoundToInt(-world.y * 100f) + bias;

        /// <summary>8-way index in PixelLab order: S, SE, E, NE, N, NW, W, SW.</summary>
        public static int Dir8(Vec2 facingGrid)
        {
            var s = ToWorld(facingGrid);
            if (s.sqrMagnitude < 1e-6f) return 0;
            float deg = Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg; // 0 = east, 90 = north
            int sector = ((Mathf.RoundToInt(deg / 45f) % 8) + 8) % 8; // 0 E,1 NE,2 N,3 NW,4 W,5 SW,6 S,7 SE
            return (sector + 2) % 8;
        }
    }
}
