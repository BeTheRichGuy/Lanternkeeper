using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lanternkeeper.View;
using UnityEditor;
using UnityEngine;

namespace Lanternkeeper.EditorTools
{
    /// <summary>
    /// Copies PixelLab exports from <c>art/pixellab</c> (outside Assets) into <c>Assets/_Game/Art/PixelLab</c>,
    /// lets <see cref="PixelArtPostprocessor"/> apply pixel-art import settings, and fills <see cref="SpriteCatalog"/>.
    /// Layout of the source is documented in <c>art/pixellab/ASSETS.md</c>.
    /// </summary>
    public static class ArtImporter
    {
        public const string CatalogPath = "Assets/_Game/Data/SpriteCatalog.asset";
        public const string Dest = "Assets/_Game/Art/PixelLab";
        public static readonly string[] Dirs = { "south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west" };
        static readonly string[] Anims = { "idle", "walk", "attack", "hurt", "death" };
        static readonly HashSet<string> LoopAnims = new HashSet<string> { "idle", "walk" };

        /// <summary>Catalog key → PixelLab character folder. Hero variants are 희승's picks (ASSETS.md, 2026-11-19).</summary>
        public static readonly (string key, string src)[] Characters =
        {
            ("hero/ashknight",  "characters/ember_knight/B_lantern"),
            ("hero/oathknight", "characters/oath_knight/E_unveiled"), // 11-24: face under a mourning hood, unlike the ash knight's closed visor
            ("hero/windhunter", "characters/wind_hunter/A_hooded_ranger"),
            ("hero/starseer",   "characters/star_seer/A_astrologer"),
            ("enemy/villager",  "characters/monster/ashen_villager"),
            ("enemy/skeleton",  "characters/monster/skeleton_soldier"),
            ("enemy/archer",    "characters/monster/skeleton_archer"),
            ("enemy/ghoul",     "characters/monster/crypt_ghoul"),
            ("enemy/wraith",    "characters/monster/fog_wraith"),
            ("enemy/brute",     "characters/monster/gravedigger_brute"),
            ("enemy/gravelord", "characters/boss/tomb_lord"),
            ("enemy/grave_hound",       "characters/monster/grave_hound"),
            ("enemy/candle_priest",     "characters/monster/candle_priest"),
            ("enemy/bone_shieldbearer", "characters/monster/bone_shieldbearer"),
            ("enemy/plague_rat_swarm",  "characters/monster/plague_rat_swarm"),
            // 막 II 가라앉은 수도원 (act2-assets)
            ("enemy/drowned_monk",   "characters/monster/drowned_monk"),
            ("enemy/mire_eel",       "characters/monster/mire_eel"),
            ("enemy/choir_wraith",   "characters/monster/choir_wraith"),
            ("enemy/penitent",       "characters/monster/penitent"),
            ("enemy/tide_acolyte",   "characters/monster/tide_acolyte"),
            ("enemy/bell_ringer",    "characters/monster/bell_ringer"),
            ("enemy/abbey_guardian", "characters/monster/abbey_guardian"),
            ("enemy/drowned_abbot",  "characters/boss/drowned_abbot"),
            // 막 III 균사 왕국 (act3-assets)
            ("enemy/spore_thrall",      "characters/monster/spore_thrall"),
            ("enemy/shroom_crawler",    "characters/monster/shroom_crawler"),
            ("enemy/mycelium_weaver",   "characters/monster/mycelium_weaver"),
            ("enemy/bloated_carrier",   "characters/monster/bloated_carrier"),
            ("enemy/fungal_stalker",    "characters/monster/fungal_stalker"),
            ("enemy/lantern_moth",      "characters/monster/lantern_moth"),
            ("enemy/mycelial_colossus", "characters/monster/mycelial_colossus"),
            ("enemy/spore_queen",       "characters/boss/spore_queen"),
            // 막 IV 녹은 대장간 (act4-assets)
            ("enemy/slag_worker",    "characters/monster/slag_worker"),
            ("enemy/ember_imp",      "characters/monster/ember_imp"),
            ("enemy/bellows_priest", "characters/monster/bellows_priest"),
            ("enemy/iron_golem",     "characters/monster/iron_golem"),
            ("enemy/forge_hound",    "characters/monster/forge_hound"),
            ("enemy/ash_wisp",       "characters/monster/ash_wisp"),
            ("enemy/guild_enforcer", "characters/monster/guild_enforcer"),
            ("enemy/molten_giant",   "characters/boss/molten_giant"),
            // 막 V 별 없는 심연 (act5-assets)
            ("enemy/fallen_pilgrim",    "characters/monster/fallen_pilgrim"),
            ("enemy/void_crawler",      "characters/monster/void_crawler"),
            ("enemy/starless_seer",     "characters/monster/starless_seer"),
            ("enemy/husk_titan",        "characters/monster/husk_titan"),
            ("enemy/abyss_stalker",     "characters/monster/abyss_stalker"),
            ("enemy/star_shard",        "characters/monster/star_shard"),
            ("enemy/ash_herald",        "characters/monster/ash_herald"),
            ("enemy/dead_star_apostle", "characters/boss/dead_star_apostle"),
            // 막 I 모래 무덤 동굴 (sand-assets)
            ("enemy/bandaged_wanderer", "characters/monster/bandaged_wanderer"),
            ("enemy/scarab_swarm", "characters/monster/scarab_swarm"),
            ("enemy/shroud_wraith", "characters/monster/shroud_wraith"),
            ("enemy/sandstone_sentinel", "characters/monster/sandstone_sentinel"),
            ("enemy/sand_wolf", "characters/monster/sand_wolf"),
            ("enemy/sand_lurker", "characters/monster/sand_lurker"),
            ("enemy/bronze_veiled_general", "characters/monster/bronze_veiled_general"),
            ("enemy/sarcophagus_king", "characters/boss/sarcophagus_king"),
            // 막 II 뿌리 밀림 (jungle-assets)
            ("enemy/rootwalker", "characters/monster/rootwalker"),
            ("enemy/vine_hanger", "characters/monster/vine_hanger"),
            ("enemy/flowerface_witch", "characters/monster/flowerface_witch"),
            ("enemy/spore_host", "characters/monster/spore_host"),
            ("enemy/thorn_boar", "characters/monster/thorn_boar"),
            ("enemy/leafmail_hunter", "characters/monster/leafmail_hunter"),
            ("enemy/antler_knight", "characters/monster/antler_knight"),
            ("enemy/crowned_great_tree", "characters/boss/crowned_great_tree"),
            // 막 III 핏덩이 소굴 (flesh-assets)
            ("enemy/flesh_crawler", "characters/monster/flesh_crawler"),
            ("enemy/wailing_brood", "characters/monster/wailing_brood"),
            ("enemy/sinew_summoner", "characters/monster/sinew_summoner"),
            ("enemy/hook_butcher", "characters/monster/hook_butcher"),
            ("enemy/blood_hound", "characters/monster/blood_hound"),
            ("enemy/bone_puppet", "characters/monster/bone_puppet"),
            ("enemy/fleshmail_warden", "characters/monster/fleshmail_warden"),
            ("enemy/pulse_lord", "characters/boss/pulse_lord"),
            // 막 IV 얼음 동굴 (ice-assets)
            ("enemy/frost_wanderer", "characters/monster/frost_wanderer"),
            ("enemy/icicle_spider", "characters/monster/icicle_spider"),
            ("enemy/frozen_choir", "characters/monster/frozen_choir"),
            ("enemy/icemail_soldier", "characters/monster/icemail_soldier"),
            ("enemy/frost_bear", "characters/monster/frost_bear"),
            ("enemy/blizzard_wraith", "characters/monster/blizzard_wraith"),
            ("enemy/frost_lord_knight", "characters/monster/frost_lord_knight"),
            ("enemy/giant_in_ice", "characters/boss/giant_in_ice"),
        };

        /// <summary>Monsters and the boss ship 5 directions; W, SW, NW reuse E, SE, NE flipped (ASSETS.md).</summary>
        static readonly int[] MirrorOf = { -1, -1, -1, -1, -1, 3, 2, 1 }; // index in Dirs → source index

        const string AudioDest = "Assets/_Game/Resources/Audio";
        static string SourceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/pixellab"));

        [MenuItem("Lanternkeeper/Import PixelLab Art")]
        public static string Import()
        {
            var log = new List<string>();
            var plan = new List<(string key, string[] assetPaths, int frames, int mirror)>();
            var vfxMeta = new Dictionary<string, (float fps, bool loop)>();
            var audioPaths = new List<(string kind, string path)>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var (key, src) in Characters)
                {
                    var root = Path.Combine(SourceRoot, src, "Idle");
                    var rot = Path.Combine(root, "rotations");
                    if (!Directory.Exists(rot)) { log.Add($"skip {key}: no rotations"); continue; }
                    plan.Add((key, Dirs.Select(d => CopyIn(Path.Combine(rot, d + ".png"), $"{Dest}/{key}/rot/{d}.png")).ToArray(), 0, 0));
                    var anims = new List<string>();
                    foreach (var anim in Anims)
                    {
                        var animDir = Path.Combine(root, "animations", anim);
                        if (!Directory.Exists(animDir)) continue;
                        var perDir = Dirs.Select(d => Directory.Exists(Path.Combine(animDir, d))
                            ? Directory.GetFiles(Path.Combine(animDir, d), "frame_*.png").OrderBy(f => f).ToList()
                            : new List<string>()).ToList();
                        int mirror = 0;
                        for (int d = 0; d < Dirs.Length; d++)
                            if (perDir[d].Count == 0 && MirrorOf[d] >= 0 && perDir[MirrorOf[d]].Count > 0) mirror |= 1 << d;
                        int n = Enumerable.Range(0, Dirs.Length).Where(d => (mirror & (1 << d)) == 0).Min(d => perDir[d].Count);
                        if (n == 0) continue;
                        // v3 animations (padded canvas) start with the still pose; drop it from loops for a seamless cycle.
                        bool skipFirst = LoopAnims.Contains(anim) && n > 1 && PngSize(perDir[0][0]).w > PngSize(Path.Combine(rot, "south.png")).w;
                        int start = skipFirst ? 1 : 0;
                        var copied = new string[Dirs.Length][];
                        for (int d = 0; d < Dirs.Length; d++)
                        {
                            if ((mirror & (1 << d)) != 0) continue;
                            copied[d] = new string[n - start];
                            for (int f = start; f < n; f++)
                                copied[d][f - start] = CopyIn(perDir[d][f], $"{Dest}/{key}/anim/{anim}/{Dirs[d]}/{Path.GetFileName(perDir[d][f])}");
                        }
                        var paths = new List<string>();
                        for (int d = 0; d < Dirs.Length; d++)
                            paths.AddRange((mirror & (1 << d)) != 0 ? copied[MirrorOf[d]] : copied[d]);
                        plan.Add(($"{key}/{anim}", paths.ToArray(), n - start, mirror));
                        anims.Add($"{anim}×{n - start}{(mirror != 0 ? "(반전)" : "")}");
                    }
                    log.Add($"{key} ← {src} [{string.Join(", ", anims)}]");
                }

                var floorDir = Path.Combine(SourceRoot, "tiles/crypt_floor");
                if (Directory.Exists(floorDir))
                {
                    var files = Directory.GetFiles(floorDir, "tile_*.png")
                        .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Substring(5))).ToArray();
                    plan.Add(("tile/floor", files.Select(f => CopyIn(f, $"{Dest}/tile/floor/{Path.GetFileName(f)}")).ToArray(), 0, 0));
                    log.Add($"tile/floor ← {files.Length}");
                }
                foreach (var (folder, key) in new[] { ("tiles/monastery_floor", "tile2"), ("tiles/fungal_floor", "tile3"), ("tiles/forge_floor", "tile4"),
                    ("tiles/abyss_floor", "tile5"), ("tiles/sand_floor", "tile_sand"), ("tiles/jungle_floor", "tile_jungle"),
                    ("tiles/flesh_floor", "tile_flesh"), ("tiles/ice_floor", "tile_ice") })
                {
                    var dir = Path.Combine(SourceRoot, folder);
                    if (!Directory.Exists(dir)) continue;
                    var files = Directory.GetFiles(dir, "tile_*.png")
                        .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Substring(5))).ToArray();
                    if (files.Length == 0) continue;
                    plan.Add(($"{key}/floor", files.Select(f => CopyIn(f, $"{Dest}/{key}/floor/{Path.GetFileName(f)}")).ToArray(), 0, 0));
                    log.Add($"{key}/floor <- {files.Length}");
                }
                var block = Path.Combine(SourceRoot, "tiles/crypt_block.png");
                if (File.Exists(block)) plan.Add(("tile/wall", new[] { CopyIn(block, $"{Dest}/tile/wall/crypt_block.png") }, 0, 0));

                // 갈림길의 상인 (M2 event room): one standing pose is enough for a prop.
                var merchant = Path.Combine(SourceRoot, "characters/npc/wandering_merchant/Idle/rotations/south-east.png");
                if (File.Exists(merchant)) plan.Add(("prop/merchant", new[] { CopyIn(merchant, $"{Dest}/prop/merchant.png") }, 0, 0));

                // VFX game packs (ASSETS.md VFX 표): each clip's sheet is cut into frames whose pivot is the effect anchor.
                var vfxRoot = Path.Combine(SourceRoot, "vfx");
                if (Directory.Exists(vfxRoot))
                    foreach (var dir in Directory.GetDirectories(vfxRoot).OrderBy(d => d))
                    {
                        var manifestPath = Path.Combine(dir, "game", "manifest.json");
                        if (!File.Exists(manifestPath)) continue;
                        string id = Path.GetFileName(dir);
                        var m = JsonUtility.FromJson<VfxManifest>(File.ReadAllText(manifestPath));
                        if (m?.clips == null) continue;
                        bool layered = m.clips.Any(c => c.name == "front");
                        foreach (var clip in m.clips)
                        {
                            if (layered ? clip.name != "front" && clip.name != "behind" : clip.name != "whole") continue;
                            var paths = SliceClip(Path.Combine(dir, "game"), clip, $"{Dest}/vfx/{id}/{clip.name}");
                            if (paths.Count == 0) continue;
                            plan.Add(($"vfx/{id}/{clip.name}", paths.ToArray(), paths.Count, 0));
                            vfxMeta[$"vfx/{id}/{clip.name}"] = (m.fps > 0 ? m.fps : 12f, clip.loop || m.loop);
                        }
                        log.Add($"vfx/{id}");
                    }

                // Organic terrain kit (ASSETS.md "유기 지형 키트"): wall pieces by neighbour shape, floor decals.
                foreach (var (kind, prefix) in new[] { ("props", "prop"), ("icons", "icon"), ("tiles/organic", "wall"), ("decals", "decal"), ("ui", "ui"), ("ui/buff_icons", "buff"), ("portraits", "portrait"),
                    ("tiles/organic_monastery", "wall2"), ("props/monastery", "prop2"), ("decals/monastery", "decal2"),
                    ("tiles/organic_fungal", "wall3"), ("props/fungal", "prop3"), ("decals/fungal", "decal3"),
                    ("tiles/organic_forge", "wall4"), ("props/forge", "prop4"), ("decals/forge", "decal4"),
                    ("tiles/organic_abyss", "wall5"), ("props/abyss", "prop5"), ("decals/abyss", "decal5"),
                    ("tiles/organic_sand", "wall_sand"), ("props/sand", "prop_sand"), ("decals/sand", "decal_sand"),
                    ("tiles/organic_jungle", "wall_jungle"), ("props/jungle", "prop_jungle"), ("decals/jungle", "decal_jungle"),
                    ("tiles/organic_flesh", "wall_flesh"), ("props/flesh", "prop_flesh"), ("decals/flesh", "decal_flesh"),
                    ("tiles/organic_ice", "wall_ice"), ("props/ice", "prop_ice"), ("decals/ice", "decal_ice") })
                {
                    var dir = Path.Combine(SourceRoot, kind);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "*.png"))
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        plan.Add(($"{prefix}/{name}", new[] { CopyIn(f, $"{Dest}/{prefix}/{name}.png") }, 0, 0));
                        log.Add($"{prefix}/{name}");
                    }
                }

                // GDD §18 소리 (art/audio from audio-assets): Resources/Audio/{sfx,ambient,music}/<id>.<ext>.
                var audioRoot = Path.GetFullPath(Path.Combine(SourceRoot, "../audio"));
                foreach (var kind in new[] { "sfx", "ambient", "music" })
                {
                    var dir = Path.Combine(audioRoot, kind);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir))
                    {
                        var ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".wav" && ext != ".ogg" && ext != ".mp3") continue;
                        audioPaths.Add((kind, CopyIn(f, $"{AudioDest}/{kind}/{Path.GetFileName(f)}")));
                        log.Add($"audio/{kind}/{Path.GetFileNameWithoutExtension(f)}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Short effects decompress on load (they fire dozens of times a second); loops stream.
            foreach (var (kind, path) in audioPaths)
            {
                if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) continue;
                var set = ai.defaultSampleSettings;
                bool sfx = kind == "sfx";
                set.loadType = sfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
                set.compressionFormat = AudioCompressionFormat.Vorbis;
                set.quality = sfx ? 0.7f : 0.6f;
                ai.defaultSampleSettings = set;
                ai.forceToMono = sfx;
                ai.SaveAndReimport();
            }

            var catalog = LoadOrCreateCatalog();
            foreach (var (key, paths, frames, mirror) in plan)
            {
                var sprites = paths.Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
                catalog.entries.RemoveAll(e => e.key == key);
                var entry = new SpriteCatalog.Entry { key = key, sprites = sprites, frames = frames, fps = 10f, mirrorMask = mirror };
                if (vfxMeta.TryGetValue(key, out var vm)) { entry.fps = vm.fps; entry.loop = vm.loop; }
                catalog.entries.Add(entry);
                int missing = sprites.Count(s => s == null);
                if (missing > 0) log.Add($"WARN {key}: {missing} sprites failed to load");
            }
            // Drop entries whose art no longer exists (e.g. an old hero variant).
            catalog.entries.RemoveAll(e => e.sprites == null || e.sprites.All(s => s == null));
            catalog.Invalidate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        public static SpriteCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<SpriteCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        static Dictionary<string, Vector2> _kitPivots;

        /// <summary>Per-file pivots from <c>art/pixellab/organic_kit.json</c> (walls, terrain props, decals).</summary>
        public static bool KitPivot(string fileName, out Vector2 pivot)
        {
            if (_kitPivots == null)
            {
                _kitPivots = new Dictionary<string, Vector2>();
                // organic_kit.json first, then every act kit (monastery, fungal, forge, abyss, sand, jungle, flesh, ice, ...).
                var kits = new List<string> { Path.Combine(SourceRoot, "organic_kit.json") };
                if (Directory.Exists(SourceRoot))
                    kits.AddRange(Directory.GetFiles(SourceRoot, "*_kit.json").Where(p => !p.EndsWith("organic_kit.json")).OrderBy(p => p));
                foreach (var json in kits)
                {
                    if (!File.Exists(json)) continue;
                    var rx = new System.Text.RegularExpressions.Regex(
                        "\"([A-Za-z0-9_]+\\.png)\"\\s*:\\s*\\{[^{}]*?\"pivot\"\\s*:\\s*\\[\\s*([-0-9.]+)\\s*,\\s*([-0-9.]+)\\s*\\]");
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(File.ReadAllText(json)))
                        _kitPivots[m.Groups[1].Value] = new Vector2(float.Parse(m.Groups[2].Value, inv), float.Parse(m.Groups[3].Value, inv));
                }
            }
            return _kitPivots.TryGetValue(fileName, out pivot);
        }

        // ---------- VFX game packs ----------

        [System.Serializable] sealed class VfxManifest { public float fps; public bool loop; public VfxClip[] clips; }
        [System.Serializable] sealed class VfxClip { public string name, depth; public bool loop; public int frames, start_frame; public VfxSize frame; public VfxPoint offset; public VfxSheet[] sheets; }
        [System.Serializable] sealed class VfxSize { public int w, h; }
        [System.Serializable] sealed class VfxPoint { public int x, y; }
        [System.Serializable] sealed class VfxSheet { public string file; public int first_frame, frames, columns, rows; }

        /// <summary>
        /// Cut a clip's sheets into one PNG per frame. The manifest draws a frame at anchor + offset, so the anchor sits at
        /// (−offset.x, −offset.y) from the frame's top-left: that point becomes the sprite pivot, encoded in the file name
        /// (…_p&lt;x&gt;_&lt;y&gt;.png, value×10000+50000) so a later reimport still knows it.
        /// </summary>
        static List<string> SliceClip(string gameDir, VfxClip clip, string destDir)
        {
            var paths = new List<string>();
            if (clip.sheets == null || clip.frame == null || clip.offset == null || clip.frame.w <= 0 || clip.frame.h <= 0) return paths;
            int w = clip.frame.w, h = clip.frame.h;
            // Pad to multiples of 4 (transparent, on the right and top) so the frames can be BC7-compressed; the anchor
            // keeps its pixel distance from the left and bottom edges.
            int w4 = (w + 3) / 4 * 4, h4 = (h + 3) / 4 * 4;
            int px = Mathf.RoundToInt(-clip.offset.x / (float)w4 * 10000f) + 50000;
            int py = Mathf.RoundToInt((h + clip.offset.y) / (float)h4 * 10000f) + 50000;
            Directory.CreateDirectory(destDir);
            int index = 0;
            // A layer that starts later than the effect (start_frame, e.g. the part behind the hero coming in at frame 3)
            // waits in empty frames, so front and behind play in step (11-23: v2 packs start layers at different frames).
            if (clip.start_frame > 0)
            {
                var blank = new Texture2D(w4, h4, TextureFormat.RGBA32, false);
                blank.SetPixels32(new Color32[w4 * h4]);
                var blankBytes = blank.EncodeToPNG();
                Object.DestroyImmediate(blank);
                for (int f = 0; f < clip.start_frame; f++)
                {
                    var path = $"{destDir}/f{index:000}_p{px}_{py}.png";
                    if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(blankBytes)) File.WriteAllBytes(path, blankBytes);
                    paths.Add(path);
                    index++;
                }
            }
            foreach (var sheet in clip.sheets)
            {
                var file = Path.Combine(gameDir, sheet.file);
                if (!File.Exists(file)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(file))) { Object.DestroyImmediate(tex); continue; }
                for (int f = 0; f < sheet.frames; f++)
                {
                    int col = f % Mathf.Max(1, sheet.columns), row = f / Mathf.Max(1, sheet.columns);
                    int x = col * w, y = tex.height - (row + 1) * h;
                    if (x + w > tex.width || y < 0) break;
                    var frame = new Texture2D(w4, h4, TextureFormat.RGBA32, false);
                    frame.SetPixels32(new Color32[w4 * h4]);
                    frame.SetPixels(0, 0, w, h, tex.GetPixels(x, y, w, h));
                    var bytes = frame.EncodeToPNG();
                    Object.DestroyImmediate(frame);
                    var path = $"{destDir}/f{index:000}_p{px}_{py}.png";
                    if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
                    paths.Add(path);
                    index++;
                }
                Object.DestroyImmediate(tex);
            }
            // Drop frames left over from an older, longer version of the clip.
            foreach (var old in Directory.GetFiles(destDir, "f*.png"))
                if (!paths.Contains(old.Replace(Path.DirectorySeparatorChar, '/'))) { File.Delete(old); File.Delete(old + ".meta"); }
            return paths;
        }

        /// <summary>Pivot stored in a sliced VFX frame's file name by <see cref="SliceClip"/>.</summary>
        public static bool VfxPivot(string path, out Vector2 pivot)
        {
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(path), @"_p(\d+)_(\d+)$");
            pivot = m.Success ? new Vector2((int.Parse(m.Groups[1].Value) - 50000) / 10000f, (int.Parse(m.Groups[2].Value) - 50000) / 10000f) : default;
            return m.Success;
        }

        static Dictionary<string, Vector4> _kitBorders;

        /// <summary>Unity order (left, bottom, right, top) from the kit's (left, top, right, bottom).</summary>
        public static bool KitBorder(string fileName, out Vector4 border)
        {
            if (_kitBorders == null)
            {
                _kitBorders = new Dictionary<string, Vector4>();
                var json = Path.Combine(SourceRoot, "m2_kit.json");
                if (File.Exists(json))
                {
                    var rx = new System.Text.RegularExpressions.Regex(
                        @"""([A-Za-z0-9_]+\.png)""\s*:\s*\{[^{}]*?""border""\s*:\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]");
                    foreach (System.Text.RegularExpressions.Match m in rx.Matches(File.ReadAllText(json)))
                    {
                        int l = int.Parse(m.Groups[2].Value), t = int.Parse(m.Groups[3].Value), r = int.Parse(m.Groups[4].Value), b = int.Parse(m.Groups[5].Value);
                        _kitBorders[m.Groups[1].Value] = new Vector4(l, b, r, t);
                    }
                }
            }
            return _kitBorders.TryGetValue(fileName, out border);
        }

        static string CopyIn(string sourceFile, string assetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var dst = new FileInfo(assetPath);
            var src = new FileInfo(sourceFile);
            if (!dst.Exists || dst.Length != src.Length || dst.LastWriteTimeUtc < src.LastWriteTimeUtc)
                File.Copy(sourceFile, assetPath, true);
            return assetPath;
        }

        /// <summary>PNG width/height from the IHDR chunk (bytes 16..23, big-endian).</summary>
        public static (int w, int h) PngSize(string file)
        {
            using (var s = File.OpenRead(file))
            {
                var b = new byte[24];
                if (s.Read(b, 0, 24) < 24) return (0, 0);
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return (w, h);
            }
        }
    }

    /// <summary>
    /// Pixel-art import settings for everything under <see cref="ArtImporter.Dest"/>: sprite, PPU 64, point filter,
    /// uncompressed, no mips, and a pivot chosen from the path (feet for characters, centre for tiles and icons).
    /// </summary>
    public sealed class PixelArtPostprocessor : AssetPostprocessor
    {
        const float FootAt64 = 0.12f; // feet sit 12% up a 64 px character canvas

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtImporter.Dest + "/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = IsoProjection.PPU;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            // Effect and character frames are most of the build (7,000+ sheets): BC7 high quality keeps pixel art
            // crisp at a quarter of the size. Tiles, walls, UI and icons stay uncompressed.
            var relPath = assetPath.Substring(ArtImporter.Dest.Length + 1);
            bool frames = relPath.StartsWith("vfx/") || relPath.StartsWith("hero/") || relPath.StartsWith("enemy/");
            imp.textureCompression = frames ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePivot = PivotFor(assetPath);
            // 9-slice borders for the gothic UI kit (m2_kit.json "border": left, top, right, bottom).
            if (assetPath.Contains("/ui/") && ArtImporter.KitBorder(Path.GetFileName(assetPath), out var border)) settings.spriteBorder = border;
            imp.SetTextureSettings(settings);
        }

        static Vector2 PivotFor(string path)
        {
            var rel = path.Substring(ArtImporter.Dest.Length + 1);
            if (rel.StartsWith("vfx/") && ArtImporter.VfxPivot(path, out var vp)) return vp;
            // Act II kits (wall2/, prop2/, decal2/) share the organic kit's file names and pivot table.
            // Every act's kit (wall/, wall2/ ... wall_sand/, prop_ice/, decal_flesh/) shares the kit pivot tables by file name.
            string family = rel.Substring(0, System.Math.Max(0, rel.IndexOf('/')));
            bool kitFamily = family.StartsWith("wall") || family.StartsWith("decal") || family.StartsWith("prop");
            if (kitFamily && ArtImporter.KitPivot(Path.GetFileName(path), out var kit)) return kit;
            if (rel.StartsWith("tile/wall")) return new Vector2(0.5f, 0.25f);
            if (family.StartsWith("tile")) return new Vector2(0.5f, 0.5f);
            if (rel.StartsWith("ui/") || rel.StartsWith("buff/") || rel.StartsWith("portrait/")) return new Vector2(0.5f, 0.5f);
            if (rel.StartsWith("tile/")) return new Vector2(0.5f, 0.5f);
            if (rel.StartsWith("icon/")) return new Vector2(0.5f, 0.5f);
            if (family.StartsWith("prop")) return new Vector2(0.5f, 0.12f);
            if (rel.Contains("/anim/"))
            {
                // Padded v3 canvas: the figure (64 px, or 128 px for the boss) is centred with (S - base) / 2 px
                // added on every side (ASSETS.md), so the feet move up by that padding.
                int s = ArtImporter.PngSize(path).w;
                int baseSize = s >= 128 ? 128 : 64;
                if (s > baseSize) return new Vector2(0.5f, (FootAt64 * baseSize + (s - baseSize) / 2f) / s);
            }
            return new Vector2(0.5f, FootAt64);
        }
    }
}
