using System.Collections.Generic;
using Lanternkeeper.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Lanternkeeper.View
{
    /// <summary>
    /// 첫 안내 (UX plan D4; 희승 11-23 "이제 막 게임 시작하는 사람들은 어떻겠어"): one line under the top HUD the first time
    /// something happens — the first magic, rare, legendary, unique and ★ drop, the first resonance, the levels that open a
    /// system, the first death, shrine, augment card, evolution, a nearly full bag. Each is shown once per account (kept in
    /// PlayerPrefs), eight seconds, one at a time with a pause between, queued. It never stops the hunt.
    /// </summary>
    public sealed class FirstTips
    {
        const float W = 600f, H = 24f, Show = 8f, Pause = 12f;
        const string Key = "first_tips_seen";
        readonly RectTransform _box;
        readonly Text _text;
        readonly CanvasGroup _group;
        readonly Queue<(string id, string text)> _queue = new Queue<(string, string)>();
        readonly HashSet<string> _seen = new HashSet<string>();
        float _left, _pause = 4f, _alive;
        bool _welcomed;
        /// <summary>F2, the return summary and the class screen keep it hidden (the queue waits).</summary>
        public bool Hidden;

        public FirstTips(UiKit ui)
        {
            _box = UiKit.Rect(ui.Root, "First Tips", 0f, 0f, W, H);
            _box.anchorMin = _box.anchorMax = new Vector2(0.5f, 1f);
            _box.pivot = new Vector2(0.5f, 1f);
            _box.anchoredPosition = new Vector2(0f, -46f); // under the level, ETA and next-goal lines
            var back = _box.gameObject.AddComponent<Image>();
            back.color = new Color(0.03f, 0.05f, 0.09f, 0.92f);
            back.raycastTarget = false;
            UiKit.Fill(_box, "edge", 0f, 0f, 3f, H, new Color(0.45f, 0.7f, 1f));
            UiKit.Label(_box, "?", 8f, 5f, 12f, 16f, 12, TextAnchor.UpperLeft, new Color(0.55f, 0.78f, 1f));
            _text = UiKit.Label(_box, "", 22f, 5f, W - 30f, 16f, 12, TextAnchor.UpperLeft, new Color(0.86f, 0.92f, 1f));
            _group = _box.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            try
            {
                foreach (var id in PlayerPrefs.GetString(Key, "").Split(','))
                    if (id.Length > 0) _seen.Add(id);
            }
            catch { /* no prefs: show them again, nothing breaks */ }
            _box.gameObject.SetActive(false);
        }

        void Offer(string id, string text)
        {
            if (_seen.Contains(id)) return;
            foreach (var q in _queue) if (q.id == id) return;
            _queue.Enqueue((id, text));
        }

        /// <summary>After each sim tick: what happened for the first time.</summary>
        public void OnTick(SimWorld w)
        {
            var h = w.Hero;
            if (!_welcomed)
            {
                _welcomed = true;
                if (h.Level <= 1) Offer("welcome", "사냥은 알아서 합니다. 당신은 장비(I)와 스킬 순서(R)를 고릅니다. 위의 '다음:' 줄이 지금 목표입니다.");
            }
            foreach (var ev in w.Events)
            {
                switch (ev.Type)
                {
                    case SimEventType.Drop when ev.Item != null:
                        switch (ev.Item.Rarity)
                        {
                            case Rarity.Magic: Offer("magic", "파란 이름은 마법 등급입니다. ◆ 개수가 등급이고, 더 좋은지는 ▲▼ 화살표가 알려 줍니다."); break;
                            case Rarity.Rare: Offer("rare", "노란 이름은 희귀입니다. 옵션이 3~4줄 붙습니다."); break;
                            case Rarity.Legendary: Offer("legendary", "주황은 전설입니다. 빌드를 바꾸는 전설 효과가 붙습니다."); break;
                            case Rarity.Unique: Offer("unique", "보라는 고유입니다. 가장 드문 등급입니다."); break;
                        }
                        if (ev.Item.GreaterCount > 0) Offer("star", "★는 대옵션(수치 1.5배)입니다. 자동 분해되지 않습니다.");
                        break;
                    case SimEventType.Equip: Offer("equip", "더 좋은 장비(▲)는 알아서 낍니다. 가방(I)에서 볼 수 있습니다."); break;
                    case SimEventType.Resonance when ev.Res != Resonance.Stack:
                        Offer("resonance", "공명! 원소 각인이 남은 적을 순환 순서의 다음 원소로 치면 터집니다. 스킬 창(R)의 공명표를 보세요."); break;
                    case SimEventType.SkillLevelUp when ev.Value >= 10:
                        Offer("copy", "숙련 10: 분신 +1. 같은 스킬이 다른 적에게 한 번 더 나갑니다."); break;
                    case SimEventType.Evolved: Offer("evolved", "스킬이 진화했습니다. 스킬 트리(T)에서 무엇이 달라졌는지 볼 수 있습니다."); break;
                    case SimEventType.HeroDied: Offer("death", "쓰러지면 구역 첫 층으로 돌아갑니다. 잃는 것은 없습니다. 자주 쓰러지면 태세(S)를 '신중'으로."); break;
                    case SimEventType.EventOffered: Offer("shrine", "이벤트방: 1·2·3 중 하나를 고릅니다. 시간이 지나면 기본값을 고릅니다."); break;
                    case SimEventType.AugmentOffered: Offer("augment", "증강 카드는 영구 강화입니다. 첫 삶의 세 장은 시간 제한이 없습니다."); break;
                    case SimEventType.LevelUp:
                        int lv = h.IntegratedLevel;
                        if (lv == 2) Offer("lv2", "스킬이 둘이 됐습니다. 스킬 창(R)에서 쓰는 순서를 정합니다.");
                        else if (lv == 8) Offer("lv8", "스킬 4칸이 다 찼습니다. 스킬 창(R)의 [순서 시험]으로 더 센 순서를 찾아보세요.");
                        else if (lv == SkillBook.SigilLevel) Offer("lv12", "보조 문양이 열렸습니다. 단 칸과 그 다음 칸이 강해집니다(스킬 창 R).");
                        else if (lv == SkillBook.UltimateLevel) Offer("lv30", "궁극기가 열렸습니다. 스킬 트리(T)에서 둘 중 하나를 고릅니다.");
                        break;
                }
            }
            if (w.Bag.Items.Count >= 36) Offer("bag", "가방이 거의 찼습니다. 가득 차면 가장 약한 것부터 분해됩니다. 지킬 것은 Shift+클릭으로 잠그세요.");
            if (w.CanRebirth) Offer("rebirth", "환생할 수 있습니다. 레벨은 1로, 장비는 그대로, 쌓인 경험치는 영구 잔불 레벨이 됩니다(야영지 Y).");
        }

        public void Render(float dt)
        {
            if (Hidden) { if (_box.gameObject.activeSelf) _box.gameObject.SetActive(false); return; }
            if (_left > 0f)
            {
                _left -= dt;
                _alive += dt;
                float a = Mathf.Min(1f, _alive / 0.25f) * Mathf.Clamp01(_left / 0.5f);
                _group.alpha = Mathf.Round(a * 8f) / 8f;
                if (!_box.gameObject.activeSelf) _box.gameObject.SetActive(true);
                if (_left <= 0f) { _box.gameObject.SetActive(false); _pause = Pause; }
                return;
            }
            _pause -= dt;
            if (_pause > 0f || _queue.Count == 0) return;
            var (id, text) = _queue.Dequeue();
            _seen.Add(id);
            try { PlayerPrefs.SetString(Key, string.Join(",", _seen)); PlayerPrefs.Save(); } catch { }
            _text.text = text;
            float w = Mathf.Min(900f, _text.preferredWidth + 34f);
            _box.sizeDelta = new Vector2(w, H);
            _text.rectTransform.sizeDelta = new Vector2(w - 30f, 16f);
            _left = Show;
            _alive = 0f;
            _box.SetAsLastSibling(); // over an open window: a tip under the bag's edge was cut in half (loop 1)
        }
    }
}
