// 버리는 프로토타입(스파이크): "몰이 사냥 쾌감" 검증용.
// 검증 질문: 스킬 고리 + 원소 공명 + 사망 전파 + 몰이 AI가 구경만 해도 시원한가? 작은 창에서 읽히는가?
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Spike
{
    public enum Elem { None, Ember, Frost, Shock, Rot }

    public class Unit
    {
        public Vector2 pos, knock;
        public float hp, maxHp, flash;
        public SpriteRenderer sr;
        public Sprite normal, white;
    }

    public class Enemy : Unit
    {
        public int pack;
        public bool aggro, elite, dead;
        public float atkCd, slow, stun, deadT;
        public Elem mark;
    }

    public class Skill
    {
        public string name;
        public Elem elem;
        public int kind; // 0 회전베기, 1 서리 파동, 2 전격 사슬, 3 부패 구름
        public float cd, timer, radius, dmg;
    }

    class Fx { public SpriteRenderer sr; public Vector3 vel; public float t, life, s0, s1; public bool gravity; public Color c; }
    class Loot { public SpriteRenderer sr, beam; public Vector2 pos; public float t, h, vh; public int rarity; }
    class Zone { public SpriteRenderer sr; public Vector2 pos; public float r, t, life, tick; }
    class FloatText { public Vector3 w; public string s; public Color c; public float t, life; public int size; }
    class Pending { public Vector2 pos; public Elem elem; public float power, at; public int depth; }

    public class SpikeGame : MonoBehaviour
    {
        public static SpikeGame I;

        [Header("실험 토글 (플레이 중 키 또는 script eval로 변경)")]
        public bool propagate = true;     // [P] 각인 사망 전파
        public int gatherThreshold;       // [G] 몰이 목표 수 (0 = 몰이 안 함). 실험상 몰이 안 함이 더 빨라 기본값 0
        public int orderPreset;           // [R] 고리 순서 프리셋
        public int linkSlot = 1;          // [L] '범위 확대' 문양 위치 (인접 슬롯도 +30%)
        public float timeMul = 1f;        // [1][2][4] 배속
        public bool forceHorde;           // [H] 다음 방을 호드 방으로
        public bool autoExperiment;       // 켜면 설정 4종을 방 6개씩 4배속으로 돌리고 콘솔에 [SPIKE] 로그를 남긴다
        const int RoomsPerConfig = 6;
        static readonly (string name, bool prop, int gather)[] Configs =
        {
            ("전파O·몰이15", true, 15), ("전파X·몰이15", false, 15), ("전파O·몰이X", true, 0), ("전파X·몰이X", false, 0),
        };
        string cfgName = "수동";
        int roomMaxChain, roomMaxStreak, roomReactions;
        float roomDmgTaken;

        [Header("측정값")]
        public int room, totalKills, streak, maxStreak, biggestChain, legendaryCount, uniqueCount, deaths;
        public float lastClearSec;
        public int[] reactions = new int[6];
        public string heroMode = "";
        public readonly List<float> clearLog = new();

        static readonly string[] ReactionNames = { "파쇄", "과부하", "역병 전도", "섬광", "연소", "취약" };
        static readonly int[][] Orders = { new[] { 0, 1, 2, 3 }, new[] { 0, 2, 1, 3 }, new[] { 3, 2, 1, 0 } };
        const float ArenaR = 7f;

        Camera cam;
        Vector3 camPos;
        Unit hero;
        float heroPotionCd;
        readonly List<Enemy> enemies = new();
        readonly List<Fx> fx = new();
        readonly List<Loot> loot = new();
        readonly List<Zone> zones = new();
        readonly List<FloatText> texts = new();
        readonly List<Pending> pending = new();
        readonly Queue<float> killTimes = new();
        Skill[] skills;
        int ringPtr, processingDepth;
        float castGap, basicCd, roomStart, nextRoomAt = -1, lastKillTime, shake, hitstopUntil, lastHitstop, gatherStart;
        bool hordeRoom;
        string toast; float toastUntil;
        Transform tileRoot;
        SpriteRenderer darkness, lanternGlow;
        Sprite ringSpr, discSpr, pixSpr, beamSpr;
        (Sprite n, Sprite w) heroArt, mobArt, eliteArt;
        GUIStyle style, bigStyle;

        static readonly Color[] ElemColor =
        {
            Color.white, new Color(1f, 0.45f, 0.15f), new Color(0.45f, 0.8f, 1f), new Color(1f, 0.95f, 0.35f), new Color(0.55f, 0.9f, 0.3f)
        };
        static readonly Color[] RarityColor =
        {
            new Color(0.8f, 0.8f, 0.8f), new Color(0.4f, 0.6f, 1f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.5f, 0.05f), new Color(0.95f, 0.78f, 0.4f)
        };

        public static Vector3 ToScreen(Vector2 g) => new Vector3((g.x - g.y) * 0.5f, (g.x + g.y) * 0.25f, 0f);

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            cam = Camera.main;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);

            ringSpr = SpikeArt.Ring();
            discSpr = SpikeArt.Disc();
            pixSpr = SpikeArt.Pixel();
            beamSpr = SpikeArt.Beam();
            heroArt = SpikeArt.Figure(14, 20, new Color(0.78f, 0.62f, 0.42f), new Color(0.12f, 0.08f, 0.05f), new Color(1f, 0.9f, 0.5f));
            mobArt = SpikeArt.Figure(12, 14, new Color(0.42f, 0.52f, 0.4f), new Color(0.06f, 0.08f, 0.06f), new Color(1f, 0.2f, 0.2f));
            eliteArt = SpikeArt.Figure(20, 24, new Color(0.58f, 0.32f, 0.62f), new Color(0.9f, 0.7f, 0.2f), new Color(1f, 0.9f, 0.2f));

            skills = new[]
            {
                new Skill { name = "잿불 회전베기", elem = Elem.Ember, kind = 0, cd = 1.2f, radius = 1.6f, dmg = 30 },
                new Skill { name = "서리 파동", elem = Elem.Frost, kind = 1, cd = 2.0f, radius = 2.2f, dmg = 18 },
                new Skill { name = "전격 사슬", elem = Elem.Shock, kind = 2, cd = 1.5f, radius = 1.8f, dmg = 28 },
                new Skill { name = "부패 구름", elem = Elem.Rot, kind = 3, cd = 3.0f, radius = 1.5f, dmg = 9 },
            };

            hero = new Unit { maxHp = 700, hp = 700, normal = heroArt.n, white = heroArt.w };
            hero.sr = NewSprite("Hero", heroArt.n, 0);
            lanternGlow = NewSprite("LanternGlow", discSpr, 0);
            lanternGlow.color = new Color(1f, 0.7f, 0.3f, 0.18f);
            lanternGlow.transform.localScale = Vector3.one * 2.2f;
            darkness = NewSprite("Darkness", SpikeArt.Darkness(), 30000);
            darkness.transform.localScale = new Vector3(4f, 4f, 1f);
            tileRoot = new GameObject("Tiles").transform;

            NewRoom();
        }

        SpriteRenderer NewSprite(string n, Sprite s, int order)
        {
            var go = new GameObject(n);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sharedMaterial = SpikeArt.Mat;
            r.sortingOrder = order;
            return r;
        }

        // ───────────── 방 생성 ─────────────
        void NewRoom()
        {
            room++;
            foreach (var e in enemies) Destroy(e.sr.gameObject);
            enemies.Clear();
            foreach (Transform c in tileRoot) Destroy(c.gameObject);

            Color[][] pal =
            {
                new[] { new Color(0.24f, 0.25f, 0.30f), new Color(0.14f, 0.15f, 0.19f) },
                new[] { new Color(0.20f, 0.30f, 0.30f), new Color(0.11f, 0.18f, 0.19f) },
                new[] { new Color(0.28f, 0.22f, 0.30f), new Color(0.17f, 0.12f, 0.18f) },
            };
            var p = pal[(room - 1) % 3];
            var tileSprites = new[] { SpikeArt.Tile(p[0], p[1], room * 7), SpikeArt.Tile(p[0] * 0.92f, p[1], room * 7 + 1), SpikeArt.Tile(p[0] * 1.06f, p[1], room * 7 + 2) };
            for (int i = -9; i <= 9; i++)
                for (int j = -9; j <= 9; j++)
                {
                    if (i * i + j * j > (ArenaR + 0.7f) * (ArenaR + 0.7f)) continue;
                    var r = NewSprite("t", tileSprites[Mathf.Abs(i * 31 + j * 17) % 3], -20000);
                    r.transform.SetParent(tileRoot);
                    r.transform.position = ToScreen(new Vector2(i, j));
                }

            if (autoExperiment)
            {
                int ci = (room - 1) / RoomsPerConfig;
                if (ci >= Configs.Length) { autoExperiment = false; Debug.Log("[SPIKE] 실험 종료"); }
                else
                {
                    (cfgName, propagate, gatherThreshold) = Configs[ci];
                    timeMul = 4f;
                    Random.InitState(1000 + (room - 1) % RoomsPerConfig); // 설정마다 같은 방 배치
                }
            }
            roomMaxChain = roomMaxStreak = roomReactions = 0;
            roomDmgTaken = 0f;
            hordeRoom = forceHorde || room % 3 == 0;
            forceHorde = false;
            int scaleRoom = autoExperiment ? (room - 1) % RoomsPerConfig : room - 1; // 실험 중엔 설정마다 같은 체력 곡선
            float hpScale = 1f + 0.06f * scaleRoom;
            int packs = hordeRoom ? 7 : 4;
            int perPack = hordeRoom ? 10 : 9;
            for (int k = 0; k < packs; k++)
            {
                float ang;
                do ang = Random.Range(0f, 360f); while (Mathf.Abs(Mathf.DeltaAngle(ang, 225f)) < 50f);
                var center = (Vector2)(Quaternion.Euler(0, 0, ang) * Vector2.right) * Random.Range(hordeRoom ? 2f : 2.5f, 6f);
                bool elitePack = k == 0;
                int count = perPack + Random.Range(-1, 3);
                for (int m = 0; m < count; m++)
                {
                    bool elite = elitePack && m == 0;
                    var art = elite ? eliteArt : mobArt;
                    var e = new Enemy
                    {
                        pack = k, elite = elite, normal = art.n, white = art.w,
                        pos = ClampArena(center + Random.insideUnitCircle * 0.9f),
                        maxHp = (elite ? 700 : 110) * hpScale,
                    };
                    e.hp = e.maxHp;
                    e.sr = NewSprite(elite ? "Elite" : "Mob", art.n, 0);
                    enemies.Add(e);
                }
            }

            hero.pos = new Vector2(-4.3f, -4.3f);
            hero.hp = hero.maxHp;
            roomStart = Time.time;
            gatherStart = Time.time;
            Toast(hordeRoom ? $"방 {room} — 호드!" : $"방 {room}");
        }

        Vector2 ClampArena(Vector2 g) => g.magnitude > ArenaR ? g.normalized * ArenaR : g;

        // ───────────── 메인 루프 ─────────────
        void Update()
        {
            HandleKeys();
            Time.timeScale = Time.unscaledTime < hitstopUntil ? 0.05f : timeMul;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            cam.orthographicSize = 6f / cam.aspect;
            if (nextRoomAt > 0 && Time.time >= nextRoomAt) { nextRoomAt = -1; NewRoom(); }

            foreach (var s in skills) s.timer -= dt;
            UpdateHero(dt);
            UpdateEnemies(dt);
            UpdatePending();
            UpdateZones(dt);
            UpdateLoot(dt);
            UpdateFx(dt);
            for (int i = texts.Count - 1; i >= 0; i--) { texts[i].t += dt; if (texts[i].t > texts[i].life) texts.RemoveAt(i); }

            if (streak > 0 && Time.time - lastKillTime > 1.5f) streak = 0;
            while (killTimes.Count > 0 && Time.time - killTimes.Peek() > 60f) killTimes.Dequeue();

            if (nextRoomAt < 0 && AliveCount() == 0)
            {
                lastClearSec = Time.time - roomStart;
                clearLog.Add(lastClearSec);
                Debug.Log($"[SPIKE] cfg={cfgName} room={room} horde={hordeRoom} clear={lastClearSec:0.0}s chain={roomMaxChain} streak={roomMaxStreak} reactions={roomReactions} dmgTaken={roomDmgTaken:0} deaths={deaths}");
                Toast($"방 클리어 {lastClearSec:0.0}초");
                nextRoomAt = Time.time + 1.5f;
            }

            var hs = ToScreen(hero.pos);
            camPos = Vector3.Lerp(camPos, new Vector3(hs.x, hs.y + 0.4f, -10f), 1f - Mathf.Exp(-6f * dt));
            shake = Mathf.Max(0f, shake - dt * 2.5f);
            var cp = camPos + (Vector3)(Random.insideUnitCircle * shake * 0.18f);
            cam.transform.position = new Vector3(Mathf.Round(cp.x * 32f) / 32f, Mathf.Round(cp.y * 32f) / 32f, -10f);
            darkness.transform.position = hs + new Vector3(0f, 0.3f, 0f);
            lanternGlow.transform.position = hs + new Vector3(0f, 0.05f, 0f);
            lanternGlow.sortingOrder = -19000;
        }

        int AliveCount() { int n = 0; foreach (var e in enemies) if (!e.dead) n++; return n; }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.pKey.wasPressedThisFrame) { propagate = !propagate; Toast($"사망 전파 {(propagate ? "켜짐" : "꺼짐")}"); }
            if (kb.gKey.wasPressedThisFrame) { gatherThreshold = gatherThreshold switch { 0 => 8, 8 => 15, 15 => 25, _ => 0 }; Toast($"몰이 목표 {gatherThreshold}"); }
            if (kb.rKey.wasPressedThisFrame) { orderPreset = (orderPreset + 1) % Orders.Length; Toast("고리 순서 변경"); }
            if (kb.lKey.wasPressedThisFrame) { linkSlot = (linkSlot + 1) % 4; Toast($"문양 위치 {linkSlot + 1}번 칸"); }
            if (kb.hKey.wasPressedThisFrame) { forceHorde = true; Toast("다음 방은 호드"); }
            if (kb.digit1Key.wasPressedThisFrame) timeMul = 1f;
            if (kb.digit2Key.wasPressedThisFrame) timeMul = 2f;
            if (kb.digit4Key.wasPressedThisFrame) timeMul = 4f;
        }

        // ───────────── 영웅: 몰이 AI + 스킬 고리 ─────────────
        void UpdateHero(float dt)
        {
            hero.hp = Mathf.Min(hero.maxHp, hero.hp + 10f * dt);
            heroPotionCd -= dt;
            if (hero.hp < hero.maxHp * 0.3f && heroPotionCd <= 0f)
            {
                hero.hp += hero.maxHp * 0.5f; heroPotionCd = 8f;
                AddText(hero.pos, "물약", new Color(0.4f, 1f, 0.4f), 1, 0.8f);
            }
            if (hero.hp <= 0f)
            {
                deaths++; hero.hp = hero.maxHp; hero.pos = new Vector2(-4.3f, -4.3f);
                foreach (var e in enemies) e.aggro = false;
                Toast("쓰러짐! 다시 일어선다");
            }

            int aggroCount = 0;
            Vector2 centroid = Vector2.zero;
            Enemy nearestIdle = null, nearestAny = null;
            float nd = float.MaxValue, na = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                float d = (e.pos - hero.pos).sqrMagnitude;
                if (d < na) { na = d; nearestAny = e; }
                if (e.aggro) { aggroCount++; centroid += e.pos; }
                else if (d < nd) { nd = d; nearestIdle = e; }
            }
            if (aggroCount > 0) centroid /= aggroCount; else gatherStart = Time.time;

            bool canGather = gatherThreshold > 0 && nearestIdle != null && hero.hp > hero.maxHp * 0.45f && Time.time - gatherStart < 10f;
            bool gathering = canGather && aggroCount < gatherThreshold;

            Vector2 move = Vector2.zero;
            float spd = 2.6f;
            if (gathering)
            {
                heroMode = $"몰이 중 {aggroCount}/{gatherThreshold}";
                move = (nearestIdle.pos - hero.pos).normalized;
                spd = 1.9f;
            }
            else if (aggroCount > 0)
            {
                heroMode = gatherThreshold > 0 && aggroCount >= gatherThreshold ? "폭발!" : "전투";
                if (hero.hp < hero.maxHp * 0.35f) move = (hero.pos - centroid).normalized;
                else if ((centroid - hero.pos).magnitude > 0.5f) move = (centroid - hero.pos).normalized;
                spd = 1.6f;
                CastRing(dt);
            }
            else if (nearestAny != null) { heroMode = "탐험"; move = (nearestAny.pos - hero.pos).normalized; }
            else heroMode = "대기";

            hero.pos = ClampArena(hero.pos + move * spd * dt);
            if (move.sqrMagnitude > 0.01f) hero.sr.flipX = ToScreen(move).x < 0f;
            hero.flash -= dt;
            Sync(hero);
        }

        void CastRing(float dt)
        {
            castGap -= dt;
            basicCd -= dt;
            var order = Orders[orderPreset];
            if (castGap <= 0f)
            {
                for (int k = 0; k < 4; k++)
                {
                    int slot = (ringPtr + k) % 4;
                    var s = skills[order[slot]];
                    if (s.timer > 0f) continue;
                    float rmul = slot == linkSlot || slot == (linkSlot + 1) % 4 || slot == (linkSlot + 3) % 4 ? 1.3f : 1f;
                    processingDepth = 0;
                    Fire(s, rmul);
                    s.timer = s.cd;
                    ringPtr = (slot + 1) % 4;
                    castGap = 0.28f;
                    return;
                }
            }
            if (basicCd <= 0f)
            {
                var t = Nearest(hero.pos, 1.2f, null);
                if (t != null) { processingDepth = 0; Hit(t, 14f, Elem.None, false); basicCd = 0.55f; }
            }
        }

        void Fire(Skill s, float rmul)
        {
            float r = s.radius * rmul;
            switch (s.kind)
            {
                case 0:
                case 1:
                    RingFx(hero.pos, r, ElemColor[(int)s.elem], 0.25f);
                    foreach (var e in Around(hero.pos, r))
                    {
                        if (s.kind == 1) e.slow = 1.5f;
                        Hit(e, s.dmg, s.elem, false);
                    }
                    break;
                case 2:
                {
                    var hit = new HashSet<Enemy>();
                    var cur = Nearest(hero.pos, 4f, hit);
                    Vector2 from = hero.pos;
                    for (int i = 0; i < 6 && cur != null; i++)
                    {
                        hit.Add(cur);
                        DotLine(from, cur.pos, ElemColor[(int)Elem.Shock]);
                        from = cur.pos;
                        Hit(cur, s.dmg, s.elem, false);
                        cur = Nearest(from, r, hit);
                    }
                    break;
                }
                case 3:
                {
                    Enemy best = null; int bestN = -1;
                    foreach (var e in enemies)
                    {
                        if (e.dead) continue;
                        int n = 0;
                        foreach (var o in enemies) if (!o.dead && (o.pos - e.pos).sqrMagnitude < r * r) n++;
                        if (n > bestN) { bestN = n; best = e; }
                    }
                    if (best == null) break;
                    var z = new Zone { pos = best.pos, r = r, life = 3f, sr = NewSprite("Rot", discSpr, -18000) };
                    z.sr.color = new Color(0.45f, 0.85f, 0.25f, 0.35f);
                    z.sr.transform.position = ToScreen(z.pos);
                    z.sr.transform.localScale = Vector3.one * (0.707f * r);
                    zones.Add(z);
                    break;
                }
            }
        }

        // ───────────── 피해, 공명, 처치, 사망 전파 ─────────────
        void Hit(Enemy e, float dmg, Elem el, bool fromReaction)
        {
            if (e.dead) return;
            if (!fromReaction && el != Elem.None && e.mark != Elem.None && e.mark != el)
            {
                var m = e.mark;
                e.mark = Elem.None;
                React(e, m, el);
                if (e.dead) return;
            }
            else if (el != Elem.None) e.mark = el;

            e.hp -= dmg;
            e.flash = 0.07f;
            e.knock += (e.pos - hero.pos).normalized * (e.elite ? 0.3f : 0.9f);
            AggroPack(e.pack);
            if (dmg >= 30f) AddText(e.pos, Mathf.RoundToInt(dmg).ToString(), ElemColor[(int)el], 0, 0.5f);
            if (e.hp <= 0f) Kill(e);
        }

        void React(Enemy e, Elem a, Elem b)
        {
            int type = Pair(a, b, Elem.Ember, Elem.Frost) ? 0
                : Pair(a, b, Elem.Frost, Elem.Shock) ? 1
                : Pair(a, b, Elem.Shock, Elem.Rot) ? 2
                : Pair(a, b, Elem.Ember, Elem.Shock) ? 3
                : Pair(a, b, Elem.Ember, Elem.Rot) ? 4 : 5;
            reactions[type]++;
            roomReactions++;
            float[] radius = { 1.2f, 1.0f, 2.0f, 1.4f, 1.2f, 1.2f };
            float[] dmg = { 45f, 25f, 20f, 30f, 35f, 25f };
            var col = Color.Lerp(ElemColor[(int)a], ElemColor[(int)b], 0.5f);
            RingFx(e.pos, radius[type], col, 0.3f);
            Burst(ToScreen(e.pos) + Vector3.up * 0.2f, col, 6);
            AddText(e.pos + new Vector2(0.3f, 0.3f), ReactionNames[type] + "!", col, 1, 0.7f);
            shake = Mathf.Max(shake, 0.25f);

            var targets = new List<Enemy>(Around(e.pos, radius[type]));
            if (type == 2) targets = Closest(e.pos, radius[type], 4);
            foreach (var o in targets)
            {
                if (type == 1) o.stun = 1.2f;
                if (type == 5) o.slow = 1.5f;
                Hit(o, dmg[type], Elem.None, true);
                if (type == 2 && !o.dead) o.mark = Elem.Rot;
            }
        }

        static bool Pair(Elem a, Elem b, Elem x, Elem y) => (a == x && b == y) || (a == y && b == x);

        void Kill(Enemy e)
        {
            e.dead = true;
            e.deadT = 0f;
            totalKills++;
            killTimes.Enqueue(Time.time);
            streak = Time.time - lastKillTime <= 1.5f ? streak + 1 : 1;
            lastKillTime = Time.time;
            if (streak > maxStreak) maxStreak = streak;
            if (streak > roomMaxStreak) roomMaxStreak = streak;
            if (streak == 10 || streak == 25 || streak == 50 || streak == 100) { Toast($"×{streak} 학살!"); shake = Mathf.Max(shake, 0.5f); }

            e.sr.sprite = e.normal;
            Burst(ToScreen(e.pos) + Vector3.up * 0.2f, e.elite ? new Color(0.7f, 0.4f, 0.8f) : new Color(0.5f, 0.6f, 0.45f), e.elite ? 24 : 10);
            if (e.elite) Hitstop(0.08f);
            RollLoot(e);

            if (propagate && e.mark != Elem.None)
            {
                int depth = processingDepth + 1;
                if (depth > biggestChain) biggestChain = depth;
                if (depth > roomMaxChain) roomMaxChain = depth;
                pending.Add(new Pending { pos = e.pos, elem = e.mark, power = e.maxHp * 0.45f, at = Time.time + 0.07f, depth = depth });
            }
        }

        void UpdatePending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (Time.time < p.at) continue;
                pending.RemoveAt(i);
                RingFx(p.pos, 1.3f, ElemColor[(int)p.elem], 0.22f);
                if (p.depth >= 6) { shake = Mathf.Max(shake, 0.35f); Hitstop(0.04f); }
                processingDepth = p.depth;
                foreach (var o in new List<Enemy>(Around(p.pos, 1.3f)))
                {
                    Hit(o, p.power, Elem.None, true);
                    if (!o.dead && o.mark == Elem.None) o.mark = p.elem;
                }
                processingDepth = 0;
            }
        }

        void AggroPack(int pack)
        {
            foreach (var e in enemies) if (e.pack == pack) e.aggro = true;
        }

        // ───────────── 적 ─────────────
        void UpdateEnemies(float dt)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.dead)
                {
                    e.deadT += dt;
                    e.sr.color = new Color(0.25f, 0.2f, 0.2f, Mathf.Clamp01(1.2f - e.deadT));
                    if (e.deadT > 1.2f) { Destroy(e.sr.gameObject); enemies.RemoveAt(i); }
                    continue;
                }
                e.flash -= dt; e.stun -= dt; e.slow -= dt;
                float d = (hero.pos - e.pos).magnitude;
                if (!e.aggro && d < 3f) AggroPack(e.pack);

                Vector2 v = Vector2.zero;
                if (e.aggro && e.stun <= 0f)
                {
                    if (d > 0.5f) v = (hero.pos - e.pos).normalized * (e.elite ? 1.1f : 1.45f) * (e.slow > 0f ? 0.5f : 1f);
                    else
                    {
                        e.atkCd -= dt;
                        if (e.atkCd <= 0f) { float hd = e.elite ? 16f : 3f; hero.hp -= hd; roomDmgTaken += hd; e.atkCd = 1f; hero.flash = 0.05f; }
                    }
                }
                e.pos = ClampArena(e.pos + (v + e.knock) * dt);
                e.knock = Vector2.Lerp(e.knock, Vector2.zero, 10f * dt);
            }

            // 겹침 밀어내기
            for (int a = 0; a < enemies.Count; a++)
            {
                var ea = enemies[a];
                if (ea.dead) continue;
                for (int b = a + 1; b < enemies.Count; b++)
                {
                    var eb = enemies[b];
                    if (eb.dead) continue;
                    var diff = ea.pos - eb.pos;
                    float min = ea.elite || eb.elite ? 0.5f : 0.32f;
                    float sq = diff.sqrMagnitude;
                    if (sq < min * min && sq > 1e-6f)
                    {
                        var push = diff.normalized * (min - Mathf.Sqrt(sq)) * 0.5f;
                        ea.pos += push; eb.pos -= push;
                    }
                }
            }

            foreach (var e in enemies)
            {
                if (e.dead) continue;
                Sync(e);
                if (e.flash <= 0f && e.mark != Elem.None) e.sr.color = Color.Lerp(Color.white, ElemColor[(int)e.mark], 0.55f);
                else if (e.flash <= 0f) e.sr.color = Color.white;
            }
        }

        void Sync(Unit u)
        {
            var sp = ToScreen(u.pos);
            u.sr.transform.position = sp;
            u.sr.sortingOrder = 5000 - Mathf.RoundToInt(sp.y * 100f);
            u.sr.sprite = u.flash > 0f ? u.white : u.normal;
        }

        IEnumerable<Enemy> Around(Vector2 c, float r)
        {
            float rr = r * r;
            foreach (var e in enemies) if (!e.dead && (e.pos - c).sqrMagnitude <= rr) yield return e;
        }

        List<Enemy> Closest(Vector2 c, float r, int n)
        {
            var list = new List<Enemy>(Around(c, r));
            list.Sort((x, y) => (x.pos - c).sqrMagnitude.CompareTo((y.pos - c).sqrMagnitude));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        Enemy Nearest(Vector2 c, float r, HashSet<Enemy> exclude)
        {
            Enemy best = null; float bd = r * r;
            foreach (var e in enemies)
            {
                if (e.dead || (exclude != null && exclude.Contains(e))) continue;
                float d = (e.pos - c).sqrMagnitude;
                if (d <= bd) { bd = d; best = e; }
            }
            return best;
        }

        // ───────────── 부패 구름 ─────────────
        void UpdateZones(float dt)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                z.t += dt; z.tick -= dt;
                if (z.tick <= 0f)
                {
                    z.tick = 0.5f;
                    processingDepth = 0;
                    foreach (var e in new List<Enemy>(Around(z.pos, z.r))) Hit(e, skills[3].dmg, Elem.Rot, false);
                }
                z.sr.color = new Color(0.45f, 0.85f, 0.25f, 0.35f * Mathf.Clamp01((z.life - z.t) * 2f));
                if (z.t >= z.life) { Destroy(z.sr.gameObject); zones.RemoveAt(i); }
            }
        }

        // ───────────── 루트 ─────────────
        void RollLoot(Enemy e)
        {
            int drops = e.elite ? 3 : (Random.value < 0.12f ? 1 : 0);
            for (int k = 0; k < drops; k++)
            {
                float r = Random.value * 100f;
                int rarity = r < 0.6f ? 4 : r < 4f ? 3 : r < 16f ? 2 : r < 42f ? 1 : 0;
                if (e.elite && k == 0) rarity = Mathf.Max(rarity, 2);
                if (rarity == 0) { AddText(e.pos, "+재료", new Color(0.6f, 0.6f, 0.6f), 0, 0.5f); continue; } // 필터: 일반은 자동 분해
                var l = new Loot { pos = e.pos + Random.insideUnitCircle * 0.3f, rarity = rarity, vh = 1.6f };
                l.sr = NewSprite("Loot", pixSpr, 0);
                l.sr.color = RarityColor[rarity];
                l.sr.transform.localScale = Vector3.one * 2f;
                if (rarity >= 3)
                {
                    l.beam = NewSprite("Beam", beamSpr, 0);
                    l.beam.color = RarityColor[rarity];
                    l.beam.transform.localScale = new Vector3(1f, 1.5f, 1f);
                }
                loot.Add(l);
            }
        }

        void UpdateLoot(float dt)
        {
            for (int i = loot.Count - 1; i >= 0; i--)
            {
                var l = loot[i];
                l.t += dt;
                if (l.t < 0.5f) { l.h += l.vh * dt; l.vh -= 6f * dt; if (l.h < 0f) { l.h = 0f; l.vh = 0f; } }
                else if (l.t > 0.9f)
                {
                    var to = hero.pos - l.pos;
                    if (to.magnitude < 0.3f)
                    {
                        if (l.rarity == 3) { legendaryCount++; Toast("전설 획득!"); Hitstop(0.06f); shake = Mathf.Max(shake, 0.4f); }
                        if (l.rarity == 4) { uniqueCount++; Toast("고유 획득!!"); Hitstop(0.1f); shake = Mathf.Max(shake, 0.6f); }
                        Destroy(l.sr.gameObject);
                        if (l.beam) Destroy(l.beam.gameObject);
                        loot.RemoveAt(i);
                        continue;
                    }
                    l.pos += to.normalized * 6f * dt;
                }
                var sp = ToScreen(l.pos) + Vector3.up * l.h * 0.5f;
                l.sr.transform.position = sp;
                l.sr.sortingOrder = 5000 - Mathf.RoundToInt(sp.y * 100f) + 1;
                if (l.beam)
                {
                    l.beam.transform.position = ToScreen(l.pos);
                    l.beam.sortingOrder = 30001;
                    var c = RarityColor[l.rarity];
                    l.beam.color = new Color(c.r, c.g, c.b, 0.55f + 0.35f * Mathf.Sin(Time.time * 8f));
                }
            }
        }

        // ───────────── 이펙트 ─────────────
        void RingFx(Vector2 g, float r, Color c, float life)
        {
            var f = new Fx { sr = NewSprite("Ring", ringSpr, 20000), life = life, s0 = 0.707f * r * 0.4f, s1 = 0.707f * r, c = c };
            f.sr.transform.position = ToScreen(g);
            fx.Add(f);
        }

        void Burst(Vector3 sp, Color c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var f = new Fx
                {
                    sr = NewSprite("P", pixSpr, 20001), life = Random.Range(0.35f, 0.7f), s0 = 1.5f, s1 = 1f, c = c, gravity = true,
                    vel = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.8f, 2.6f), 0f),
                };
                f.sr.transform.position = sp;
                fx.Add(f);
            }
        }

        void DotLine(Vector2 a, Vector2 b, Color c)
        {
            Vector3 sa = ToScreen(a) + Vector3.up * 0.25f, sb = ToScreen(b) + Vector3.up * 0.25f;
            int n = Mathf.Max(2, Mathf.CeilToInt((sb - sa).magnitude / 0.08f));
            for (int i = 0; i <= n; i++)
            {
                var f = new Fx { sr = NewSprite("Z", pixSpr, 20002), life = 0.12f, s0 = 1f, s1 = 1f, c = c };
                f.sr.transform.position = Vector3.Lerp(sa, sb, i / (float)n) + (Vector3)(Random.insideUnitCircle * 0.04f);
                fx.Add(f);
            }
        }

        void UpdateFx(float dt)
        {
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.t += dt;
                float k = f.t / f.life;
                if (k >= 1f) { Destroy(f.sr.gameObject); fx.RemoveAt(i); continue; }
                if (f.gravity) { f.vel.y -= 7f * dt; f.sr.transform.position += f.vel * dt; }
                f.sr.transform.localScale = Vector3.one * Mathf.Lerp(f.s0, f.s1, k);
                f.sr.color = new Color(f.c.r, f.c.g, f.c.b, 1f - k * k);
            }
        }

        void Hitstop(float sec)
        {
            if (Time.unscaledTime - lastHitstop < 0.25f) return;
            lastHitstop = Time.unscaledTime;
            hitstopUntil = Time.unscaledTime + sec;
        }

        void AddText(Vector2 g, string s, Color c, int size, float life)
        {
            if (texts.Count > 60) texts.RemoveAt(0);
            texts.Add(new FloatText { w = ToScreen(g) + Vector3.up * 0.5f, s = s, c = c, size = size, life = life });
        }

        void Toast(string s) { toast = s; toastUntil = Time.unscaledTime + 1.6f; }

        // ───────────── HUD (스파이크용 IMGUI) ─────────────
        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 288f);
            if (style == null)
            {
                var font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
                style = new GUIStyle(GUI.skin.label) { font = font, richText = true };
                bigStyle = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            style.fontSize = Mathf.RoundToInt(9 * scale);
            bigStyle.fontSize = Mathf.RoundToInt(16 * scale);

            foreach (var t in texts)
            {
                var sp = cam.WorldToScreenPoint(t.w + Vector3.up * t.t * 0.6f);
                var st = new GUIStyle(bigStyle) { fontSize = Mathf.RoundToInt((t.size == 1 ? 10 : 7) * scale) };
                st.normal.textColor = new Color(t.c.r, t.c.g, t.c.b, 1f - t.t / t.life);
                GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 10, 120, 20 * scale), t.s, st);
            }

            var hsp = cam.WorldToScreenPoint(ToScreen(hero.pos) + Vector3.up * 0.75f);
            float bw = 30 * scale, bh = 3 * scale;
            var hr = new Rect(hsp.x - bw / 2, Screen.height - hsp.y, bw, bh);
            GUI.DrawTexture(hr, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0, 0, 0, 0.7f), 0, 0);
            GUI.DrawTexture(new Rect(hr.x, hr.y, bw * Mathf.Clamp01(hero.hp / hero.maxHp), bh), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0.85f, 0.15f, 0.15f), 0, 0);

            float kpm = killTimes.Count * 60f / Mathf.Clamp(Time.time - 0f, 1f, 60f);
            string line1 = $"방 {room}{(hordeRoom ? " 호드" : "")} · 처치/분 {kpm:0} · 최고 연속 ×{maxStreak} · 최대 연쇄 {biggestChain} · 최근 클리어 {lastClearSec:0.0}초";
            string line2 = $"[{cfgName}] <color=#ffd27a>{heroMode}</color> · 몰이 목표 {gatherThreshold} · 사망 전파 {(propagate ? "켜짐" : "꺼짐")} · 전설 {legendaryCount} · 고유 {uniqueCount} · 쓰러짐 {deaths}";
            string line3 = "";
            for (int i = 0; i < reactions.Length; i++) line3 += $"{ReactionNames[i]} {reactions[i]}  ";
            GUI.Label(new Rect(4, 2, Screen.width, 14 * scale), line1, style);
            GUI.Label(new Rect(4, 2 + 11 * scale, Screen.width, 14 * scale), line2, style);
            GUI.Label(new Rect(4, 2 + 22 * scale, Screen.width, 14 * scale), line3, style);

            var order = Orders[orderPreset];
            float sw = 52 * scale, sh = 14 * scale, sx = Screen.width / 2f - sw * 2 - 4 * scale, sy = Screen.height - sh - 4 * scale;
            for (int slot = 0; slot < 4; slot++)
            {
                var s = skills[order[slot]];
                var rect = new Rect(sx + slot * (sw + 2 * scale), sy, sw, sh);
                var c = ElemColor[(int)s.elem];
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0, 0, 0, 0.75f), 0, 0);
                float ready = 1f - Mathf.Clamp01(s.timer / s.cd);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2 * scale, sw * ready, 2 * scale), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, c, 0, 0);
                bool linked = slot == linkSlot || slot == (linkSlot + 1) % 4 || slot == (linkSlot + 3) % 4;
                var st = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(7 * scale) };
                st.normal.textColor = c;
                GUI.Label(rect, (slot == linkSlot ? "◆" : linked ? "◇" : "") + s.name, st);
            }

            if (streak >= 10)
            {
                var st = new GUIStyle(bigStyle);
                st.normal.textColor = new Color(1f, 0.85f, 0.3f);
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 12f);
                st.fontSize = Mathf.RoundToInt(14 * scale * pulse);
                GUI.Label(new Rect(Screen.width - 110 * scale, 40 * scale, 100 * scale, 24 * scale), $"×{streak} 학살", st);
            }
            if (Time.unscaledTime < toastUntil)
            {
                var st = new GUIStyle(bigStyle);
                st.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.22f, Screen.width, 24 * scale), toast, st);
            }
        }
    }
}
