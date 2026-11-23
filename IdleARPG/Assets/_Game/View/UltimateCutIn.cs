using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// An ultimate (Lv30, GDD §7) gets a cut-in: the screen dims for a beat, the class portrait slides in from the left
    /// and the skill's name lands in large type. ~1.4 s, sits under the panels.
    /// </summary>
    public sealed class UltimateCutIn
    {
        const float Life = 1.4f;
        readonly Transform _root;
        readonly SpriteRenderer _shade, _band, _portrait;
        readonly TextMesh _name, _sub;
        readonly SpriteCatalog _catalog;
        float _t = Life, _halfW = 7.5f, _halfH = 4.2f;

        public UltimateCutIn(Transform camera, SpriteCatalog catalog, Material material)
        {
            _catalog = catalog;
            _root = new GameObject("Ultimate Cut-in").transform;
            _root.SetParent(camera, false);
            _root.localPosition = new Vector3(0f, 0f, 9.8f);
            _shade = Sprite(material, "ui/dot", 32200);
            _shade.color = new Color(0f, 0f, 0f, 0f);
            _band = Sprite(material, "ui/dot", 32201);
            _portrait = Sprite(material, null, 32202);
            var font = UiFont.Title;
            _name = Text(font, 0.075f, 32203);
            _sub = Text(font, 0.032f, 32203);
            _root.gameObject.SetActive(false);
        }

        SpriteRenderer Sprite(Material m, string key, int order)
        {
            var sr = new GameObject("sr").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sharedMaterial = m;
            if (key != null) sr.sprite = _catalog.Get(key);
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh Text(Font font, float size, int order)
        {
            var go = new GameObject("text");
            go.transform.SetParent(_root, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleLeft;
            tm.richText = true;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        public void Layout(float halfW, float halfH, float ui)
        {
            _halfW = halfW / ui; _halfH = halfH / ui;
            _root.localScale = Vector3.one * ui;
        }

        public void Play(string classId, SkillDef skill)
        {
            string portrait = ClassSelectPanel.PortraitOf(classId);
            _portrait.sprite = portrait != null && _catalog.Has(portrait) ? _catalog.Get(portrait) : null;
            _name.text = skill.Name;
            _sub.text = SkillBook.ClassName(classId) + " · 궁극기";
            _t = 0f;
            _root.gameObject.SetActive(true);
        }

        static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Real time (unscaled), so the hit-stop it comes with does not stretch it.</summary>
        public void Render(float realDt)
        {
            if (_t >= Life) return;
            _t += realDt;
            if (_t >= Life) { _root.gameObject.SetActive(false); return; }
            float fadeIn = Mathf.Clamp01(_t / 0.12f), fadeOut = 1f - Mathf.Clamp01((_t - (Life - 0.3f)) / 0.3f);
            float a = fadeIn * fadeOut;
            const float dot = 0.03125f;
            _shade.transform.localScale = new Vector3(_halfW * 2.2f / dot, _halfH * 2.2f / dot, 1f);
            _shade.color = new Color(0f, 0f, 0f, 0.45f * a);
            // A warm band across the lower third that the portrait and name sit on.
            float bandW = _halfW * 2.2f * Mathf.Clamp01(_t / 0.18f);
            _band.transform.localScale = new Vector3(bandW / dot, 1.5f / dot, 1f);
            _band.transform.localPosition = new Vector3(-_halfW * 1.1f + bandW / 2f, -_halfH * 0.32f, 0f);
            _band.color = new Color(0.55f, 0.22f, 0.08f, 0.55f * a);
            float slide = OutBack(Mathf.Clamp01(_t / 0.35f));
            _portrait.transform.localPosition = new Vector3(Mathf.Lerp(-_halfW - 2f, -_halfW * 0.62f, slide), -_halfH * 0.32f + 0.1f, 0f);
            _portrait.transform.localScale = Vector3.one * 2.2f;
            _portrait.color = new Color(1f, 1f, 1f, a);
            float textIn = Mathf.Clamp01((_t - 0.15f) / 0.2f);
            _name.transform.localPosition = new Vector3(-_halfW * 0.4f + (1f - textIn) * 0.8f, -_halfH * 0.32f + 0.18f, 0f);
            _name.color = new Color(1f, 0.85f, 0.55f, a * textIn);
            _sub.transform.localPosition = new Vector3(-_halfW * 0.4f + (1f - textIn) * 0.8f, -_halfH * 0.32f - 0.35f, 0f);
            _sub.color = new Color(0.9f, 0.8f, 0.7f, a * textIn);
        }
    }
}
