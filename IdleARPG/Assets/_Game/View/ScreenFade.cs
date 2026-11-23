using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Full-screen fades: a new floor fades in from black instead of popping; a death sinks into dark red with
    /// "등불이 꺼졌다" before the checkpoint fades in (D2's death screen, quieter). Real time, above the world, under panels.
    /// </summary>
    public sealed class ScreenFade
    {
        readonly Transform _root;
        readonly SpriteRenderer _veil;
        readonly TextMesh _text, _sub;
        float _t = 99f, _life, _hold;
        Color _color;
        bool _death;

        public ScreenFade(Transform camera, SpriteCatalog catalog, Material material)
        {
            _root = new GameObject("Screen Fade").transform;
            _root.SetParent(camera, false);
            _root.localPosition = new Vector3(0f, 0f, 9.9f);
            _veil = new GameObject("veil").AddComponent<SpriteRenderer>();
            _veil.transform.SetParent(_root, false);
            _veil.sharedMaterial = material;
            _veil.sprite = catalog.Get("ui/dot");
            _veil.sortingOrder = 32300;
            var font = UiFont.Title;
            _text = NewText(font, 0.07f, new Vector3(0f, 0.25f, 0f));
            _sub = NewText(font, 0.032f, new Vector3(0f, -0.35f, 0f));
            _root.gameObject.SetActive(false);
        }

        TextMesh NewText(Font font, float size, Vector3 pos)
        {
            var go = new GameObject("text");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 32301;
            return tm;
        }

        public void Layout(float halfW, float halfH, float ui)
        {
            _veil.transform.localScale = new Vector3(halfW * 2.2f / 0.03125f, halfH * 2.2f / 0.03125f, 1f);
            _text.transform.localScale = _sub.transform.localScale = Vector3.one * ui;
            _text.transform.localPosition = new Vector3(0f, 0.25f * ui, 0f);
            _sub.transform.localPosition = new Vector3(0f, -0.35f * ui, 0f);
        }

        /// <summary>A new floor: from black to clear.</summary>
        public void FloorIn()
        {
            if (_death && _t < _life) return; // the death fade already covers it
            Begin(new Color(0f, 0f, 0f), 0f, 0.55f, false);
        }

        /// <summary>The hero fell: sink to dark red, say so, then clear on the checkpoint.</summary>
        public void Death(int deaths, bool retreat = false)
        {
            Begin(new Color(0.12f, 0.01f, 0.01f), 1.3f, 2.4f, true);
            _text.text = "등불이 꺼졌다";
            _sub.text = (retreat ? "한 구역 후퇴하여 정비합니다" : "구역 처음으로 복귀합니다") + " · 장비는 유지" + (deaths > 1 ? "   (" + deaths + "회차)" : "");
        }

        void Begin(Color c, float hold, float life, bool death)
        {
            _color = c; _hold = hold; _life = life; _death = death; _t = 0f;
            _text.gameObject.SetActive(death);
            _sub.gameObject.SetActive(death);
            _root.gameObject.SetActive(true);
        }

        public void Render(float realDt)
        {
            if (_t >= _life) return;
            _t += realDt;
            if (_t >= _life) { _root.gameObject.SetActive(false); _death = false; return; }
            float a;
            if (_death)
            {
                // Sink in for 0.35 s, hold, then lift over what is left.
                a = _t < 0.35f ? _t / 0.35f : _t < 0.35f + _hold ? 1f : 1f - (_t - 0.35f - _hold) / (_life - 0.35f - _hold);
                a *= 0.92f;
            }
            else a = 1f - _t / _life;
            _veil.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(a));
            float ta = _death ? Mathf.Clamp01((_t - 0.25f) / 0.3f) * Mathf.Clamp01(a / 0.92f) : 0f;
            _text.color = new Color(0.85f, 0.25f, 0.2f, ta);
            _sub.color = new Color(0.8f, 0.72f, 0.65f, ta);
        }
    }
}
