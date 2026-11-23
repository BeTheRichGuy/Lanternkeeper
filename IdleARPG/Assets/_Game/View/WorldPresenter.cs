using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 성장 줌 (희승 11-22: "초반 약할때는 캐릭터를 좀 확대해서 보여주다가, 이동속도 빨라지고 공격범위가 커짐에 따라
    /// 연속적으로 줌 아웃"). Pixel art only stays even at whole zooms, so the world camera renders at the next whole zoom
    /// into an offscreen texture a little larger than the screen, and the texture is drawn over the whole screen with
    /// bilinear filtering (integer prescale + bilinear: every art pixel stays one block, only its edges soften). Any
    /// zoom between whole steps then looks sharp and steady.
    /// </summary>
    public sealed class WorldPresenter
    {
        readonly Camera _world;
        readonly PixelPerfectCamera _ppc;
        readonly RawImage _image;
        RenderTexture _rt;

        // 화면 왜곡 (희승 11-23 VFX 미션): shockwaves bend the picture as it is drawn (Shaders/WorldPresent).
        struct Rip { public Vector2 C; public float R1, T, Life, Strength; }
        readonly System.Collections.Generic.List<Rip> _rips = new System.Collections.Generic.List<Rip>();
        readonly Material _present;
        readonly Vector4[] _ripData = new Vector4[8], _ripW = new Vector4[8];
        static readonly int IdCam = Shader.PropertyToID("_Cam"), IdN = Shader.PropertyToID("_RipN"), IdRip = Shader.PropertyToID("_Rip"),
            IdRipW = Shader.PropertyToID("_RipW"), IdArtPx = Shader.PropertyToID("_ArtPx");

        /// <summary>The whole zoom the world is rendered at (texture pixels per art pixel).</summary>
        public static int Whole { get; private set; } = 1;

        /// <summary>Screen pixels per art pixel being shown.</summary>
        public float Zoom { get; private set; } = 1f;

        public WorldPresenter(Camera world, PixelPerfectCamera ppc)
        {
            _world = world;
            _ppc = ppc;
            // A camera that only clears the screen: the world arrives as a picture on the canvas under the GUI.
            var present = new GameObject("Present Camera").AddComponent<Camera>();
            present.cullingMask = 0;
            present.clearFlags = CameraClearFlags.SolidColor;
            present.backgroundColor = Color.black;
            present.orthographic = true;
            present.depth = world.depth + 1;
            var canvasGo = new GameObject("World Picture", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100; // under the GUI (100)
            _image = new GameObject("picture", typeof(RectTransform)).AddComponent<RawImage>();
            _image.transform.SetParent(canvasGo.transform, false);
            var r = _image.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            _image.raycastTarget = false;
            var shader = Resources.Load<Shader>("Shaders/WorldPresent");
            if (shader != null)
            {
                _present = new Material(shader);
                _present.SetFloat(IdArtPx, 1f / IsoProjection.PPU);
                _image.material = _present;
            }
        }

        /// <summary>A shockwave over the floor: runs out to <paramref name="radiusCells"/> in <paramref name="seconds"/>,
        /// bending the picture by up to <paramref name="strength"/> × 5 art pixels.</summary>
        public void Ripple(Vector3 world, float radiusCells, float seconds, float strength)
        {
            if (_present == null || !GameSettings.Shake) return; // 설정: 흔들림을 끈 사람에게는 왜곡도 없다
            if (_rips.Count >= 8) _rips.RemoveAt(0);
            _rips.Add(new Rip { C = new Vector2(world.x, world.y), R1 = 0.707f * radiusCells * 1.15f, Life = seconds, Strength = strength * 5f / IsoProjection.PPU });
        }

        static readonly int IdImpact = Shader.PropertyToID("_Impact");
        float _kick, _kickT, _kickLen = 0.18f;

        /// <summary>A zoom punch (review 2, 11-23): the picture leaps <paramref name="amount"/> larger and eases back in
        /// 0.18 s — the picture only, so the world texture is never reallocated.</summary>
        public void Kick(float amount)
        {
            if (!GameSettings.Shake || amount <= _kick * (1f - _kickT / _kickLen)) return;
            _kick = amount; _kickT = 0f;
        }
        float _impactLeft, _impactLen;
        Color _impactCol;

        /// <summary>임팩트 프레임: the picture goes two-tone for <paramref name="seconds"/> (an ultimate, a boss's fall).</summary>
        public void Impact(Color color, float seconds)
        {
            if (_present == null || !GameSettings.Shake) return; // 설정: 흔들림을 끈 사람에게는 이것도 없다
            _impactCol = color; _impactLen = _impactLeft = seconds;
            Kick(0.03f);
        }

        void TickRipples(float dt)
        {
            if (_present == null) return;
            _impactLeft = Mathf.Max(0f, _impactLeft - Time.unscaledDeltaTime);
            if (_kickT < _kickLen) _kickT += Time.unscaledDeltaTime;
            float kk = _kickT < _kickLen ? _kick * (1f - _kickT / _kickLen) * (1f - _kickT / _kickLen) : 0f;
            _image.rectTransform.localScale = new Vector3(1f + kk, 1f + kk, 1f);
            _present.SetColor(IdImpact, new Color(_impactCol.r, _impactCol.g, _impactCol.b, _impactLeft > 0f ? 0.92f : 0f));
            int n = 0;
            for (int i = _rips.Count - 1; i >= 0; i--)
            {
                var r = _rips[i];
                r.T += dt;
                if (r.T >= r.Life) { _rips.RemoveAt(i); continue; }
                _rips[i] = r;
                float k = r.T / r.Life;
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                _ripData[n] = new Vector4(r.C.x, r.C.y, Mathf.Lerp(0.1f, r.R1, ease), r.Strength * (1f - k) * (1f - k));
                _ripW[n] = new Vector4(Mathf.Lerp(0.12f, 0.32f, k), 0f, 0f, 0f);
                n++;
            }
            _present.SetInt(IdN, n);
            _present.SetVectorArray(IdRip, _ripData);
            _present.SetVectorArray(IdRipW, _ripW);
            // The picture's footprint in the world (what the world camera sees).
            float h = _world.orthographicSize * 2f, w = h * _world.aspect;
            var p = _world.transform.position;
            _present.SetVector(IdCam, new Vector4(p.x - w * 0.5f, p.y - h * 0.5f, w, h));
        }

        public void Apply(float zoom)
        {
            zoom = Mathf.Max(1f, zoom);
            int n = Mathf.Max(1, Mathf.CeilToInt(zoom - 0.001f));
            Whole = n;
            // The texture holds a whole, even number of art pixels each way (the pixel-perfect camera warns on odd sizes).
            int w = Mathf.Max(2, Even(Mathf.CeilToInt(Screen.width / zoom))) * n;
            int h = Mathf.Max(2, Even(Mathf.CeilToInt(Screen.height / zoom))) * n;
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                _world.targetTexture = null;
                if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "World Picture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                _rt.Create();
                _world.targetTexture = _rt;
                _image.texture = _rt;
            }
            if (_ppc != null)
            {
                if (_ppc.refResolutionX != w / n) _ppc.refResolutionX = w / n;
                if (_ppc.refResolutionY != h / n) _ppc.refResolutionY = h / n;
            }
            Zoom = zoom;
            TickRipples(Time.deltaTime);
        }

        static int Even(int v) => v + (v & 1);

        /// <summary>A screen point (mouse) in world space, whatever the camera renders into.</summary>
        public static Vector3 ScreenToWorld(Camera cam, Vector2 screen) =>
            cam.ViewportToWorldPoint(new Vector3(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height), 0f));
    }
}
