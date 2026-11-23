using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Gothic panel frames (art/pixellab/ui, 9-slice borders set by the importer). A panel that was drawn as a
    /// stretched dark dot keeps its size but wears the stone-and-bronze frame instead; missing art leaves it as it was.
    /// </summary>
    public static class UiFrame
    {
        public const string Window = "ui/ui_window_frame_ornate", Plain = "ui/ui_window_frame", Tooltip = "ui/ui_tooltip_frame", BossBar = "ui/ui_boss_bar_frame";

        /// <summary>Swap <paramref name="back"/> (a scaled 2-px dot) for a sliced frame of the same world size.</summary>
        public static void Apply(SpriteRenderer back, SpriteCatalog catalog, string key = Window, float alpha = 0.97f)
        {
            if (back == null || !catalog.Has(key)) return;
            // Panels scale "ui/dot" as if it were 2 px (0.03125 u): that is the size they mean.
            var size = new Vector2(back.transform.localScale.x * 0.03125f, back.transform.localScale.y * 0.03125f);
            back.sprite = catalog.Get(key);
            back.drawMode = SpriteDrawMode.Sliced;
            back.transform.localScale = Vector3.one;
            // Whole art pixels so the bronze corners stay crisp.
            back.size = new Vector2(Mathf.Round(size.x * 64f) / 64f, Mathf.Round(size.y * 64f) / 64f);
            back.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>A new sliced frame of <paramref name="size"/> world units.</summary>
        public static SpriteRenderer Make(Transform parent, SpriteCatalog catalog, Material material, string key, Vector2 size, Vector3 pos, int order)
        {
            if (!catalog.Has(key)) return null;
            var sr = new GameObject("frame").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sharedMaterial = material;
            sr.sprite = catalog.Get(key);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(Mathf.Round(size.x * 64f) / 64f, Mathf.Round(size.y * 64f) / 64f);
            sr.sortingOrder = order;
            return sr;
        }
    }
}
