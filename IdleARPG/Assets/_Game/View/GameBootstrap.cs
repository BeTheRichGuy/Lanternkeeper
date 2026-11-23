using System;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Scene entry point: loads the save (and applies offline progress), owns the SimWorld, drives 10 Hz ticks,
    /// keeps the views in step, and autosaves (GDD §13, §19).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] SpriteCatalog catalog;
        [SerializeField] Material spriteMaterial;
        [Tooltip("Sprite-Lit-Default: world sprites react to the 2D lights.")]
        [SerializeField] Material litMaterial;
        [SerializeField] int seed = 1;
        [SerializeField, Range(0.25f, 8f)] float simSpeed = 1f;
        [SerializeField] Camera cam;
        [SerializeField] bool useSaves = true;

        const float AutosaveSeconds = 60f, BackupSeconds = 20f * 60f;

        public SimWorld World { get; private set; }
        readonly SimClock _clock = new SimClock();
        WorldView _view;
        HudView _hud;
        UiKit _ui;
        GearPanel _gear;
        CharacterPanel _character;
        CampPanel _camp;
        SkillTreePanel _talents; // 기억의 나무 (11-21) took the talent list's place: T and the menu open it
        CodexPanel _codex;
        RingPanel _ringPanel;
        ClassSelectPanel _classSelect;
        FirstTips _tips;
        EventPrompt _eventPrompt;
        LootReveal _reveal;
        bool _hudHiddenForClass;
        bool _firstRun;
        OfflinePanel _offline;
        AugmentPanel _augment;
        HuntPanel _hunt;
        LootBanner _banner;
        SaveSystem _saves;
        float _saveTimer, _backupTimer = BackupSeconds; // first save of a session rotates a backup (review 1-1)
        bool _saveSoon;
        Vector3 _camVel;

        void Awake()
        {
            // Pace frames by the display, not a software timer: targetFrameRate alone causes micro-stutter.
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            if (cam == null) cam = Camera.main;
            if (catalog == null) catalog = ScriptableObject.CreateInstance<SpriteCatalog>();

            OfflineReport report = null;
            string warning = null;
            if (useSaves)
            {
                try
                {
                    _classId = ReadActiveClass();
                    _saves = new SaveSystem(ClassDir(_classId));
                    var data = _saves.Load(out var source, out var result);
                    if (data != null)
                    {
                        World = SimWorld.FromSave(data);
                        double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - data.SavedAtUnix;
                        // GDD §7.1: idle hunting is one character at a time — the one you switched away from did not hunt.
                        report = _switchedClass ? null : World.ApplyOffline(away);
                        _switchedClass = false;
                        Debug.Log($"[Lanternkeeper] loaded {source}: Lv {World.Hero.Level}, floor {World.FloorIndex}, away {away:0}s");
                    }
                    else if (result == SaveSystem.LoadResult.NoSave && !System.IO.File.Exists(ActiveFile)) _firstRun = true; // GDD §7.1: choose a class
                    else if (result == SaveSystem.LoadResult.AllUnreadable)
                    {
                        // Never let a fresh game overwrite progress that only failed to load (review 1-1).
                        var kept = _saves.Quarantine(DateTime.UtcNow);
                        warning = "세이브 데이터를 불러오지 못해 별도 보관 후 새로 시작했습니다";
                        Debug.LogWarning("[Lanternkeeper] no readable save; originals moved to " + kept);
                    }
                }
                catch (Exception e)
                {
                    _saves = null; // saving disabled this session rather than risking an overwrite
                    warning = "세이브 폴더에 접근할 수 없어 이번 진행은 저장되지 않습니다";
                    Debug.LogWarning("[Lanternkeeper] save system unavailable: " + e.Message);
                }
            }
            World ??= new SimWorld(seed, _classId);
            if (_saves != null) AdoptAccountWick();

            _view = new WorldView(transform, World, catalog, spriteMaterial, litMaterial);
            _hud = new HudView(cam.transform, World, catalog, spriteMaterial, Diorama.HalfWidth, Diorama.HalfHeight);
            if (cam.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            _audio = new AudioView(cam.transform, World);
            _minimap = new MinimapView(cam.transform, World, catalog, spriteMaterial);
            _menu = new MenuBar(cam.transform, catalog, spriteMaterial);
            _debug = new DebugPanel(cam.transform, World, catalog, spriteMaterial);
            _cutIn = new UltimateCutIn(cam.transform, catalog, spriteMaterial);
            _fade = new ScreenFade(cam.transform, catalog, spriteMaterial);
            _audio.SetAct(Mathf.Min(World.ActLoop, EnemyRoster.Acts.Length - 1));
            ItemStyle.HasSprite = catalog.Has;
            _ui = new UiKit(catalog); // screen-space GUI (2026-11-21 재기획 §5.1); panels move onto it one by one
            _toasts = new ToastView(_ui);
            UiKit.Clicked = () => _audio.Ui("ui_click");
            _settings = new SettingsPanel(_ui) { OnPick = ChangeSetting, OnCharacters = OpenCharacters };
            _help = new HelpPanel(_ui);
            _journal = new JournalPanel(_ui);
            _mailbox = new MailboxPanel(_ui, World);
            _gear = new GearPanel(_ui, World);
            _character = new CharacterPanel(_ui, World);
            _camp = new CampPanel(_ui, World) { OnChanged = () => _saveSoon = true };
            _talents = new SkillTreePanel(_ui, World);
            _talents.OnChanged = () => _saveSoon = true;
            _codex = new CodexPanel(_ui, World);
            _ringPanel = new RingPanel(_ui, World);
            _gear.OnChanged = () => _saveSoon = true;
            _gear.OnSalvaged = _toasts.Salvaged;
            _gear.OnNotice = _toasts.Tip;
            _ringPanel.OnChanged = () => _saveSoon = true;
            // 재기획 §5.8: the screen HUD (lantern vial, skill bar, icon menu) replaces the world-space corner and menu.
            _hudBar = new HudBar(_ui, World) { OpenMenu = OpenFromMenu };
            _hud.HideCorner();
            _menu.Hidden = true;
            _classSelect = new ClassSelectPanel(_ui) { OnPick = ChooseClass };
            if (_firstRun) { _classSelect.Open(null, CharacterSummary, false); _hud.SetVisible(false); _hudBar.Visible = false; _minimap.Hide(true); }
            else if (World.Kills == 0 && World.Hero.Level == 1)
            {
                // A brand-new hero: three short tips, then the hunt speaks for itself.
                _toasts.Tip("등불지기는 자동으로 사냥합니다. 화면을 지켜보기만 해도 됩니다.");
                _toasts.Tip("I로 소지품 · R로 스킬 배치 · T로 스킬 트리");
                _toasts.Tip("Space 또는 등불 클릭: 점화 (피해 +15%)   Esc: 설정");
            }
            _offline = new OfflinePanel(_ui) { OnChanged = () => _saveSoon = true };
            _augment = new AugmentPanel(_ui, id => _audio.Sfx(id)) { OnChanged = () => _saveSoon = true };
            _hunt = new HuntPanel(_ui);
            _banner = new LootBanner(_ui);
            _tips = new FirstTips(_ui); // UX plan D4: first-time lines
            _eventPrompt = new EventPrompt(_ui); // UX plan D7: the event's choices as a box over the windows
            _reveal = new LootReveal(_ui); // 11-24: a legendary or unique find gets its own card
            _reveal.OnOpenBag = () => { if (!_gear.Visible) OpenFromMenu(0); };
            _reveal.OnEquipped = () => _saveSoon = true;
            if (report != null && !report.Empty)
            {
                _offline.Show(report, World);
                Save(rotateNow: true); // lock in the offline reward at once; the pre-offline state becomes backup 1
            }
            if (warning != null) _hud.SetWarning(warning);
            _hud.ShowZone(World.Zone, World.ActLoop);
            _view.OnTick();

            _ppc = cam.GetComponent<PixelPerfectCamera>();
            _presenter = new WorldPresenter(cam, _ppc);
            _view.Director.Ripple = _presenter.Ripple;
            _view.Director.ImpactFrame = _presenter.Impact;
            _view.Director.ZoomKick = _presenter.Kick;
            _zoom = GrowthZoom();
            SetupPostProcessing();

        }

        /// <summary>F11 / Alt+Enter: borderless fullscreen at the display's resolution, and back to the last window size.</summary>
        // ---------- characters (GDD §7.1: one per class, each with its own save) ----------

        string _classId = "ashknight";
        static bool _switchedClass;

        static string ActiveFile => System.IO.Path.Combine(SaveSystem.DefaultDir, "active_class.txt");

        /// <summary>GDD §11.5 / §12.5: the 심지 belongs to the account — every class burns the same wick ("level xp").</summary>
        static string WickFile => System.IO.Path.Combine(SaveSystem.DefaultDir, "account_wick.txt");

        void AdoptAccountWick()
        {
            try
            {
                if (!System.IO.File.Exists(WickFile)) return;
                var parts = System.IO.File.ReadAllText(WickFile).Trim().Split(' ');
                if (parts.Length < 2 || !int.TryParse(parts[0], out int level) || !long.TryParse(parts[1], out long xp)) return;
                var h = World.Hero;
                if (level < h.WickLevel || (level == h.WickLevel && xp <= h.WickXp)) return;
                h.WickLevel = level;
                h.WickXp = xp;
                HeroStats.Recompute(h);
            }
            catch (Exception e) { Debug.LogWarning("[Lanternkeeper] could not read the account wick: " + e.Message); }
        }

        void StoreAccountWick()
        {
            var h = World.Hero;
            if (h.WickLevel <= 0 && h.WickXp <= 0) return;
            try { System.IO.File.WriteAllText(WickFile, h.WickLevel + " " + h.WickXp); }
            catch (Exception e) { Debug.LogWarning("[Lanternkeeper] could not write the account wick: " + e.Message); }
        }

        /// <summary>The Ashknight keeps the original folder so saves from before classes still load.</summary>
        static string ClassDir(string classId) =>
            classId == "ashknight" ? SaveSystem.DefaultDir : System.IO.Path.Combine(SaveSystem.DefaultDir, classId);

        static string ReadActiveClass()
        {
            try
            {
                if (System.IO.File.Exists(ActiveFile))
                {
                    var id = System.IO.File.ReadAllText(ActiveFile).Trim();
                    if (Array.IndexOf(SkillBook.PlayableClasses, id) >= 0) return id;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Lanternkeeper] active class unreadable: " + e.Message); }
            return "ashknight";
        }

        /// <summary>설정 → 캐릭터 선택: the character screen over the paused hunt.</summary>
        void OpenCharacters()
        {
            if (_settings.Visible) _settings.Toggle();
            _classSelect.Open(World.Hero.ClassId, CharacterSummary, true);
        }

        /// <summary>"Lv 23 · 3막 5층" for a class that has a character, null for none (read from its own save).</summary>
        string CharacterSummary(string classId)
        {
            if (World != null && classId == World.Hero.ClassId && !_firstRun) return Summary(World.Hero.Level, World.FloorIndex);
            try
            {
                var path = System.IO.Path.Combine(ClassDir(classId), SaveSystem.MainFile);
                if (!System.IO.File.Exists(path) || !SaveSystem.TryDecode(System.IO.File.ReadAllText(path), out var d) || d == null) return null;
                if (d.ClassId != null && d.ClassId != classId) return null;
                return Summary(d.Level, d.FloorIndex);
            }
            catch (Exception e) { Debug.LogWarning("[Lanternkeeper] could not read a character: " + e.Message); return null; }
        }

        static string Summary(int level, int floorIndex) =>
            "Lv " + level + " · 막 " + EnemyRoster.ForAct(floorIndex / SimWorld.ActLength).Numeral + " " + (floorIndex % SimWorld.ActLength + 1) + "/" + SimWorld.ActLength + "층"; // as the HUD says it, short enough for one card line (loop 4)

        void ChooseClass(string classId)
        {
            // From the game: keep this character's progress before another one loads (the first launch has none yet).
            if (!_firstRun && classId != World.Hero.ClassId && useSaves && _saves != null) Save(rotateNow: false);
            try { System.IO.File.WriteAllText(ActiveFile, classId); }
            catch (Exception e) { Debug.LogWarning("[Lanternkeeper] could not remember the class: " + e.Message); }
            _classSelect.Show(false);
            _hud.SetVisible(true);
            _hudBar.Visible = true;
            _minimap.Hide(false);
            if (classId == World.Hero.ClassId) return;
            _switchedClass = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>원정 명령 (지도 제작자, O) in words.</summary>
        static string OrderName(ExpeditionOrder o) =>
            o == ExpeditionOrder.BossRush ? "보스 직행" : o == ExpeditionOrder.TreasureHunt ? "보물 찾기" : o == ExpeditionOrder.XpFirst ? "경험치 우선" : "모두 처치";

        void ToggleFullscreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                _windowed = new Vector2Int(Screen.width, Screen.height);
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                var w = _windowed.x > 0 ? _windowed : new Vector2Int(Diorama.WindowWidth, Diorama.WindowHeight);
                Screen.SetResolution(w.x, w.y, FullScreenMode.Windowed);
            }
        }

        Vector2Int _windowed;
        Vector3 _laidOutFor;

        // ---------- zoom (희승, 2026-11-19) ----------
        // The world zoom never follows the screen size: a bigger window or monitor shows more of the map.
        // It follows progress instead — close on the hero early on, pulled back once fights fill the screen
        // with monsters and spells. The mouse wheel overrides it for the session.
        PixelPerfectCamera _ppc;
        float _zoom = 2f, _zoomMul = 1f;
        WorldPresenter _presenter;
        Vector2Int _zoomScreen;

        /// <summary>
        /// 2026-11-21 재기획 §2: one whole step per 540 screen pixels (1080p ×2, 2160p ×4), whatever the progress.
        /// Zooming out to ×1 at Lv10 left 80% of a 1080p screen black and the hero 2–3% of a 4K screen tall.
        /// </summary>
        int AutoZoom() => Mathf.Clamp(Screen.height / 540, 1, 4);

        /// <summary>
        /// 희승 11-22 성장 줌: a new hero starts at the whole-step view (1080p ×2); as it levels and its skills and stride
        /// grow the view eases out a little further (×0.85 of it). A rebirth starts close again.
        /// </summary>
        float GrowthZoom()
        {
            var h = World.Hero;
            float lv = Mathf.Clamp01((h.Level - 1) / 39f);
            float area = Mathf.Clamp01(h.Derived.AreaPct / 0.6f);
            float move = Mathf.Clamp01((h.Stats.MoveSpeed / HeroStats.BaseMoveSpeed - 1f) / 0.5f);
            float grown = Mathf.Clamp01(0.6f * lv + 0.2f * area + 0.2f * move);
            return AutoZoom() * Mathf.Lerp(StartZoomMul, EndZoomMul, grown);
        }

        // 희승 11-22: 1.5 was "너무 가까이서 시작한다"; 11-23: 1.25 still was - the screenshot sent ("처음 시작할때 화면 사이즈
        // 이정도에서") is the Lv5 view two wheel notches out, ≈ the whole-step view (1080p ×2). A new hero starts there and
        // the growth pulls back a little past it as the fights fill the screen (×1.7 at 1080p for a grown hero).
        public const float StartZoomMul = 1f;
        public const float EndZoomMul = 0.85f;

        /// <summary>Art pixels per screen pixel stays a whole number: the PPC picks the largest zoom that fits the
        /// reference resolution, so asking for exactly screen/zoom gives exactly that zoom.</summary>
        void ApplyZoom()
        {
            _presenter?.Apply(_zoom);
        }

        /// <summary>HUD size on screen: grows with resolution in whole steps, never shrinks below the world zoom.</summary>
        int UiScale() => Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));

        Bloom _bloom;
        Vignette _vignette;
        ColorAdjustments _grade;
        ShadowsMidtonesHighlights _smh;
        bool _postSnap = true;
        Color _vigColor = Color.black;
        float _vigIntensity = 0.3f, _hurt, _bloomBase = 0.35f;

        void SetupPostProcessing()
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            // The scene's camera was built with HDR off (SceneBuilder), so nothing passed 1.0 and the bloom never fired:
            // spell cores, fires and flashes are meant to glow (docs/research/1122_pixel_lighting.md §1).
            cam.allowHDR = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            // HDR on (2026-11-21 재기획 §3): lit floors no longer clip to orange at 1.0, so the bloom line sits at 1.0 —
            // only spell cores and prop lights pushed past it glow. White tint, no warm push from grading.
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1f);
            _bloom.highQualityFiltering.Override(true);
            _bloom.intensity.Override(0.35f);
            _bloom.scatter.Override(0.6f);
            _bloom.tint.Override(Color.white);
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.3f);
            _vignette.smoothness.Override(0.45f);
            _vignette.color.Override(Color.black);
            _grade = profile.Add<ColorAdjustments>(true);
            _grade.contrast.Override(8f);
            _grade.saturation.Override(0f);
            _grade.postExposure.Override(0f);
            _smh = profile.Add<ShadowsMidtonesHighlights>(true);
            _smh.shadows.Override(new Vector4(1f, 1f, 1f, 0f));
            _smh.highlights.Override(new Vector4(1f, 1f, 1f, 0f));
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// Per-act post (docs/research/2026-11-21-modern-pixel-rendering.md §3.2): bloom, contrast, saturation, shadow tint,
        /// a coloured vignette and a near-black void with the act's hue. Bloom is half the report's value — 희승 found the
        /// spells eye-burning, so only cores glow. Eased over about 1.5 s when the act changes.
        /// </summary>
        static readonly (float bloom, float contrast, float sat, Vector4 shadows, Vector4 highs, Color vig, float vigI, float vigS, Color voidC)[] ActPost =
        {
            (0.40f,  8f,  5f, new Vector4(1f, 1f, 1f, 0f),          new Vector4(1.02f, 1.01f, 0.98f, 0f), Hex(0x2A1E12), 0.30f, 0.45f, Hex(0x0E0B08)),
            (0.50f, 12f, 10f, new Vector4(0.96f, 1.02f, 1.02f, 0f), new Vector4(1f, 1f, 1f, 0f),          Hex(0x0E2418), 0.35f, 0.50f, Hex(0x060C09)),
            (0.45f, 10f,  0f, new Vector4(1.00f, 0.95f, 1.05f, 0f), new Vector4(1f, 1f, 1f, 0f),          Hex(0x2A0A10), 0.35f, 0.50f, Hex(0x0C0507)),
            (0.60f, 14f,  8f, new Vector4(0.94f, 0.98f, 1.08f, 0f), new Vector4(1f, 1f, 1f, 0f),          Hex(0x0B2040), 0.38f, 0.50f, Hex(0x050A12)),
            (0.70f, 12f,  5f, new Vector4(1.05f, 0.97f, 0.94f, 0f), new Vector4(1.02f, 1f, 0.97f, 0f),    Hex(0x2A0E04), 0.35f, 0.45f, Hex(0x0B0604)),
            (0.60f, 12f,  5f, new Vector4(0.97f, 0.95f, 1.08f, -0.02f), new Vector4(1f, 1f, 1f, 0f),      Hex(0x120E2A), 0.42f, 0.55f, Hex(0x040410)),
        };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        bool _anyWindowWas;

        /// <summary>A window opening (any of them) sounds once: a soft creak and a page.</summary>
        void SoundWindows()
        {
            if (_gear == null) return;
            bool any = _gear.Visible || _ringPanel.Visible || _talents.Visible || _camp.Visible || _codex.Visible || _mailbox.Visible
                || _journal.Visible || _settings.Visible || _help.Visible;
            if (any && !_anyWindowWas) _audio.Ui("ui_open");
            _anyWindowWas = any;
        }

        void WatchStuck()
        {
            if (World == null || _toasts == null) return;
            float now = Time.unscaledTime;
            // A new best, a rebirth (floors start over) or an idle switch that stays put on purpose: start counting again.
            bool reset = World.FloorIndex > _bestFloor || World.FloorIndex < _bestFloor - 13 || World.FloorLock || World.TopLoop || World.CodexFirst;
            if (reset) { _bestFloor = World.FloorIndex; _bestAt = now; _deathsAtBest = World.Deaths; return; }
            int deaths = World.Deaths - _deathsAtBest;
            if (now - _bestAt >= 1800f && deaths >= 3 && now - _stuckToldAt >= 1800f)
            {
                _stuckToldAt = now;
                _toasts.Stuck(Mathf.RoundToInt((now - _bestAt) / 60f), deaths);
            }
        }

        void UpdateActPost(float dt)
        {
            if (_bloom == null || World == null) return;
            var p = ActPost[Mathf.Clamp(World.ActLoop, 0, ActPost.Length - 1)];
            float k = _postSnap ? 1f : 1f - Mathf.Exp(-dt * 2.5f);
            _postSnap = false;
            // Audit 11-24: the act's bloom, swelled for a quarter second by a big blow (ultimate, grand resonance, a boss's fall).
            _bloomBase = Mathf.Lerp(_bloomBase, p.bloom, k);
            _bloom.intensity.value = _bloomBase * (1f + 1.2f * (_view != null ? _view.BloomKick : 0f));
            _grade.contrast.value = Mathf.Lerp(_grade.contrast.value, p.contrast, k);
            _grade.saturation.value = Mathf.Lerp(_grade.saturation.value, p.sat, k);
            _smh.shadows.value = Vector4.Lerp(_smh.shadows.value, p.shadows, k);
            _smh.highlights.value = Vector4.Lerp(_smh.highlights.value, p.highs, k);
            _vigColor = Color.Lerp(_vigColor, p.vig, k);
            _vigIntensity = Mathf.Lerp(_vigIntensity, p.vigI, k);
            // 재기획 §4.3 (DMD): a blow to the hero reddens the screen edge for 0.28 s, by how much of the life it took.
            _hurt = Mathf.Max(0f, _hurt - dt / 0.28f);
            _vignette.color.value = Color.Lerp(_vigColor, new Color(0.55f, 0.02f, 0.02f), _hurt);
            _vignette.intensity.value = _vigIntensity + 0.18f * _hurt;
            _vignette.smoothness.value = Mathf.Lerp(_vignette.smoothness.value, p.vigS, k);
            cam.backgroundColor = Color.Lerp(cam.backgroundColor, p.voidC, k);
        }

        Action _tick;
        HudBar _hudBar;
        // 정체 알림: the best floor and when it was last beaten, deaths since then (real time: it is for the watcher).
        int _bestFloor = -1, _deathsAtBest;
        float _bestAt, _stuckToldAt = -1e9f;
        bool _uiHidden;
        AudioView _audio;
        MinimapView _minimap;
        HelpPanel _help;
        MenuBar _menu;
        SettingsPanel _settings;
        DebugPanel _debug;
        ToastView _toasts;
        JournalPanel _journal;

        /// <summary>Opens one panel by menu button index (MenuBar.Buttons), closing the others like its key does.</summary>
        void OpenFromMenu(int i)
        {
            void CloseAll(object keep)
            {
                if (keep != (object)_gear && _gear.Visible) _gear.Toggle();
                if (keep != (object)_gear && keep != (object)_character && _character.Visible) _character.Toggle();
                if (keep != (object)_ringPanel && _ringPanel.Visible) _ringPanel.Toggle();
                if (keep != (object)_talents && _talents.Visible) _talents.Toggle();
                if (keep != (object)_camp && _camp.Visible) _camp.Toggle();
                if (keep != (object)_codex) _codex.Close();
                if (keep != (object)_mailbox && _mailbox.Visible) _mailbox.Toggle();
                if (keep != (object)_settings && _settings.Visible) _settings.Toggle();
                if (keep != (object)_help && _help.Visible) _help.Toggle();
                if (keep != (object)_journal && _journal.Visible) _journal.Toggle(_toasts.History);
                if (_debug.Visible) _debug.Toggle(); // the test panel takes the keys: a menu click means the player is done with it
            }
            switch (i)
            {
                case 0: CloseAll(_gear); _gear.Toggle(); break;
                case 1: CloseAll(_ringPanel); _ringPanel.Toggle(); break;
                case 2: CloseAll(_talents); _talents.Toggle(); break;
                case 3: CloseAll(_camp); _camp.Toggle(); break;
                case 4: CloseAll(_codex); _codex.Toggle(); break;
                case 5: CloseAll(_mailbox); _mailbox.Toggle(); break;
                case 6: CloseAll(_journal); _journal.Toggle(_toasts.History); break;
                case 7: CloseAll(_settings); _settings.Toggle(); RefreshSettings(); break;
                case 8: CloseAll(_help); _help.Toggle(); break;
                case 9: CloseAll(_gear); _character.Toggle(); break; // beside the inventory, as in Diablo II
            }
        }

        /// <summary>
        /// F1 → P 후반 미리보기 (SimWorld.ApplyLatePreview) without touching the save — saving stops for this run first;
        /// restart to get the real hero back.
        /// </summary>
        void LatePreview()
        {
            Save(); // the real hero is safe on disk first
            _saves = null;
            World.ApplyLatePreview();
            _debug.Previewing = true;
            _debug.Show(false);
            _toasts.Tip("후반 미리보기 · 이번 진행은 저장되지 않습니다. 재시작하면 기존 캐릭터로 복귀합니다");
        }

        void RefreshSettings() => _settings.Refresh(_audio.MusicOn, _audio.Muted, _view.Numbers.ModeName, Screen.fullScreenMode != FullScreenMode.Windowed, World.AutoIgnite);

        void ChangeSetting(int i)
        {
            switch (i)
            {
                case 0: GameSettings.ToggleShake(); break;
                case 1: GameSettings.ToggleHitFlash(); break;
                case 2: GameSettings.CycleFx(); break;
                case 3: _view.Numbers.Toggle(); break;
                case 4: GameSettings.CycleEcho(); break; // rows follow SettingsPanel.Rows
                case 5: _audio.ToggleMusic(); break;
                case 6: _audio.ToggleMute(); break;
                case 7: ToggleFullscreen(); break;
                case 8: World.AutoIgnite = !World.AutoIgnite; _saveSoon = true; break;
                case 9: GameSettings.ToggleNotices(); break;
                case 10: GameSettings.ToggleBlood(); break;
                case 11:
                    UiFont.CycleNext();
                    _toasts.Setting("글꼴 · " + UiFont.SetNames[UiFont.NextSet] + (UiFont.NextSet != UiFont.Set ? " (다시 시작하면 바뀝니다)" : ""));
                    break;
            }
            RefreshSettings();
        }
        UltimateCutIn _cutIn;
        ScreenFade _fade;
        MailboxPanel _mailbox;
        bool _snapCamera = true;

        void Update()
        {
            UpdateActPost(Time.unscaledDeltaTime);
            WatchStuck();
            SoundWindows();
            _tick ??= () =>
            {
                World.Tick();
                _view.OnTick();
                _audio.OnTick(World.Events);
                foreach (var ev in World.Events)
                    if (ev.Type == SimEventType.Cast && ev.SourceId == World.Hero.Id && SkillBook.Get(ev.SkillId)?.Role == SkillRole.Ultimate)
                    {
                        _cutIn.Play(World.Hero.ClassId, SkillBook.Get(ev.SkillId));
                        _view.SlowMo(0.3f, 0.55f);
                    }
                _minimap.OnTick();
                _toasts.OnTick(World.Events, World);
                _reveal.OnTick(World.Events);
                // The world holds its breath for a legendary or unique as its shaft lands (11-24, the D2 "orange beam" beat).
                foreach (var ev in World.Events)
                    if (ev.Type == SimEventType.Drop && ev.Item != null && ev.Item.Rarity >= Rarity.Legendary) { _view.SlowMo(0.35f, 0.4f); break; }
                _banner.OnTick(World.Events);
                _tips.OnTick(World);
                _hunt.OnTick(World.Events);
                _audio.SetAct(Mathf.Min(World.ActLoop, EnemyRoster.Acts.Length - 1));
                bool died = false, retreat = false;
                foreach (var ev in World.Events)
                {
                    if (ev.Type == SimEventType.HeroDied) { died = true; retreat = ev.Value > 0f; }
                    if (ev.Type == SimEventType.Hit && ev.TargetId == World.Hero.Id && ev.Value > 0f)
                        _hurt = Mathf.Max(_hurt, Mathf.Clamp01(6f * ev.Value / Mathf.Max(1f, World.Hero.Stats.MaxHp))); // a 5% hit is a faint edge, 15% full red
                }
                if (died) { _fade.Death(World.Deaths, retreat); _view.SlowMo(0.3f, 0.6f); }
                if (_view.FloorChangedThisTick)
                {
                    _fade.FloorIn();
                    _snapCamera = true;
                }
                foreach (var ev in World.Events)
                {
                    if (ev.Type == SimEventType.ZoneEntered) _hud.ShowZone((int)ev.Value, ev.Depth);
                    if (ev.Type == SimEventType.FloorCleared || ev.Type == SimEventType.LevelUp ||
                        (ev.Type == SimEventType.Drop && ev.Item != null && ev.Item.Rarity >= Rarity.Legendary))
                        _saveSoon = true;
                }
            };
            // The class screen hides the HUD bar while it is up: its numbers showed through the cards (loop 4).
            if (_classSelect.Visible) { if (_hudBar.Visible) { _hudBar.Visible = false; _hudHiddenForClass = true; } }
            else if (_hudHiddenForClass) { _hudHiddenForClass = false; _hudBar.Visible = true; }
            if (_classSelect.Visible)
            {
                // First launch: the world waits until a class is chosen.
                var keys = UnityEngine.InputSystem.Keyboard.current;
                if (keys != null && _classSelect.Closable && keys.escapeKey.wasPressedThisFrame) { _classSelect.Show(false); return; }
                if (keys != null)
                    for (int i = 0; i < 4; i++)
                        if ((i == 0 ? keys.digit1Key : i == 1 ? keys.digit2Key : i == 2 ? keys.digit3Key : keys.digit4Key).wasPressedThisFrame) { ChooseClass(ClassSelectPanel.ClassAt(i)); return; }
                _view.Render(0f, Time.deltaTime);
                return;
            }
            _view.TickSlowMo(Time.unscaledDeltaTime);
            float dt = Time.deltaTime * _view.SlowScale; // hit-stop slows the world and its effects, not the HUD
            _clock.Step(dt * simSpeed, _tick);
            _view.Render(_clock.Alpha, dt);
            _hud.Render(Time.deltaTime);
            _hudBar.SetWindowOpen(_anyWindowWas || _character.Visible);
            _hudBar.Render(Time.unscaledDeltaTime);
            _minimap.Render(World.Hero.Pos);
            _cutIn.Render(Time.unscaledDeltaTime);
            _fade.Render(Time.unscaledDeltaTime);
            _mailbox.Render();
            // The cards never cut into a panel the player is reading (they dealt over the open inventory and picked on their
            // own seconds later, 11-23): while one is open the offer waits - its auto-pick clock held - and comes on close.
            bool reading = _gear.Visible || _character.Visible || _ringPanel.Visible || _talents.Visible || _camp.Visible || _codex.Visible
                || _mailbox.Visible || _settings.Visible || _help.Visible || _journal.Visible || _debug.Visible;
            if (reading && World.Hero.AugOffer != null && !_augment.Visible) World.Hero.AugOfferAt = World.Time;
            _augment.Update(Time.deltaTime, World, !_offline.Visible && !_classSelect.Visible && !reading);
            // The hunt board steps aside while a window is open: windows cut it down to bare numbers (loop 2 P0).
            _hunt.Update(World, Time.unscaledDeltaTime, !_uiHidden && !_offline.Visible && !_classSelect.Visible && !reading);
            _tips.Hidden = _uiHidden || _offline.Visible || _classSelect.Visible || _augment.Visible;
            _tips.Render(Time.unscaledDeltaTime);
            // Under an open window it waits (critique 6: the box lay over the skill card); its own timer still picks the default.
            _eventPrompt.Render(World, _uiHidden || _offline.Visible || _classSelect.Visible || _anyWindowWas || _character.Visible);
            _reveal.Render(World, _uiHidden || _offline.Visible || _classSelect.Visible || _anyWindowWas || _character.Visible); // never over a window
            _banner.Update(Time.unscaledDeltaTime);

            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            // While the return's decision cards are up, a click belongs to them; a key still closes.
            bool anyInput = (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame && !_offline.CardsUp);
            if (_offline.Visible || !Application.isFocused) _hud.ClearHover();
            if (_offline.Visible)
            {
                _offline.Update(Time.deltaTime, anyInput && Application.isFocused); // Esc also just closes the summary
            }
            else if (kb != null && Application.isFocused)
            {
                // Mouse: the menu buttons (hover lights them), then the settings lines.
                // The screen GUI takes its own clicks (right click there salvages): the world ignores a pointer over it.
                bool overGui = UiKit.PointerOverGui;
                if (mouse != null && !overGui)
                {
                    var mp = mouse.position.ReadValue();
                    int over = _menu.Hit(cam, mp);
                    _menu.Hovered(over);
                    _hud.HoverRing(cam, mp);
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        if (over >= 0) { OpenFromMenu(over); goto AfterInput; }
                        bool panelOpen = _debug.Visible || _settings.Visible || _help.Visible || _journal.Visible || _mailbox.Visible || _gear.Visible
                            || _camp.Visible || _talents.Visible || _codex.Visible || _ringPanel.Visible;
                        if (!panelOpen && _minimap.Hit(cam, mp)) { _minimap.ToggleLarge(); goto AfterInput; }
                    }
                }
                // F1 test panel: kept in player builds while 희승 tests them. Turn DebugPanel.Enabled off before release.
                if (DebugPanel.Enabled && kb.f1Key.wasPressedThisFrame) _debug.Toggle();
                // F2 (재기획 §5.8): hide the HUD, the notices and the map — only the hunt on screen; F2 again brings them back.
                if (kb.f2Key.wasPressedThisFrame)
                {
                    _uiHidden = !_uiHidden;
                    _hudBar.Visible = !_uiHidden;
                    _hud.SetVisible(!_uiHidden);
                    _toasts.Hidden = _uiHidden;
                    _minimap.Hide(_uiHidden);
                }
                if (_debug.Visible)
                {
                    // F1 test panel: digits jump acts and grant levels, legendaries, mastery (see DebugPanel.Act).
                    for (int i = 0; i <= 9; i++)
                        if (kb[UnityEngine.InputSystem.Key.Digit0 + i].wasPressedThisFrame) { _debug.Act(i); _saveSoon = true; }
                    if (kb.pKey.wasPressedThisFrame) LatePreview();
                    if (kb.vKey.wasPressedThisFrame) { _view.Showcase = !_view.Showcase; _toasts.Tip(_view.Showcase ? "VFX 쇼케이스 · 다시 F1 → V로 끕니다" : "VFX 쇼케이스 끔"); }
                    if (kb.escapeKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame)) _debug.Toggle();
                    goto AfterInput;
                }
                if (_settings.Visible)
                {
                    for (int i = 0; i < 9; i++) if (kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame) ChangeSetting(i);
                    if (kb.digit0Key.wasPressedThisFrame) ChangeSetting(9); // the tenth row
                    if (kb.escapeKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame)) _settings.Toggle();
                    goto AfterInput; // the panel takes the keys, but rendering and autosave go on below
                }
                // Any panel key closes the journal first (it is a full overlay).
                if (_journal.Visible && (kb.tabKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame
                    || kb.cKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame)) _journal.Toggle(_toasts.History);
                if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) _gear.Toggle(); // I: Diablo II's inventory key
                if (kb.fKey.wasPressedThisFrame) { _gear.CycleFilter(); _saveSoon = true; _toasts.Setting("자동 분해 · " + GearPanel.SalvageName(World.AutoSalvage) + (World.AutoSalvage < 0 ? "" : " (전설 이상·잠근 장비는 지킴)")); }
                if (kb.pKey.wasPressedThisFrame) { World.SetPreset(World.Hero.Preset + 1); _saveSoon = true; _toasts.Setting("스킬 배치 · " + SkillBook.PresetsFor(World.Hero.ClassId)[World.Hero.Preset].Name); }
                // K opens the character screen (희승 11-23: "k키로 직업 변경하려고 했더니 안되던데" - it wanted a second press
                // within two seconds, a rule nobody could see): four cards with each character's level and act, click or 1-4.
                if (kb.kKey.wasPressedThisFrame) { OpenCharacters(); goto AfterInput; }
                // GDD §14: the cartographer unlocks orders (Lv1) and the floor lock (Lv2).
                if (kb.oKey.wasPressedThisFrame && World.CartographerLevel >= 1) { World.Order = (ExpeditionOrder)(((int)World.Order + 1) % 4); _saveSoon = true; _toasts.Setting("원정 명령 · " + OrderName(World.Order)); }
                if (kb.sKey.wasPressedThisFrame) { World.Stance = (Stance)(((int)World.Stance + 1) % 3); _saveSoon = true; _toasts.Setting("태세 · " + SimWorld.StanceName(World.Stance) + (World.Stance == Stance.Charge ? " (주는 피해·받는 피해 +12%, 쉬지 않음)" : World.Stance == Stance.Careful ? " (주는 피해 −8%, 받는 피해 −12%, 자주 쉼)" : " (기본)")); }
                // L cycles the idle switches: off → 층 고정 → 도감 먼저 (지도 제작자 Lv3) → off.
                if (kb.lKey.wasPressedThisFrame && World.CartographerLevel >= 2)
                {
                    // off → 층 고정 → 도감 먼저 → 꼭대기 반복 (the last two need 지도 제작자 Lv3) → off
                    if (!World.FloorLock && !World.CodexFirst && !World.TopLoop) World.FloorLock = true;
                    else if (World.FloorLock && World.CartographerLevel >= 3) { World.FloorLock = false; World.CodexFirst = true; }
                    else if (World.CodexFirst) { World.CodexFirst = false; World.TopLoop = true; }
                    else { World.FloorLock = false; World.CodexFirst = false; World.TopLoop = false; }
                    _saveSoon = true;
                    _toasts.Setting("방치 방식 · " + (World.FloorLock ? "층 고정 (지금 층에서 계속 사냥)" : World.CodexFirst ? "도감 먼저" : World.TopLoop ? "꼭대기 반복" : "끔 (계속 내려감)"));
                }
                if (kb.cKey.wasPressedThisFrame) { if (_talents.Visible) _talents.Toggle(); if (_camp.Visible) _camp.Toggle(); _character.Toggle(); } // Diablo II's character screen
                if (kb.yKey.wasPressedThisFrame) { if (_gear.Visible) _gear.Toggle(); if (_character.Visible) _character.Toggle(); if (_talents.Visible) _talents.Toggle(); _camp.Toggle(); }
                if (kb.tKey.wasPressedThisFrame) { if (_gear.Visible) _gear.Toggle(); if (_camp.Visible) _camp.Toggle(); _codex.Close(); _talents.Toggle(); }
                if (kb.jKey.wasPressedThisFrame) { if (_gear.Visible) _gear.Toggle(); if (_camp.Visible) _camp.Toggle(); if (_talents.Visible) _talents.Toggle(); if (_ringPanel.Visible) _ringPanel.Toggle(); _codex.Toggle(); }
                if (kb.rKey.wasPressedThisFrame) { if (_gear.Visible) _gear.Toggle(); if (_camp.Visible) _camp.Toggle(); if (_talents.Visible) _talents.Toggle(); _codex.Close(); _ringPanel.Toggle(); }
                if (_ringPanel.Visible)
                {
                    for (int i = 0; i < 4; i++)
                        if ((i == 0 ? kb.digit1Key : i == 1 ? kb.digit2Key : i == 2 ? kb.digit3Key : kb.digit4Key).wasPressedThisFrame) _ringPanel.SelectSlot(i);
                    if (kb.qKey.wasPressedThisFrame) { _ringPanel.CycleSkill(-1); _saveSoon = true; }
                    if (kb.eKey.wasPressedThisFrame) { _ringPanel.CycleSkill(1); _saveSoon = true; }
                    if (kb.aKey.wasPressedThisFrame) { _ringPanel.CycleSigil(-1); _saveSoon = true; }
                    if (kb.dKey.wasPressedThisFrame) { _ringPanel.CycleSigil(1); _saveSoon = true; }
                }
                else if (_camp.Visible)
                {
                    if (kb.qKey.wasPressedThisFrame) _camp.NextSpot(-1);
                    if (kb.eKey.wasPressedThisFrame) _camp.NextSpot(1);
                    if (kb.aKey.wasPressedThisFrame) _camp.NextLine(-1);
                    if (kb.dKey.wasPressedThisFrame) _camp.NextLine(1);
                    for (int i = 0; i < 8; i++)
                        if ((i == 0 ? kb.digit1Key : i == 1 ? kb.digit2Key : i == 2 ? kb.digit3Key : i == 3 ? kb.digit4Key : i == 4 ? kb.digit5Key : i == 5 ? kb.digit6Key : i == 6 ? kb.digit7Key : kb.digit8Key).wasPressedThisFrame && _camp.Buy(i)) _saveSoon = true;
                }
                else if (World.PendingEvent != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) World.ChooseEvent(0);
                    else if (kb.digit2Key.wasPressedThisFrame) World.ChooseEvent(1);
                    else if (kb.digit3Key.wasPressedThisFrame) World.ChooseEvent(2);
                }
                if (kb.f11Key.wasPressedThisFrame) ToggleFullscreen();
                if (kb.mKey.wasPressedThisFrame) _audio.ToggleMute();   // GDD §18
                if (kb.vKey.wasPressedThisFrame) _view.Numbers.Toggle();
                // Z was only the minimap; for five seconds after a hand salvage it puts the piece back first (UX plan A7).
                if (kb.zKey.wasPressedThisFrame && !_gear.TryUndoSalvage()) _minimap.Toggle();
                if (kb.hKey.wasPressedThisFrame) _help.Toggle();
                if (kb.uKey.wasPressedThisFrame) _mailbox.Toggle();
                if (kb.gKey.wasPressedThisFrame) OpenFromMenu(6); // 사냥 일지
                if (_mailbox.Visible)
                {
                    if (kb.qKey.wasPressedThisFrame) _mailbox.Move(-1);
                    if (kb.eKey.wasPressedThisFrame) _mailbox.Move(1);
                }
                if (kb.xKey.wasPressedThisFrame) { World.CycleLantern(); _saveSoon = true; _toasts.Setting("등불 색 · " + SimWorld.LanternColors[World.Hero.LanternColor].name); }
                if (kb.bKey.wasPressedThisFrame) { Save(); BossKey(); }
                if (kb.nKey.wasPressedThisFrame) _audio.ToggleMusic();
                // GDD §5.3 등불 점화: Space, or a click on the lantern vial or on the hero who carries it.
                bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame && !overGui;
                if (kb.spaceKey.wasPressedThisFrame || (clicked && LanternClicked(mouse.position.ReadValue()))) World.Ignite();
                float wheel = mouse != null && !overGui ? mouse.scroll.ReadValue().y : 0f; // over the GUI the wheel scrolls lists
                if (wheel != 0f)
                {
                    _zoomMul = Mathf.Clamp(_zoomMul * (wheel > 0f ? 1.12f : 1f / 1.12f), 0.6f, 1.8f); // the wheel nudges the growth zoom
                }
                // Esc closes the panel, else leaves fullscreen. It never quits: the window has a close button.
                // Right click closes the open panel, like Esc, but never opens the settings.
                bool rightClick = mouse != null && mouse.rightButton.wasPressedThisFrame && !overGui;
                bool anyPanel = _help.Visible || _journal.Visible || _mailbox.Visible || _gear.Visible || _camp.Visible || _talents.Visible || _codex.Visible || _ringPanel.Visible;
                if (kb.escapeKey.wasPressedThisFrame || (rightClick && anyPanel))
                {
                    if (_help.Visible) _help.Toggle();
                    else if (_journal.Visible) _journal.Toggle(_toasts.History);
                    else if (_mailbox.Visible) _mailbox.Toggle();
                    else if (_gear.Visible) _gear.Toggle();
                    else if (_camp.Visible) _camp.Toggle();
                    else if (_talents.Visible) _talents.Toggle();
                    else if (_codex.Visible) _codex.Close();
                    else if (_ringPanel.Visible) _ringPanel.Toggle();
                    else { _settings.Toggle(); RefreshSettings(); } // nothing open: the settings (전체 화면 is one of them)
                }
            }
            AfterInput:
            _debug.Render(Time.unscaledDeltaTime);
            _toasts.Render(Time.unscaledDeltaTime);
            _gear.Render();
            _character.Render();
            _camp.Render();
            _talents.Render();
            _codex.Render(Time.unscaledDeltaTime);
            _ringPanel.Render();

            _saveTimer += Time.unscaledDeltaTime;
            _backupTimer += Time.unscaledDeltaTime;
            if (_saveSoon || _saveTimer >= AutosaveSeconds) Save();
        }

        bool LanternClicked(Vector2 screen)
        {
            if (_hud.LanternHit(cam, screen)) return true;
            var p = WorldPresenter.ScreenToWorld(cam, screen);
            var hero = _view.HeroWorldPos(_clock.Alpha) + new Vector3(0f, 0.35f, 0f);
            return (new Vector2(p.x - hero.x, p.y - hero.y)).sqrMagnitude <= 0.6f * 0.6f;
        }

        void Save() => Save(false);

        void Save(bool rotateNow)
        {
            _saveSoon = false;
            _saveTimer = 0f;
            if (_saves == null || World == null) return;
            bool rotate = rotateNow || _backupTimer >= BackupSeconds;
            if (rotate) _backupTimer = 0f;
            try
            {
                _saves.Save(World.ToSave(), DateTime.UtcNow, rotate);
                StoreAccountWick();
                if (_saveFailures >= 3) _hud.SetWarning(null);
                _saveFailures = 0;
            }
            catch (Exception e)
            {
                _saveFailures++;
                Debug.LogWarning("[Lanternkeeper] save failed: " + e.Message);
                if (_saveFailures >= 3) _hud.SetWarning("저장에 계속 실패하고 있습니다");
            }
        }

        int _saveFailures;

        void OnApplicationQuit() { Save(); _toasts?.OnApplicationQuit(); } // the journal keeps its last lines too

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        /// <summary>
        /// GDD §3.2 저전력: unfocused (watched on a second monitor while working) it renders every second refresh — 30 fps on
        /// 60 Hz, 72 on 144 Hz — still vsynced and smooth, about half the GPU/CPU. The Sim runs on its own clock.
        /// </summary>
        void OnApplicationFocus(bool focused)
        {
            QualitySettings.vSyncCount = focused ? 1 : 2;
            Application.targetFrameRate = -1;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#endif

        /// <summary>GDD §3.2 보스 키 (B): hide the window at once; the world keeps going.</summary>
        static void BossKey()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Screen.fullScreenMode != FullScreenMode.Windowed) Screen.fullScreenMode = FullScreenMode.Windowed;
            ShowWindow(GetActiveWindow(), 6); // SW_MINIMIZE
#endif
        }

        void LateUpdate()
        {
            // The pixel-perfect camera resizes the view with the window: keep the HUD on the view's edges.
            // A new window size hands the zoom back to the screen rule; the wheel only overrides it until then.
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != _zoomScreen) { _zoomScreen = screen; _zoomMul = 1f; _zoom = GrowthZoom(); }
            // Eases toward the growth zoom (a level-up or a new stride never jerks the view).
            float want = GrowthZoom() * _zoomMul;
            _zoom = Mathf.Exp(Mathf.MoveTowards(Mathf.Log(_zoom), Mathf.Log(want), Time.unscaledDeltaTime * 0.3f));
            ApplyZoom();
            _ui.UpdateScale();
            float ui = UiScale() / (float)_zoom;
            var half = new Vector3(cam.orthographicSize * cam.aspect, cam.orthographicSize, ui);
            if (half != _laidOutFor)
            {
                _laidOutFor = half;
                _hud.Layout(half.x, half.y, ui);
                _minimap.Layout(half.x, half.y, ui);
                _debug.SetScale(ui);
                _menu.Layout(half.x, half.y, ui);
                _cutIn.Layout(half.x, half.y, ui);
                _fade.Layout(half.x, half.y, ui);
            }

            var target = _view.HeroWorldPos(_clock.Alpha);
            target.z = cam.transform.position.z;
            if (_snapCamera)
            {
                cam.transform.position = target; // new floor: jump, don't pan across the old map
                _camVel = Vector3.zero;
                _shakeOffset = Vector3.zero;
                _snapCamera = false;
                return;
            }
            // Lock onto the hero on the same 1/64 pixel grid the sprites snap to. A damped camera lagged the
            // constant-speed hero, and the two rounded to pixels independently, so everything wobbled by ±1 px.
            const float px = 1f / 64f;
            var locked = new Vector3(Mathf.Round(target.x / px) * px, Mathf.Round(target.y / px) * px, target.z);
            _shakeOffset = Vector3.zero;
            if (_view.Shake > 0.001f && GameSettings.Shake)
            {
                // 재기획 §4.3 trauma shake: amplitude = 4 art px × trauma² (trauma = Shake / 0.2), smooth 28 Hz noise
                // instead of a fresh random spot every frame. It reached 12.8 px before, a fifth of the hero's height.
                float trauma = Mathf.Clamp01(_view.Shake / 0.2f);
                float amp = 4f * px * trauma * trauma;
                float t = Time.unscaledTime * 28f;
                _shakeOffset = new Vector3(Mathf.Round((Mathf.PerlinNoise(t, 0.3f) * 2f - 1f) * amp / px) * px,
                                           Mathf.Round((Mathf.PerlinNoise(0.7f, t) * 2f - 1f) * amp * 0.6f / px) * px, 0f);
            }
            cam.transform.position = locked + _shakeOffset;
        }

        Vector3 _shakeOffset;
    }
}
