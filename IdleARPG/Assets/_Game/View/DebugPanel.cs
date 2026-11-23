using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// F1 테스트 패널 (arpg-ui-ux-patterns.md #15): live numbers (DPS, act, floor, enemies, rates, FPS) and shortcuts to
    /// check content fast — jump to any act, levels, legendaries, skill mastery. Changes go into the save like play does.
    /// </summary>
    public sealed class DebugPanel
    {
        readonly Transform _root;
        readonly TextMesh _text;
        readonly SimWorld _world;
        float _fps, _refresh;
        public bool Visible { get; private set; }
        /// <summary>Set by GameBootstrap's late preview: saving is off for the rest of this run.</summary>
        public bool Previewing;

        /// <summary>Test builds only: its shortcuts grant levels and legendaries into the save. Off before release.</summary>
        public const bool Enabled = true;

        public DebugPanel(Transform camera, SimWorld world, SpriteCatalog catalog, Material material)
        {
            _world = world;
            _root = new GameObject("Debug Panel").transform;
            _root.SetParent(camera, false);
            _root.localPosition = new Vector3(-Diorama.HalfWidth * 0.55f, Diorama.HalfHeight * 0.1f, 8.3f);

            var back = new GameObject("back").AddComponent<SpriteRenderer>();
            back.transform.SetParent(_root, false);
            back.sharedMaterial = material;
            back.sprite = catalog.Get("ui/dot");
            back.transform.localScale = new Vector3(Diorama.HalfWidth * 0.82f / 0.03125f, Diorama.HalfHeight * 0.72f / 0.03125f, 1f);
            back.transform.localPosition = new Vector3(0f, Diorama.HalfHeight * 0.24f, 0f); // fits the eleven lines
            back.color = new Color(0.04f, 0.05f, 0.06f, 0.9f);
            back.sortingOrder = 32600;
            UiFrame.Apply(back, catalog);

            var font = UiFont.Body;
            var go = new GameObject("text");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(-Diorama.HalfWidth * 0.38f, Diorama.HalfHeight * 0.55f, 0f);
            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = 48;
            _text.characterSize = 0.026f;
            _text.lineSpacing = 1.15f;
            _text.anchor = TextAnchor.UpperLeft;
            _text.richText = true;
            _text.color = new Color(0.85f, 0.92f, 0.95f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 32610;
            Show(false);
        }

        public void SetScale(float scale) => _root.localScale = Vector3.one * scale;
        public void Toggle() => Show(!Visible);

        public void Show(bool v)
        {
            Visible = v;
            _root.gameObject.SetActive(v);
            _refresh = 0f;
        }

        /// <summary>Digit actions: 1–6 act start, 7 levels +5, 8 three legendaries, 9 ring mastery +5, 0 this act's boss.</summary>
        public void Act(int digit)
        {
            var h = _world.Hero;
            switch (digit)
            {
                case 1: case 2: case 3: case 4: case 5: case 6:
                    _world.GoToFloor(SimWorld.ActLength * (digit - 1));
                    break;
                case 7:
                    for (int i = 0; i < 5 && h.Level < SimWorld.LevelCap; i++) _world.GrantXpForTest(Mathf.Max(1, h.XpToNext - h.Xp));
                    break;
                case 8:
                    for (int i = 0; i < 3; i++) _world.DropItem(h.Pos, Rarity.Legendary);
                    break;
                case 9:
                    foreach (var s in h.Ring.Slots)
                    {
                        if (s == null) continue;
                        h.SkillUses.TryGetValue(s.BaseId, out int u);
                        h.SkillUses[s.BaseId] = SkillBook.UsesFor(Mathf.Min(SkillBook.MaxSkillLevel, SkillBook.SkillLevel(u) + 5));
                    }
                    break;
                case 0:
                    _world.GoToFloor(SimWorld.ActLength * _world.ActLoop + SimWorld.ActBossFloorInAct);
                    break;
            }
            _refresh = 0f;
        }

        public void Render(float dt)
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(1e-4f, dt), 0.05f);
            if (!Visible || (_refresh -= dt) > 0f) return;
            _refresh = 0.5f;
            var h = _world.Hero;
            int alive = 0;
            foreach (var e in _world.Enemies) if (e.Alive) alive++;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>테스트 패널</b>  ").Append(Previewing
                ? "<color=#ffd38a>후반 미리보기 중 · 저장하지 않음</color>\n\n"
                : "<color=#8a8178>F1 닫기 · 바꾼 것은 세이브에 남습니다</color>\n\n");
            sb.Append("막 ").Append(_world.ActNow.Numeral).Append(' ').Append(_world.ActNow.Name).Append("   층 ").Append(_world.FloorIndex)
              .Append("   지역 Lv").Append(_world.AreaLevel).Append('\n');
            sb.Append("살아 있는 적 ").Append(alive).Append("   분당 처치 ").Append(_world.KillsPerMinute.ToString("0"))
              .Append("   분당 경험치 ").Append(_world.XpPerMinute.ToString("0")).Append('\n');
            sb.Append("초당 피해(추정) ").Append(HeroStats.Dps(h).ToString("0")).Append("   생명 ").Append(h.Stats.Hp.ToString("0")).Append('/').Append(h.Stats.MaxHp.ToString("0"))
              .Append("   FPS ").Append(_fps.ToString("0")).Append('\n');
            sb.Append("범위 +").Append((h.Derived.AreaPct * 100f).ToString("0")).Append("%   사거리 +").Append((h.Derived.RangePct * 100f).ToString("0"))
              .Append("%   분신 +").Append(h.Derived.Projectiles).Append("   심지 ").Append(h.WickLevel).Append("\n\n");
            sb.Append("<color=#ffd38a>[1]~[6]</color>  막 I~VI 처음으로\n");
            sb.Append("<color=#ffd38a>[0]</color>  이 막의 보스 층으로\n");
            sb.Append("<color=#ffd38a>[7]</color>  레벨 +5\n");
            sb.Append("<color=#ffd38a>[8]</color>  전설 장비 3개\n");
            sb.Append("<color=#ffd38a>[9]</color>  고리 스킬 숙련 +5\n");
            sb.Append("<color=#ffd38a>[P]</color>  후반 미리보기 (저장 안 함, 다시 켜면 원래대로)");
            _text.text = sb.ToString();
        }
    }
}
