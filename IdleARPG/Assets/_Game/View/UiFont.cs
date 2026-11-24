using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// The game's Korean fonts, all SIL Open Font License 1.1 (licences beside them in Resources/Fonts). 희승 11-24 "폰트도
    /// 바꾸자. 이 폰트 너무 안이뻐": three sets, picked in 설정 → 글꼴 and applied on the next start (every window lays its
    /// lines out once, with the font it was built with):
    ///   0 명조 — 나눔명조 body, 고운바탕 Bold titles: the dark-fantasy book look, and smooth at 1080p where a
    ///     pixel font is scaled by a non-whole 1.5.
    ///   1 돋움 (default) — 고운돋움 body, 고운바탕 Bold titles: the easiest to read.
    ///   2 픽셀 — Galmuri11 / Galmuri14, the old look.
    /// Falls back to Unity's built-in font if a file is missing.
    /// </summary>
    public static class UiFont
    {
        public const string PrefKey = "ui_font";
        public static readonly string[] SetNames = { "명조", "돋움", "픽셀" };
        /// <summary>희승 11-24 "글씨는 G가 제일 괜찮은 것 같아": 돋움 (고운돋움 body, 고운바탕 Bold titles) by default.</summary>
        public const int DefaultSet = 1;
        static Font _body, _bold, _title;
        static int _set = -1;

        /// <summary>The set in use this run (read once).</summary>
        public static int Set
        {
            get
            {
                if (_set < 0) { try { _set = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, DefaultSet), 0, SetNames.Length - 1); } catch { _set = DefaultSet; } }
                return _set;
            }
        }

        /// <summary>The set the next start uses (설정 → 글꼴); this run keeps <see cref="Set"/>.</summary>
        public static int NextSet => PlayerPrefs.GetInt(PrefKey, DefaultSet);
        public static void CycleNext() => PlayerPrefs.SetInt(PrefKey, (NextSet + 1) % SetNames.Length);

        static bool Pixel => Set == 2;

        public static Font Body => _body ??= Load(Set == 0 ? "Fonts/NanumMyeongjo-Regular" : Set == 1 ? "Fonts/GowunDodum-Regular" : "Fonts/Galmuri11");
        public static Font Bold => _bold ??= Load(Set == 0 ? "Fonts/NanumMyeongjo-Bold" : Set == 1 ? "Fonts/GowunBatang-Bold" : "Fonts/Galmuri11-Bold");
        public static Font Title => _title ??= Load(Pixel ? "Fonts/Galmuri14" : "Fonts/GowunBatang-Bold");

        static Font Load(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f == null) return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Pixel glyphs stay crisp (no smoothing when the atlas is sampled); the smooth sets keep bilinear.
            var mode = Pixel ? FilterMode.Point : FilterMode.Bilinear;
            void Filter(Font font)
            {
                if (font == f && f.material != null && f.material.mainTexture != null) f.material.mainTexture.filterMode = mode;
            }
            Filter(f);
            Font.textureRebuilt += Filter;
            return f;
        }
    }
}
