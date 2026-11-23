using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Item drops on the diorama: the item pops out in an arc, lands under a light shaft in its rarity colour
    /// (taller and pulsing for legendary and up), rare+ show their name, then it flies into the hero's lantern.
    /// The Sim has already put it in the bag or on the hero; this is the moment, not the logic.
    /// 재기획 §6: a soft pool of the rarity's colour on the floor under it, the rarity told 0.2 s after it lands (the shaft
    /// lands white, then takes its colour), a thin bright core shaft for jackpots, and names on a dark band.
    /// </summary>
    public sealed class DropView
    {
        sealed class Drop
        {
            public SpriteRenderer Icon, Beam, Core, Pool, Plate;
            public TextMesh Name;
            public MeshRenderer NameMr;
            public Vec2 From, Land;
            public float T;
            /// <summary>How long it lies before flying in: longer the rarer (1122_numbers_loot.md: 공개 지연).</summary>
            public float Lie;
            public Rarity Rarity;
            public bool Active, Jackpot;
        }

        const float Arc = 0.35f, Fly = 0.35f;

        static float LieFor(Rarity r, bool jackpot) => jackpot ? 2.6f : r >= Rarity.Rare ? 1.4f : r == Rarity.Magic ? 0.8f : 0.5f;
        const int Order = 30500;
        readonly Transform _root;
        readonly SpriteCatalog _catalog;
        readonly Material _material;
        readonly Font _font;
        readonly List<Drop> _pool = new List<Drop>();
        readonly System.Random _jitter = new System.Random(7);
        readonly List<Vector3> _placed = new List<Vector3>(); // this frame's name labels: x, y, width

        public DropView(Transform root, SpriteCatalog catalog, Material material)
        {
            _root = root;
            _catalog = catalog;
            _material = material;
            _font = UiFont.Body;
        }

        public void Spawn(Item item, Vec2 at)
        {
            var d = _pool.Find(x => !x.Active);
            if (d == null)
            {
                d = new Drop();
                d.Pool = NewSprite("pool", _catalog.Get("fx/disc"), Order - 1);
                d.Beam = NewSprite("beam", _catalog.Get("fx/beam"), Order);
                d.Core = NewSprite("core", _catalog.Get("fx/beam"), Order);
                d.Plate = NewSprite("plate", _catalog.Get("ui/dot"), Order + 1);
                d.Icon = NewSprite("drop", null, Order + 1);
                var go = new GameObject("drop name");
                go.transform.SetParent(_root, false);
                d.Name = go.AddComponent<TextMesh>();
                d.Name.font = _font;
                d.Name.fontSize = 48;
                d.Name.characterSize = 0.045f;
                d.Name.anchor = TextAnchor.LowerCenter;
                d.NameMr = go.GetComponent<MeshRenderer>();
                d.NameMr.sharedMaterial = _font.material;
                d.NameMr.sortingOrder = Order + 2;
                _pool.Add(d);
            }
            float a = (float)(_jitter.NextDouble() * Mathf.PI * 2.0), r = 0.4f + (float)_jitter.NextDouble() * 0.6f;
            d.From = at;
            d.Land = at + new Vec2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            d.T = 0f;
            d.Rarity = item.Rarity;
            d.Jackpot = ItemStyle.IsJackpot(item);
            d.Lie = LieFor(item.Rarity, d.Jackpot);
            d.Active = true;
            d.Icon.sprite = _catalog.Get(ItemStyle.Icon(item));
            d.Icon.enabled = d.Beam.enabled = d.Pool.enabled = true;
            d.Core.enabled = d.Jackpot;
            int stars = item.GreaterCount;
            d.Name.text = item.Rarity >= Rarity.Rare ? (stars > 0 ? new string('★', stars) + " " : "") + item.Name : "";
            d.Name.color = ItemStyle.Color(item.Rarity);
            d.NameMr.enabled = d.Plate.enabled = item.Rarity >= Rarity.Rare;
            d.NameMr.ResetBounds(); // the band is sized from these bounds
        }

        SpriteRenderer NewSprite(string name, Sprite s, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(_root, false);
            sr.sharedMaterial = _material;
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        public void Clear()
        {
            foreach (var d in _pool) Hide(d);
        }

        static void Hide(Drop d)
        {
            d.Active = false;
            d.Icon.enabled = d.Beam.enabled = d.Core.enabled = d.Pool.enabled = d.Plate.enabled = false;
            d.NameMr.enabled = false;
        }

        public void Render(float dt, Vec2 heroPos)
        {
            var heroWorld = IsoProjection.ToWorld(heroPos) + new Vector3(0f, 0.45f, 0f);
            _placed.Clear();
            foreach (var d in _pool)
            {
                if (!d.Active) continue;
                d.T += dt;
                var col = ItemStyle.Color(d.Rarity);
                bool big = d.Jackpot;
                Vector3 p;
                float iconAlpha = 1f;
                if (d.T < Arc)
                {
                    float k = d.T / Arc;
                    var g = new Vec2(d.From.X + (d.Land.X - d.From.X) * k, d.From.Y + (d.Land.Y - d.From.Y) * k);
                    p = IsoProjection.ToWorld(g) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.45f, 0f);
                }
                else if (d.T < Arc + d.Lie)
                {
                    p = IsoProjection.ToWorld(d.Land) + new Vector3(0f, 0.04f * Mathf.Sin(d.T * 4f), 0f);
                }
                else if (d.T < Arc + d.Lie + Fly)
                {
                    float k = (d.T - Arc - d.Lie) / Fly;
                    p = Vector3.Lerp(IsoProjection.ToWorld(d.Land), heroWorld, k * k);
                    iconAlpha = 1f - k;
                }
                else { Hide(d); continue; }

                const float px = 1f / 64f;
                p = new Vector3(Mathf.Round(p.x / px) * px, Mathf.Round(p.y / px) * px, 0f);
                d.Icon.transform.localPosition = p;
                d.Icon.color = new Color(1f, 1f, 1f, iconAlpha);

                float lying = d.T < Arc ? d.T / Arc : d.T < Arc + d.Lie ? 1f : 1f - (d.T - Arc - d.Lie) / Fly;
                float pulse = big ? 0.7f + 0.3f * Mathf.Sin(d.T * Mathf.PI * 2f / 1.5f) : 1f; // a slow 1.5 s breath
                d.Beam.transform.localPosition = IsoProjection.ToWorld(d.Land);
                // A legendary's shaft reaches for the top of the screen; a rare's is shorter; the rest barely rise.
                d.Beam.transform.localScale = new Vector3(big ? 2.4f : 1f, (big ? 6.5f : d.Rarity >= Rarity.Rare ? 2.2f : 1.1f) * lying, 1f);
                // The rarity is told a beat after landing: the shaft lands white and takes its colour 0.2 s later.
                float tell = Mathf.Clamp01((d.T - Arc - 0.2f) / 0.12f);
                var shown = Color.Lerp(new Color(1f, 0.97f, 0.9f), col, tell);
                var bc = shown; bc.a = (d.Rarity == Rarity.Common ? 0.25f : 0.6f) * lying * pulse;
                d.Beam.color = bc;
                if (d.Core.enabled)
                {
                    d.Core.transform.localPosition = d.Beam.transform.localPosition;
                    d.Core.transform.localScale = new Vector3(0.5f, 7f * lying, 1f);
                    d.Core.color = new Color(1f, 1f, 1f, 0.55f * lying * tell * pulse);
                }
                // A soft pool of the rarity's colour on the floor (flat 2:1), bigger and brighter for jackpots.
                d.Pool.transform.localPosition = IsoProjection.ToWorld(d.Land);
                d.Pool.transform.localScale = new Vector3(big ? 1.6f : 1f, big ? 0.8f : 0.5f, 1f);
                var pc = shown; pc.a = (d.Rarity == Rarity.Common ? 0.12f : big ? 0.5f : 0.3f) * lying;
                d.Pool.color = pc;

                var namePos = IsoProjection.ToWorld(d.Land) + new Vector3(0f, (big ? 1.4f : 1f) * Mathf.Max(0.3f, lying), 0f);
                if (d.NameMr.enabled)
                {
                    // 재기획 §6.3: names that would overlap stack upward, 2 px apart.
                    var size = d.NameMr.bounds.size;
                    bool moved = true;
                    for (int guard = 0; moved && guard < 8; guard++)
                    {
                        moved = false;
                        foreach (var r in _placed)
                            if (Mathf.Abs(r.x - namePos.x) < (r.z + size.x) / 2f && Mathf.Abs(r.y - namePos.y) < size.y + 2f / 64f)
                            { namePos.y = r.y + size.y + 2f / 64f; moved = true; }
                    }
                    _placed.Add(new Vector3(namePos.x, namePos.y, size.x));
                }
                d.Name.transform.localPosition = namePos;
                var nc = col; nc.a = lying * tell;
                d.Name.color = nc;
                if (d.Plate.enabled)
                {
                    // A dark band under the name, a pixel wider each side (ui/dot is 2 px at PPU 64).
                    var b = d.NameMr.bounds;
                    d.Plate.transform.localPosition = new Vector3(d.Name.transform.localPosition.x, d.Name.transform.localPosition.y + b.size.y / 2f, 0f);
                    d.Plate.transform.localScale = new Vector3((b.size.x + 0.12f) / 0.03125f, (b.size.y + 0.04f) / 0.03125f, 1f);
                    d.Plate.color = new Color(0.02f, 0.015f, 0.025f, 0.7f * lying * tell);
                }
            }
        }
    }
}
