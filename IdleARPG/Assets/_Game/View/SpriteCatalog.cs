using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Key → sprites. Characters store 8 directions (S, SE, E, NE, N, NW, W, SW).
    /// Animations live under "&lt;key&gt;/&lt;anim&gt;" (idle, walk, attack, hurt, death) stored direction-major:
    /// sprites[dir * frames + frame]. Tiles store variants with frames = 0.
    /// Missing keys fall back to <see cref="PlaceholderArt"/>, so new art drops in without code changes.
    /// Filled by the editor's ArtImporter from art/pixellab.
    /// </summary>
    [CreateAssetMenu(menuName = "Lanternkeeper/Sprite Catalog")]
    public sealed class SpriteCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public Sprite[] sprites;
            /// <summary>Frames per direction for animations; 0 for static sets (directions or variants).</summary>
            public int frames;
            /// <summary>Playback rate for animations.</summary>
            public float fps = 10f;
            /// <summary>Bit per direction (S, SE, E, NE, N, NW, W, SW): that direction reuses its mirror and must be drawn flipped.</summary>
            public int mirrorMask;
            /// <summary>VFX clips: whether the clip repeats.</summary>
            public bool loop;
        }

        /// <summary>A VFX clip ("vfx/&lt;id&gt;/&lt;front|behind|whole&gt;"): frames in order, rate, and whether it loops.</summary>
        public bool TryGetClip(string key, out Sprite[] sprites, out float fps, out bool loop)
        {
            if (Map.TryGetValue(key, out var e) && e.sprites.Length > 0)
            {
                sprites = e.sprites; fps = e.fps; loop = e.loop;
                return true;
            }
            sprites = null; fps = 0f; loop = false;
            return false;
        }

        public List<Entry> entries = new List<Entry>();

        Dictionary<string, Entry> _map;
        /// <summary>key → anim → entry, so per-frame animation lookups never build strings.</summary>
        Dictionary<string, Dictionary<string, Entry>> _anims;
        static readonly HashSet<string> AnimNames = new HashSet<string> { "idle", "walk", "attack", "hurt", "death" };

        Dictionary<string, Entry> Map
        {
            get
            {
                if (_map != null) return _map;
                _map = new Dictionary<string, Entry>();
                _anims = new Dictionary<string, Dictionary<string, Entry>>();
                foreach (var e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.key) || e.sprites == null || e.sprites.Length == 0) continue;
                    _map[e.key] = e;
                    int slash = e.key.LastIndexOf('/');
                    if (slash <= 0 || e.frames <= 0 || !AnimNames.Contains(e.key.Substring(slash + 1))) continue;
                    var owner = e.key.Substring(0, slash);
                    if (!_anims.TryGetValue(owner, out var byAnim)) _anims[owner] = byAnim = new Dictionary<string, Entry>();
                    byAnim[e.key.Substring(slash + 1)] = e;
                }
                return _map;
            }
        }

        Entry AnimEntry(string key, string anim)
        {
            _ = Map;
            return _anims.TryGetValue(key, out var byAnim) && byAnim.TryGetValue(anim, out var e) ? e : null;
        }

        public Sprite Get(string key, int index = 0)
        {
            if (Map.TryGetValue(key, out var e))
            {
                var arr = e.sprites;
                var s = arr[((index % arr.Length) + arr.Length) % arr.Length];
                if (s != null) return s;
            }
            return PlaceholderArt.Get(key, index);
        }

        public bool Has(string key) => Map.ContainsKey(key);

        /// <summary>Every key under a folder prefix ("prop_sand/"), sorted, cached: an act's kit is whatever was delivered.</summary>
        public string[] KeysWithPrefix(string prefix)
        {
            if (_byPrefix.TryGetValue(prefix, out var hit)) return hit;
            var list = new List<string>();
            foreach (var k in Map.Keys) if (k.StartsWith(prefix, System.StringComparison.Ordinal)) list.Add(k);
            list.Sort(System.StringComparer.Ordinal);
            return _byPrefix[prefix] = list.ToArray();
        }

        readonly Dictionary<string, string[]> _byPrefix = new Dictionary<string, string[]>();

        public bool HasAnim(string key, string anim) => AnimEntry(key, anim) != null;

        /// <summary>
        /// Frame of <paramref name="anim"/> at time <paramref name="t"/> seconds; falls back to the static direction sprite.
        /// <paramref name="done"/> is true once a one-shot animation has reached its last frame.
        /// </summary>
        public Sprite GetAnim(string key, string anim, int dir, float t, bool loop, float speed, out bool done) =>
            GetAnim(key, anim, dir, t, loop, speed, out done, out _);

        /// <summary>As above; <paramref name="flipX"/> says the frame is a mirrored direction (draw with flipX).</summary>
        public Sprite GetAnim(string key, string anim, int dir, float t, bool loop, float speed, out bool done, out bool flipX)
        {
            done = true;
            flipX = false;
            var e = AnimEntry(key, anim);
            if (e == null) return Get(key, dir);
            flipX = ((e.mirrorMask >> (dir % 8)) & 1) != 0;
            int n = e.frames;
            int f = (int)(t * e.fps * speed);
            if (loop) { f %= n; done = false; }
            else { done = f >= n - 1; f = Math.Min(f, n - 1); }
            int dirs = Math.Max(1, e.sprites.Length / n); // 8 normally; tolerate 4-direction sets
            var s = e.sprites[(dir % dirs) * n + f];
            return s != null ? s : Get(key, dir);
        }

        public float Duration(string key, string anim, float speed = 1f)
        {
            var e = AnimEntry(key, anim);
            return e != null ? e.frames / (e.fps * speed) : 0f;
        }

        void OnValidate() => Invalidate();

        // Domain reload is off in this project: rebuild the lookup whenever the asset is (re)loaded.
        void OnEnable() => Invalidate();

        /// <summary>Call after editing <see cref="entries"/> from code.</summary>
        public void Invalidate() { _map = null; _anims = null; }
    }
}
