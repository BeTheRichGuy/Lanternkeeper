namespace Lanternkeeper.View
{
    /// <summary>
    /// Framing. The game runs in a normal resizable window or fullscreen (희승, 2026-11-19: Nomad Idle처럼 해상도에
    /// 따라 조절, 전체화면까지 — replaces the transparent desktop overlay). The pixel-perfect camera zooms by the largest
    /// whole number that still shows at least <see cref="ViewWidth"/>×<see cref="ViewHeight"/> art pixels
    /// (1080p → ×2, 1440p → ×2, 4K → ×4) and simply shows more of the dungeon in the leftover space.
    /// </summary>
    public static class Diorama
    {
        /// <summary>Design size for panels (gear, offline summary): they must fit the smallest view.</summary>
        public const int RefWidth = 768, RefHeight = 576;
        /// <summary>Target view in art pixels for the camera's integer zoom.</summary>
        public const int ViewWidth = 960, ViewHeight = 540;
        /// <summary>First-launch window size (Unity remembers the player's own size afterwards).</summary>
        public const int WindowWidth = 1280, WindowHeight = 720;

        public static float HalfHeight => RefHeight / 2f / IsoProjection.PPU;
        public static float HalfWidth => RefWidth / 2f / IsoProjection.PPU;
    }
}
