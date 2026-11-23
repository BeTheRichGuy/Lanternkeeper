using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Player comfort options (docs/research/d4-visual-techniques.md §6.7, arpg-ui-ux-patterns.md): screen shake, hit
    /// flash, effect strength, fullscreen. Kept in PlayerPrefs; the view reads them every frame.
    /// </summary>
    public static class GameSettings
    {
        static bool _loaded;
        static bool _shake = true, _hitFlash = true, _notices = true, _blood = true;
        static int _fxLevel; // 0 full, 1 70%, 2 40%
        static int _echoLevel; // 재기획 §4.7 분신·메아리: 0 100%, 1 60%, 2 30%, 3 hidden

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _shake = PlayerPrefs.GetInt("set_shake", 1) == 1;
            _hitFlash = PlayerPrefs.GetInt("set_hitflash", 1) == 1;
            _notices = PlayerPrefs.GetInt("set_notices", 1) == 1;
            _blood = PlayerPrefs.GetInt("set_blood", 1) == 1;
            _fxLevel = Mathf.Clamp(PlayerPrefs.GetInt("set_fx", 0), 0, 2);
            _echoLevel = Mathf.Clamp(PlayerPrefs.GetInt("set_echo", 0), 0, 3);
        }

        public static bool Shake { get { Load(); return _shake; } }
        public static bool HitFlash { get { Load(); return _hitFlash; } }
        /// <summary>Top-left notices on screen (the journal keeps them either way).</summary>
        public static bool Notices { get { Load(); return _notices; } }
        public static void ToggleNotices() { Load(); _notices = !_notices; PlayerPrefs.SetInt("set_notices", _notices ? 1 : 0); }
        public static int FxLevel { get { Load(); return _fxLevel; } }
        /// <summary>고어 (희승 11-22): blood bursting from the fallen and stains on the floor. On by default.</summary>
        public static bool Blood { get { Load(); return _blood; } }
        public static void ToggleBlood() { Load(); _blood = !_blood; PlayerPrefs.SetInt("set_blood", _blood ? 1 : 0); }

        /// <summary>Alpha for ordinary skill effects (important ones — ultimates, evolutions — always stay full).</summary>
        public static float FxAlpha => FxLevel == 0 ? 1f : FxLevel == 1 ? 0.7f : 0.4f;

        public static string FxName => FxLevel == 0 ? "100%" : FxLevel == 1 ? "70%" : "40%";

        public static void ToggleShake() { Load(); _shake = !_shake; PlayerPrefs.SetInt("set_shake", _shake ? 1 : 0); }
        public static void ToggleHitFlash() { Load(); _hitFlash = !_hitFlash; PlayerPrefs.SetInt("set_hitflash", _hitFlash ? 1 : 0); }
        public static void CycleFx() { Load(); _fxLevel = (_fxLevel + 1) % 3; PlayerPrefs.SetInt("set_fx", _fxLevel); }

        /// <summary>Copies' and echoes' effects on their own: a crowded late screen can keep the hero's own casts bright.</summary>
        public static float EchoAlpha { get { Load(); return _echoLevel == 0 ? 1f : _echoLevel == 1 ? 0.6f : _echoLevel == 2 ? 0.3f : 0f; } }
        public static string EchoName { get { Load(); return _echoLevel == 0 ? "100%" : _echoLevel == 1 ? "60%" : _echoLevel == 2 ? "30%" : "끄기"; } }
        public static void CycleEcho() { Load(); _echoLevel = (_echoLevel + 1) % 4; PlayerPrefs.SetInt("set_echo", _echoLevel); }
    }
}
