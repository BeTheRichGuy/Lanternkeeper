using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Minimal diorama HUD (GDD §3.1, §17.1): a lantern with an HP ring and an XP ring of pixel dots that
    /// tween instead of snapping, level and floor, three slots for recent drops (filled in M1),
    /// and a boss bar across the top while a boss is engaged. Lives under the camera.
    /// </summary>
    public sealed class HudView
    {
        const int Dots = 20;
        const float Px = 1f / 64f;
        readonly SimWorld _world;
        readonly SpriteRenderer[] _hp = new SpriteRenderer[Dots], _xp = new SpriteRenderer[Dots];
        readonly TextMesh _label, _bossName, _warning, _ignite;
        int _igniteShown = -1;

        TextMesh _subtitle;
        float _subtitleT = 99f;


        /// <summary>GDD §26.6: one line of story when entering a zone, fading after a few seconds.</summary>
        public void ShowZone(int zone, int loop)
        {
            if (_subtitle == null) return;
            var act = EnemyRoster.ForAct(loop);
            var text = act.ZoneLines[Mathf.Clamp(zone, 0, act.ZoneLines.Length - 1)];
            int lap = loop - (EnemyRoster.Acts.Length - 1); // past the last act it repeats
            _subtitle.transform.localPosition = Vector3.zero;
            _subtitleHold = 4.5f;
            _subtitle.text = (zone == 0 ? "막 " + act.Numeral + " · " + act.Name + "\n" : "") + text + (lap > 0 ? "  (" + (lap + 1) + "회차)" : "");
            _subtitleT = 0f;
        }

        void RenderSubtitle(float dt)
        {
            if (_subtitle == null || _subtitleT > _subtitleHold) { if (_subtitle != null && _subtitle.gameObject.activeSelf) _subtitle.gameObject.SetActive(false); return; }
            _subtitleT += dt;
            if (!_subtitle.gameObject.activeSelf) _subtitle.gameObject.SetActive(true);
            float a = Mathf.Clamp01(_subtitleT / 0.6f) * (1f - Mathf.Clamp01((_subtitleT - (_subtitleHold - 1f)) / 1f));
            _subtitle.color = new Color(0.95f, 0.88f, 0.72f, a);
        }

        /// <summary>A persistent one-line warning above the lantern (save problems); null clears it.</summary>
        public void SetWarning(string text)
        {
            _warning.text = text ?? "";
            _warning.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        readonly SpriteRenderer _bossBack, _bossFill, _bossTrail;
        readonly SpriteRenderer _lantern;
        readonly SpriteRenderer _vialFill;
        readonly SpriteMask _vialMask;
        readonly float _vialH;
        readonly SpriteRenderer[] _slotFrame = new SpriteRenderer[3], _slotBack = new SpriteRenderer[3], _slotIcon = new SpriteRenderer[3];
        readonly SpriteCatalog _catalog;
        readonly Item[] _slotShown = new Item[3];
        long _labelKey = -1;
        bool _bossWasHidden;
        float _hpShown = 1f, _xpShown, _bossShown = 1f, _bossTrailShown = 1f, _bossAlpha;
        static readonly Color HpOn = new Color(0.86f, 0.24f, 0.2f), XpOn = new Color(1f, 0.72f, 0.3f), Off = new Color(0.15f, 0.13f, 0.16f, 0.75f);
        const float BossBarWidth = 4.5f;

        public HudView(Transform camera, SimWorld world, SpriteCatalog catalog, Material material, float halfW, float halfH)
        {
            _world = world;
            var font = UiFont.Body;

            var corner = _corner = new GameObject("HUD").transform;
            corner.SetParent(camera, false);

            _lantern = NewSprite(corner, catalog.Get("ui/lantern"), material, Vector3.zero, 3f, 32100);
            // Research top-10 #5: life as lantern oil in a vial (empty glass, liquid masked to the life fraction).
            if (catalog.Has("ui/hud_lantern_vial_empty") && catalog.Has("ui/hud_lantern_vial_fill"))
            {
                _lantern.sprite = catalog.Get("ui/hud_lantern_vial_empty");
                _lantern.transform.localScale = Vector3.one;
                _vialFill = NewSprite(corner, catalog.Get("ui/hud_lantern_vial_fill"), material, Vector3.zero, 1f, 32101);
                _vialFill.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var maskGo = new GameObject("vial mask");
                maskGo.transform.SetParent(corner, false);
                _vialMask = maskGo.AddComponent<SpriteMask>();
                _vialMask.sprite = catalog.Get("ui/dot");
                _vialMask.isCustomRangeActive = true;
                _vialMask.frontSortingOrder = 32102;
                _vialMask.backSortingOrder = 32100;
                _vialH = _lantern.sprite.bounds.size.y;
            }
            for (int i = 0; i < Dots; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / Dots;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                _hp[i] = NewSprite(corner, catalog.Get("ui/dot"), material, Snap(dir * 0.32f), 1f, 32050);
                _xp[i] = NewSprite(corner, catalog.Get("ui/dot"), material, Snap(dir * 0.43f), 1f, 32050);
            }
            _catalog = catalog;
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(0.75f + i * 0.32f, -0.2f);
                _slotFrame[i] = NewSprite(corner, catalog.Get("ui/dot"), material, pos, 10f, 32050);
                _slotBack[i] = NewSprite(corner, catalog.Get("ui/dot"), material, pos, 9f, 32051);
                _slotBack[i].color = new Color(0.08f, 0.07f, 0.1f, 0.85f);
                _slotIcon[i] = NewSprite(corner, null, material, pos, 1f, 32052);
                _slotFrame[i].color = new Color(0.2f, 0.18f, 0.22f, 0.6f);
            }
            _label = NewText(corner, font, new Vector3(0.6f, 0.1f, 0f), TextAnchor.MiddleLeft, 0.05f);
            _warning = NewText(corner, font, new Vector3(-0.3f, 0.55f, 0f), TextAnchor.MiddleLeft, 0.035f);
            _warning.color = new Color(1f, 0.45f, 0.4f);
            _ignite = NewText(corner, font, new Vector3(1.7f, 0.63f, 0f), TextAnchor.MiddleLeft, 0.032f);
            _warning.gameObject.SetActive(false);

            var center = _center = new GameObject("Subtitle").transform;
            center.SetParent(camera, false);
            _subtitle = NewText(center, font, Vector3.zero, TextAnchor.MiddleCenter, 0.05f);
            _subtitle.alignment = TextAlignment.Center;
            _subtitle.gameObject.SetActive(false);
            _event = NewText(center, font, new Vector3(0f, -0.62f, 0f), TextAnchor.UpperCenter, 0.034f);
            _event.richText = true;
            _event.alignment = TextAlignment.Center;
            _event.gameObject.SetActive(false);

            var top = _top = new GameObject("Boss").transform;
            top.SetParent(camera, false);
            _bossBack = NewSprite(top, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32050);
            _bossTrail = NewSprite(top, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32051);
            _bossFill = NewSprite(top, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32052);
            _bossName = NewText(top, font, new Vector3(0f, 0.2f, 0f), TextAnchor.MiddleCenter, 0.05f);
            _bossFrame = UiFrame.Make(top, catalog, material, UiFrame.BossBar, new Vector2(BossBarWidth + 0.5f, 0.375f), Vector3.zero, 32054);

            // Skill ring (spec §5): four slots with cooldown shade, then the resonance gauge and heat under the label.
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(1.85f + i * 0.4f, -0.2f);
                _ringFrame[i] = NewSprite(corner, catalog.Get("ui/dot"), material, pos, 11f, 32050);
                _ringFrame[i].color = new Color(0.35f, 0.3f, 0.26f, 0.8f);
                _ringIcon[i] = NewSprite(corner, null, material, pos, 1f, 32052);
                _ringCd[i] = NewSprite(corner, catalog.Get("ui/dot"), material, pos, 1f, 32053);
                _ringCd[i].color = new Color(0f, 0f, 0f, 0.62f);
                _ringSigil[i] = NewSprite(corner, null, material, pos + new Vector3(0f, -0.3f), 0.75f, 32052);
                // 숙련 (skill level from uses): the level in the slot's lower corner like a stack count (above the slot it ran
                // into the hero's level line), with a dark copy under it to read on any icon; a thin bar to the next level.
                _ringLvShadow[i] = NewText(corner, font, pos + new Vector3(0.175f, -0.165f, 0f), TextAnchor.LowerRight, 0.022f);
                _ringLvShadow[i].color = new Color(0f, 0f, 0f, 0.9f);
                _ringLvShadow[i].GetComponent<MeshRenderer>().sortingOrder = 32055;
                _ringLv[i] = NewText(corner, font, pos + new Vector3(0.165f, -0.155f, 0f), TextAnchor.LowerRight, 0.022f);
                _ringLv[i].GetComponent<MeshRenderer>().sortingOrder = 32056;
                _ringXp[i] = NewSprite(corner, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32054);
            }
            _gaugeBack = NewSprite(corner, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32050);
            _gaugeFill = NewSprite(corner, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32051);
            _heatFill = NewSprite(corner, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32051);
            _preset = NewText(corner, font, new Vector3(1.7f, 0.45f, 0f), TextAnchor.MiddleLeft, 0.03f);
            _preset.color = new Color(0.75f, 0.68f, 0.58f);
            // Hovering a ring slot: what the skill is and how far its practice has come.
            _tipBack = NewSprite(corner, catalog.Get("ui/dot"), material, Vector3.zero, 1f, 32200);
            _tipBack.color = new Color(0.04f, 0.035f, 0.05f, 0.94f);
            _tip = NewText(corner, font, Vector3.zero, TextAnchor.LowerLeft, 0.024f);
            _tip.GetComponent<MeshRenderer>().sortingOrder = 32201;
            _tipBack.enabled = false;
            _tip.gameObject.SetActive(false);
            Layout(halfW, halfH);
        }

        readonly SpriteRenderer[] _ringFrame = new SpriteRenderer[4], _ringIcon = new SpriteRenderer[4], _ringCd = new SpriteRenderer[4];
        readonly SpriteRenderer[] _ringSigil = new SpriteRenderer[4], _ringXp = new SpriteRenderer[4];
        readonly TextMesh[] _ringLv = new TextMesh[4], _ringLvShadow = new TextMesh[4];
        readonly SpriteRenderer _tipBack;
        readonly TextMesh _tip;
        int _tipSlot = -1, _tipShownFor = -1, _tipUses = -1;
        string _tipSkill;

        /// <summary>No pointer over the HUD (summary open, window not focused): drop the tooltip.</summary>
        public void ClearHover() => _tipSlot = -1;

        bool _cornerHidden;

        /// <summary>
        /// The screen HUD (<see cref="HudBar"/>) draws the lantern, skills, drops and level now: the world-space corner goes,
        /// the save warning moves under the zone line. Boss bar, zone lines and event prompts stay here.
        /// </summary>
        public void HideCorner()
        {
            _cornerHidden = true;
            _warning.transform.SetParent(_center, false);
            _warning.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            _warning.anchor = TextAnchor.MiddleCenter;
            _corner.gameObject.SetActive(false);
            _top.gameObject.SetActive(false); // the screen HUD draws the boss bar
        }

        /// <summary>The ring slot under the screen point, or −1 (GameBootstrap passes the mouse each frame).</summary>
        public void HoverRing(Camera cam, Vector2 screen)
        {
            if (_cornerHidden) { _tipSlot = -1; return; }
            var p = WorldPresenter.ScreenToWorld(cam, screen);
            _tipSlot = -1;
            for (int i = 0; i < 4; i++)
            {
                var b = _ringFrame[i].bounds;
                if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y) { _tipSlot = i; break; }
            }
        }

        void RenderTip()
        {
            var h = _world.Hero;
            var s = _tipSlot >= 0 ? h.Ring.Slots[_tipSlot] : null;
            if (s == null)
            {
                if (_tipShownFor != -1) { _tipShownFor = -1; _tipBack.enabled = false; _tip.gameObject.SetActive(false); }
                return;
            }
            h.SkillUses.TryGetValue(s.BaseId, out int uses);
            int lv = SkillBook.SkillLevel(uses);
            // Rebuilt when the slot, its skill (preset swap, evolution) or its use count changes.
            if (_tipShownFor == _tipSlot && _tipSkill == s.Id && _tipUses == uses) return;
            _tipShownFor = _tipSlot;
            _tipSkill = s.Id;
            _tipUses = uses;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(s.Name).Append("</b>  <color=#a8d0ff>숙련 Lv").Append(lv).Append("</color>\n");
            sb.Append("위력 +").Append(((SkillBook.LevelPower(lv) - 1f) * 100f).ToString("0")).Append("%  범위 +")
              .Append(((SkillBook.LevelArea(lv) - 1f) * 100f).ToString("0")).Append("%  분신 ").Append(SkillBook.LevelCopies(lv));
            if (lv < SkillBook.MaxSkillLevel) sb.Append("\n<color=#8a8178>다음 레벨까지 ").Append(SkillBook.UsesFor(lv + 1) - uses).Append("회</color>");
            if (s.Cooldown > 0f) sb.Append("  <color=#8a8178>재사용 ").Append(s.Cooldown.ToString("0.#")).Append("초</color>");
            _tip.text = sb.ToString();
            var at = new Vector3(_ringFrame[_tipSlot].transform.localPosition.x - 0.15f, 0.95f, 0f); // above the two HUD lines
            _tip.transform.localPosition = at;
            _tip.gameObject.SetActive(true);
            var mr = _tip.GetComponent<MeshRenderer>();
            mr.ResetBounds();
            var size = mr.bounds.size / Mathf.Max(1e-4f, _corner.lossyScale.x);
            _tipBack.transform.localPosition = at + new Vector3(size.x / 2f, size.y / 2f, 0f);
            _tipBack.transform.localScale = new Vector3((size.x + 0.16f) / 0.03125f, (size.y + 0.12f) / 0.03125f, 1f);
            _tipBack.enabled = true;
        }
        readonly int[] _lvShown = { -1, -1, -1, -1 };
        readonly string[] _ringShown = new string[4];
        readonly Sigil[] _sigilShown = { (Sigil)(-1), (Sigil)(-1), (Sigil)(-1), (Sigil)(-1) };
        readonly SpriteRenderer _gaugeBack, _gaugeFill, _heatFill;
        readonly TextMesh _preset, _event;
        SimWorld.OfferedEvent _eventShown;
        int _eventSecondsShown = -1;

        /// <summary>GDD §6.2: the offered event, its options (1/2/3) and the seconds left before the default is taken.</summary>
        void RenderEvent()
        {
            var p = _world.PendingEvent;
            // The augment cards hold the centre; the event's line waits behind them (both printed on one spot).
            if (p == null || _world.Hero.AugOffer != null)
            {
                if (_eventShown != null) { _eventShown = null; _event.gameObject.SetActive(false); }
                return;
            }
            int secs = Mathf.CeilToInt((p.DeadlineTick - _world.TickCount) * SimWorld.Dt);
            if (p == _eventShown && secs == _eventSecondsShown) return;
            _eventShown = p;
            _eventSecondsShown = secs;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(p.Title).Append("</b>\n");
            for (int i = 0; i < p.Options.Length; i++)
            {
                bool def = i == p.Default;
                sb.Append(def ? "<color=#ffd38a>" : "<color=#cfc6b8>").Append('[').Append(i + 1).Append("] ").Append(p.Options[i]).Append("</color>");
                sb.Append(i < p.Options.Length - 1 ? "    " : "\n");
            }
            sb.Append("<color=#8f877c>").Append(secs).Append("초 뒤 기본값</color>");
            _event.text = sb.ToString();
            _event.gameObject.SetActive(true);
        }
        int _presetShown = -1;
        float _gaugeShown;

        void RenderRing(float k)
        {
            var h = _world.Hero;
            var ring = h.Ring;
            for (int i = 0; i < 4; i++)
            {
                var s = ring.Slots[i];
                string id = s?.Id;
                if (id != _ringShown[i])
                {
                    _ringShown[i] = id;
                    _ringFrame[i].enabled = true;
                    _ringIcon[i].enabled = s != null;
                    if (s != null)
                    {
                        string key = _catalog.Has("icon/skill_" + s.Id) ? "icon/skill_" + s.Id : "icon/skill_" + s.BaseId;
                        if (_catalog.Has(key))
                        {
                            var icon = _catalog.Get(key);
                            _ringIcon[i].sprite = icon;
                            _ringIcon[i].color = Color.white;
                            float px = Mathf.Max(icon.rect.width, icon.rect.height);
                            _ringIcon[i].transform.localScale = Vector3.one * Mathf.Min(1f, 20f / px);
                        }
                        else
                        {
                            // No icon yet: an element-coloured gem stands in.
                            _ringIcon[i].sprite = _catalog.Get("ui/dot");
                            _ringIcon[i].color = FxView.ElementColor(s.Elem);
                            _ringIcon[i].transform.localScale = Vector3.one * 7f;
                        }
                        _ringFrame[i].color = s.IsEvolution ? new Color(1f, 0.62f, 0.25f, 0.95f) : new Color(0.35f, 0.3f, 0.26f, 0.8f);
                    }
                }
                // The glyph socketed in this slot (spec §6), small under the skill.
                if (ring.Sigils[i] != _sigilShown[i])
                {
                    _sigilShown[i] = ring.Sigils[i];
                    string sk = "icon/sigil_" + ring.Sigils[i].ToString().ToLowerInvariant();
                    _ringSigil[i].enabled = ring.Sigils[i] != Sigil.None && _catalog.Has(sk);
                    if (_ringSigil[i].enabled) _ringSigil[i].sprite = _catalog.Get(sk);
                }
                int uses = s != null && h.SkillUses.TryGetValue(s.BaseId, out int u) ? u : 0;
                int lv = s == null ? 0 : SkillBook.SkillLevel(uses);
                if (lv != _lvShown[i])
                {
                    _lvShown[i] = lv;
                    _ringLv[i].text = _ringLvShadow[i].text = lv > 0 ? lv.ToString() : "";
                    // Every CopyEvery-th level the skill gains a copy: gold from the first one.
                    _ringLv[i].color = lv >= SkillBook.CopyEvery ? new Color(1f, 0.82f, 0.4f) : new Color(0.85f, 0.9f, 1f);
                }
                float next = lv <= 0 || lv >= SkillBook.MaxSkillLevel ? 1f
                    : (uses - SkillBook.UsesFor(lv)) / (float)Mathf.Max(1, SkillBook.UsesFor(lv + 1) - SkillBook.UsesFor(lv));
                Bar(_ringXp[i], 1.85f + i * 0.4f - 0.16f, -0.385f, 0.32f, s == null ? 0f : next, new Color(0.6f, 0.8f, 1f, 0.85f), 0.45f);
                float cd = s == null || s.Cooldown <= 0f ? 0f : Mathf.Clamp01((float)ring.CooldownLeft(i, _world.Time) / s.Cooldown);
                // Shade drains from the top as the skill comes back (10 px square at most).
                _ringCd[i].enabled = cd > 0.001f;
                _ringCd[i].transform.localScale = new Vector3(10f, 10f * cd, 1f);
                _ringCd[i].transform.localPosition = new Vector3(1.85f + i * 0.4f, -0.2f - (1f - cd) * 0.156f);
            }

            // Resonance gauge: coloured by the element the next grand resonance will strike with.
            _gaugeShown = Mathf.Lerp(_gaugeShown, h.Gauge / SimWorld.GaugeMax, k);
            float g = Mathf.Clamp01(_gaugeShown);
            const float W = 1.5f;
            var full = g >= 0.999f ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 8f)) : 1f;
            Bar(_gaugeBack, 1.7f, 0.3f, W, 1f, new Color(0.08f, 0.06f, 0.09f, 0.85f), 2f);
            Bar(_gaugeFill, 1.7f, 0.3f, W, g, FxView.ElementColor(Elements.Next(h.LastResonanceMark)) * full, 1.5f);
            // Class resource: 열기 (ember orange) or 맹세 (oath gold).
            var res = h.ClassId == "oathknight" ? new Color(1f, 0.85f, 0.4f, 0.9f) : h.ClassId == "windhunter" ? new Color(0.55f, 0.95f, 0.75f, 0.9f)
                : h.ClassId == "starseer" ? new Color(0.65f, 0.6f, 1f, 0.9f) : new Color(1f, 0.45f, 0.15f, 0.9f);
            Bar(_heatFill, 1.7f, 0.24f, W, h.Heat / SimWorld.HeatMax, res, 0.75f);

            int headKey = (h.UseCustomRing ? 2000000 : 0) + (Talents.PointsAt(h.IntegratedLevel) > 0 ? 1000000 : 0) + (h.ClassId == "oathknight" ? 100000 : h.ClassId == "windhunter" ? 200000 : h.ClassId == "starseer" ? 300000 : 0) + h.Preset * 1000 + (int)_world.Order * 100 + (_world.FloorLock ? 10 : 0) + (_world.CodexFirst ? 20 : 0) + (_world.TopLoop ? 40 : 0) + _world.CartographerLevel * 2 + (_world.CampUnlocked ? 1 : 0) + (int)_world.Stance * 10000000;
            if (headKey != _presetShown)
            {
                _presetShown = headKey;
                // 2026-11-21 재기획 §5.1: no standing key hints, and no "탤런트 포인트 있음" (talents spend themselves, so it
                // was always on). The line only names an idle switch that changes where the hero hunts.
                _preset.text = _world.FloorLock ? "층 고정" : _world.CodexFirst ? "도감 우선" : _world.TopLoop ? "최상층 반복" : "";
            }
        }

        static void Bar(SpriteRenderer sr, float x, float y, float width, float frac, Color c, float thick)
        {
            float w = width * Mathf.Clamp01(frac);
            sr.enabled = w > 0.001f;
            sr.transform.localScale = new Vector3(w / 0.03125f, thick, 1f);
            sr.transform.localPosition = new Vector3(x + w / 2f, y, 0f);
            sr.color = c;
        }

        readonly Transform _corner, _center, _top;
        readonly SpriteRenderer _bossFrame;

        /// <summary>
        /// Pin the HUD to the view's edges; called again whenever the window or the world zoom changes.
        /// <paramref name="scale"/> keeps the HUD a steady size on screen (and a whole number of screen pixels per
        /// art pixel) while the world zooms in and out underneath it.
        /// </summary>
        /// <summary>Hidden behind full-screen choices (first-launch class selection).</summary>
        public void SetVisible(bool v)
        {
            _corner.gameObject.SetActive(v && !_cornerHidden);
            _warning.transform.parent.gameObject.SetActive(v);
            _center.gameObject.SetActive(v);
            _top.gameObject.SetActive(v && !_cornerHidden);
        }

        public void Layout(float halfW, float halfH, float scale = 1f)
        {
            var s = Vector3.one * scale;
            _corner.localScale = _center.localScale = _top.localScale = s;
            _corner.localPosition = Snap(new Vector3(-halfW + 0.55f * scale, -halfH + 0.55f * scale)) + new Vector3(0f, 0f, 10f);
            _center.localPosition = Snap(new Vector3(0f, halfH - 1.1f * scale)) + new Vector3(0f, 0f, 10f);
            _top.localPosition = Snap(new Vector3(0f, halfH - 0.35f * scale)) + new Vector3(0f, 0f, 10f);
        }

        static Vector3 Snap(Vector3 v) => new Vector3(Mathf.Round(v.x / Px) * Px, Mathf.Round(v.y / Px) * Px, 0f);

        static SpriteRenderer NewSprite(Transform parent, Sprite s, Material m, Vector3 pos, float scale, int order)
        {
            var sr = new GameObject("hud").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * scale;
            sr.sharedMaterial = m;
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>GDD §5.3: the lantern tells when it can be kindled (click it or press Space), burns, or recharges.</summary>
        void RenderIgnite()
        {
            bool on = _world.Ignited;
            float cd = _world.IgniteCooldownLeft;
            int secs = on ? Mathf.CeilToInt(_world.IgniteLeft) : Mathf.CeilToInt(cd);
            int key = (on ? 100000 : cd > 0f ? 0 : 200000) + secs;
            if (key != _igniteShown)
            {
                _igniteShown = key;
                _ignite.text = on ? $"등불 점화  {secs}초" : cd > 0f ? $"점화 {secs / 60}:{secs % 60:00}" : "점화 가능";
            }
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            _ignite.color = on ? new Color(1f, 0.9f, 0.6f) : cd > 0f ? new Color(0.5f, 0.46f, 0.42f) : new Color(1f, 0.78f * pulse + 0.1f, 0.4f * pulse, 1f);
            if (on) _lantern.color = Color.Lerp(_lantern.color, new Color(1f, 1f, 0.85f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) * 0.5f);
        }

        /// <summary>True when a screen point lands on the lantern vial (the click that kindles it).</summary>
        public bool LanternHit(Camera cam, Vector2 screen)
        {
            if (_cornerHidden) return false;
            var p = WorldPresenter.ScreenToWorld(cam, screen);
            var c = _lantern.transform.position;
            float r = 0.45f * _corner.lossyScale.x;
            return (new Vector2(p.x - c.x, p.y - c.y)).sqrMagnitude <= r * r;
        }

        static TextMesh NewText(Transform parent, Font font, Vector3 pos, TextAnchor anchor, float size)
        {
            var go = new GameObject("text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = anchor;
            tm.color = new Color(0.95f, 0.88f, 0.75f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 32100;
            return tm;
        }

        public void Render(float dt)
        {
            var h = _world.Hero;
            // Numbers animate, never snap (craft floor): ease toward the real value.
            float k = 1f - Mathf.Exp(-dt / 0.12f);
            _hpShown = Mathf.Lerp(_hpShown, Mathf.Clamp01(h.Stats.Hp / h.Stats.MaxHp), k);
            float xpTarget = Mathf.Clamp01((float)h.Xp / h.XpToNext);
            _xpShown = xpTarget < _xpShown - 0.5f ? xpTarget : Mathf.Lerp(_xpShown, xpTarget, k); // level-up wraps instantly
            int hpOn = Mathf.CeilToInt(Dots * _hpShown - 0.01f);
            int xpOn = Mathf.FloorToInt(Dots * _xpShown);
            float low = _hpShown < 0.3f ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
            for (int i = 0; i < Dots; i++)
            {
                _hp[i].color = i < hpOn ? HpOn * new Color(low, low, low, 1f) : Off;
                _xp[i].color = i < xpOn ? XpOn : Off;
            }
            if (_vialMask != null)
            {
                // The oil level: a 2-px mask stretched over the glass from the bottom up to the life fraction.
                float hgt = _vialH * Mathf.Clamp01(_hpShown);
                _vialMask.transform.localScale = new Vector3(_lantern.sprite.bounds.size.x / 0.03125f, Mathf.Max(0.001f, hgt / 0.03125f), 1f);
                _vialMask.transform.localPosition = new Vector3(0f, -_vialH / 2f + hgt / 2f, 0f);
                _vialFill.color = _hpShown < 0.3f ? new Color(1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 6f)), 0.5f, 1f) : Color.white;
            }
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.1f);
            _lantern.color = new Color(1f, 0.9f * flicker + 0.1f, 0.75f * flicker + 0.2f, 1f);
            RenderIgnite();

            long key = h.Level * 10000L + _world.FloorIndex + h.EmberLevel * 1000000L + (h.Level >= SimWorld.LevelCap ? _world.PendingEmber * 100000000L : 0L) + h.WickLevel * 10000000000L;
            if (key != _labelKey)
            {
                _labelKey = key; // rebuild the text mesh only when it changes
                string act = "막 " + _world.ActNow.Numeral;
                string where = _world.IsActBossFloor ? act + " · 보스" : act + " · " + (_world.Zone + 1) + "-" + (_world.FloorInZone + 1);
                int lap = _world.ActLoop - (EnemyRoster.Acts.Length - 1);
                if (lap > 0) where += " ·" + (lap + 1) + "회차";
                // GDD §11.5: "Lv 60 (+12)" — ember on, and grey ember waiting for the next rebirth.
                string lv = "Lv " + h.Level + (h.EmberLevel > 0 ? " <color=#ffb060>+" + h.EmberLevel + "</color>" : "");
                if (h.Level >= SimWorld.LevelCap && _world.PendingEmber > 0) lv += " <color=#8a8178>(+" + _world.PendingEmber + ")</color>";
                if (h.WickLevel > 0) lv += " <color=#ffe7a0>심지 " + h.WickLevel + "</color>";
                _label.text = lv + "   " + where + (_world.FastDescent ? "  <color=#9fd28a>고속 진행 ×" + _world.RepeatMul + "</color>" : "");
            }

            RenderBoss(dt, k);
            if (!_cornerHidden)
            {
                RenderRecentDrops();
                RenderRing(k);
                RenderTip();
            }
            RenderEvent();
            RenderSubtitle(dt);
        }

        void RenderRecentDrops()
        {
            for (int i = 0; i < 3; i++)
            {
                var item = i < _world.RecentDrops.Count ? _world.RecentDrops[i] : null;
                if (item == _slotShown[i]) continue;
                _slotShown[i] = item;
                _slotIcon[i].enabled = item != null;
                if (item == null) { _slotFrame[i].color = new Color(0.2f, 0.18f, 0.22f, 0.6f); continue; }
                var icon = _catalog.Get(ItemStyle.Icon(item));
                _slotIcon[i].sprite = icon;
                // Fit any icon size into the 18 px slot.
                float px = Mathf.Max(icon.rect.width, icon.rect.height);
                _slotIcon[i].transform.localScale = Vector3.one * Mathf.Min(1f, 16f / px);
                _slotFrame[i].color = ItemStyle.Color(item.Rarity);
            }
        }

        int _introFor = -1;
        float _subtitleHold = 4.5f;

        void RenderBoss(float dt, float k)
        {
            Enemy boss = null;
            foreach (var e in _world.Enemies) if (e.Boss && e.Aggro && e.Alive) { boss = e; break; }
            _bossAlpha = Mathf.MoveTowards(_bossAlpha, boss != null ? 1f : 0f, dt / 0.3f);
            if (boss != null)
            {
                float frac = Mathf.Clamp01(boss.Stats.Hp / boss.Stats.MaxHp);
                _bossShown = Mathf.Lerp(_bossShown, frac, k);
                _bossTrailShown = Mathf.MoveTowards(_bossTrailShown, frac, dt * 0.35f); // damage trail
                _bossName.text = string.IsNullOrEmpty(boss.Def.Name) ? "보스" : boss.Def.Name;
                // 막 보스 등장: once per boss, its name and who it was (GDD §26.4) across the middle of the screen.
                if (_world.IsActBossFloor && boss.Id != _introFor && _subtitle != null)
                {
                    _introFor = boss.Id;
                    _subtitle.transform.localPosition = new Vector3(0f, -1.7f, 0f); // under the boss bar
                    _subtitleHold = 6f;
                    _subtitle.text = "<b>" + boss.Def.Name + "</b>" + (string.IsNullOrEmpty(boss.Def.Lore) ? "" : "\n<size=34><i>" + boss.Def.Lore + "</i></size>");
                    _subtitleT = 0f;
                }
            }
            else if (_bossAlpha <= 0f) { _bossShown = _bossTrailShown = 1f; }

            float a = _bossAlpha;
            if (a <= 0f && _bossWasHidden) return; // nothing to update while no boss is engaged
            _bossWasHidden = a <= 0f;
            SetBar(_bossBack, 1f, new Color(0.08f, 0.06f, 0.08f, 0.8f * a), 1.2f);
            SetBar(_bossTrail, _bossTrailShown, new Color(0.95f, 0.85f, 0.6f, 0.8f * a), 1f);
            SetBar(_bossFill, _bossShown, new Color(0.7f, 0.12f, 0.12f, a), 1f);
            var c = _bossName.color; c.a = a; _bossName.color = c;
            if (_bossFrame != null) { _bossFrame.enabled = a > 0.001f; _bossFrame.color = new Color(1f, 1f, 1f, a); }
        }

        static void SetBar(SpriteRenderer sr, float frac, Color c, float thick)
        {
            // ui/dot is 2 px at PPU 64 (0.03125 u): scale to a bar of BossBarWidth × 4 px, left-aligned.
            float w = BossBarWidth * frac;
            sr.transform.localScale = new Vector3(w / 0.03125f, 2f * thick, 1f);
            sr.transform.localPosition = new Vector3(-BossBarWidth / 2f + w / 2f, 0f, 0f);
            sr.color = c;
            sr.enabled = c.a > 0.001f && w > 0.001f;
        }
    }
}
