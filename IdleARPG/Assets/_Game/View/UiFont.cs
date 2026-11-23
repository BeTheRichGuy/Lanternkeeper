using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// The game's Korean pixel font: Galmuri by Lee Minseo (SIL Open Font License 1.1, Resources/Fonts/Galmuri-OFL.txt).
    /// Body text in Galmuri11, titles and big numbers in Galmuri14. Falls back to Unity's built-in font if missing.
    /// </summary>
    public static class UiFont
    {
        static Font _body, _bold, _title;

        public static Font Body => _body ??= Load("Fonts/Galmuri11");
        public static Font Bold => _bold ??= Load("Fonts/Galmuri11-Bold");
        public static Font Title => _title ??= Load("Fonts/Galmuri14");

        static Font Load(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f == null) return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Pixel glyphs stay crisp: no smoothing when the atlas is sampled (the atlas is rebuilt as glyphs are added).
            void Crisp(Font font)
            {
                if (font == f && f.material != null && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Point;
            }
            Crisp(f);
            Font.textureRebuilt += Crisp;
            return f;
        }
    }
}
