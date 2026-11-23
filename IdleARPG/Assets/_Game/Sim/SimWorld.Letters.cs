using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>One record in the story mailbox (GDD §26.6): a hint before it opens, a short scene after.</summary>
    public sealed class LetterDef
    {
        public string Id, Title, Hint, Text;
        /// <summary>Only for this class (a fragment's own memory, GDD §26.2); null = everyone.</summary>
        public string ClassId;
        public Func<SimWorld, bool> Opens;
    }

    /// <summary>
    /// GDD §26.6 이야기 우편함: no cutscenes; records pile up in the mailbox (U) as milestones are passed, and are read
    /// when the player has a moment. Each shows a one-line hint until it opens.
    /// </summary>
    public sealed partial class SimWorld
    {
        public static readonly LetterDef[] Letters =
        {
            new LetterDef
            {
                Id = "first_spark", Title = "첫 불씨", Hint = "등불이 무언가를 기억해 내려 한다.",
                Opens = w => w.Kills >= 1,
                Text = "별이 하나씩 꺼질 때마다 땅이 울렸다. 마지막 별이 떨어지던 밤, 나는 마을 입구의 등을 지키고 있었다.\n\n" +
                       "불씨는 손바닥만 했다. 뜨겁지 않았다. 오히려 차가웠고, 들고 있으면 무언가가 조금씩 빠져나가는 느낌이 들었다.\n\n" +
                       "그것이 기억임을 깨달은 건 한참 뒤였다.",
            },
            new LetterDef
            {
                Id = "lamp_oil", Title = "등불 기름", Hint = "쓰러진 것들이 흔적을 흘리고 간다.",
                Opens = w => w.Kills >= 500,
                Text = "괴물이 쓰러질 때 흘리는 것은 피가 아니다. 이름 하나, 얼굴 하나, 저녁 식탁의 냄새 하나.\n\n" +
                       "등불은 그것을 마시고 탄다. 나는 그것이 고마우면서도 무섭다.\n\n" +
                       "언젠가 내가 쓰러지면, 누군가의 등불도 나를 마시겠지.",
            },
            new LetterDef
            {
                Id = "act1", Title = "모래에 묻힌 왕", Hint = "모래 아래에서 왕관이 긁히는 소리가 들린다.",
                Opens = w => w.Hero.DeepestActCleared >= 0,
                Text = "석관 군주는 오래전 이 땅의 왕이었다. 별이 지던 밤, 신하들은 하나씩 모래 속으로 걸어 들어갔고 그는 홀로 남았다.\n\n" +
                       "그는 왕좌를 떠나지 않았다. 떠나면 나라가 정말로 없어지는 것 같았을 테니까.\n\n" +
                       "석관 안쪽에는 백성들의 이름이 빼곡히 새겨져 있었고, 맨 아래 한 줄은 지워져 있었다. 이상하게도 나는 그 빈 줄 앞에서 한참을 서 있었다.",
            },
            new LetterDef
            {
                Id = "act2", Title = "뿌리가 된 기도", Hint = "땅 위의 숲을 그리워하는 뿌리들이 속삭인다.",
                Opens = w => w.Hero.DeepestActCleared >= 1,
                Text = "숲 마을의 장로는 신목 아래에서 매일 기도했다. 별이 떨어진 뒤에도, 해가 들지 않게 된 뒤에도.\n\n" +
                       "기도는 뿌리가 되어 그의 발을 땅에 묶었다. 그는 그것을 응답이라고 여겼다.\n\n" +
                       "그를 쓰러뜨리자 반딧불이 한꺼번에 날아올랐다. 위로, 더 위로. 그들은 아직 하늘을 기억하고 있었다.",
            },
            new LetterDef
            {
                Id = "act3", Title = "병과 하나가 된 자", Hint = "벽 너머에서 누군가의 심장이 뛴다.",
                Opens = w => w.Hero.DeepestActCleared >= 2,
                Text = "맥박 군주는 병든 자들을 돌보던 성직자였다. 안개가 오던 해, 그는 마지막 환자 곁을 떠나지 않았다.\n\n" +
                       "병은 환자를 삼키고, 그를 삼키고, 방 하나를, 골목 하나를 삼켰다. 그 모든 것이 이제 하나의 심장으로 뛴다.\n\n" +
                       "그는 끝까지 돌보았다. 돌볼 이가 자기 자신밖에 남지 않을 때까지.",
            },
            new LetterDef
            {
                Id = "act4", Title = "문을 지킨 자", Hint = "얼음 속에 누군가 아직 서 있다.",
                Opens = w => w.Hero.DeepestActCleared >= 3,
                Text = "얼음 속 거인은 요새의 문지기였다. 별이 지던 날, 피난민들이 다 들어올 때까지 문을 닫지 않겠다고 했다.\n\n" +
                       "마지막 사람은 끝내 오지 않았다. 그는 문을 연 채로 기다렸고, 눈이 그를 덮었다.\n\n" +
                       "그를 쓰러뜨렸을 때 얼음 속에 작은 손자국이 하나 남아 있었다. 누군가 바깥에서 그를 두드렸던 것이다.",
            },
            new LetterDef
            {
                Id = "act5", Title = "새 별을 벼리려던 자들", Hint = "아래에서 망치 소리가 멈추지 않는다.",
                Opens = w => w.Hero.DeepestActCleared >= 4,
                Text = "대장장이 조합은 떨어진 별의 쇠로 새 별을 만들려 했다. 하늘에 다시 걸면, 모든 것이 돌아올 거라고.\n\n" +
                       "조합장은 거푸집이 모자라자 자기 몸을 부었다. 쇳물은 그를 받아 주었다. 그는 지금도 두드린다.\n\n" +
                       "그의 가슴에 박힌 미완성 별은 아직 따뜻했다. 나는 그 온기를 등불에 조금 담아 왔다.",
            },
            new LetterDef
            {
                Id = "act6", Title = "전하지 못한 예언", Hint = "가장 깊은 곳에서 별빛 하나가 꺼지지 않고 빛난다.",
                Opens = w => w.Hero.DeepestActCleared >= 5,
                Text = "꺼진 별의 사도는 한 사람이 아니었다. 별이 떨어지던 밤, 아무 준비도 없이 하늘을 올려다본 사람들이었다.\n\n" +
                       "그들 가운데 누구도 알지 못했다. 알았던 사람은 단 하나였다. 그는 말하지 않았다.\n\n" +
                       "가장 깊은 바닥에 부서진 등불이 놓여 있었다. 손잡이에 내 손자국이 남아 있었다. 여기가 바로 내가 쓰러진 곳이다.",
            },
            new LetterDef
            {
                Id = "rebirth", Title = "잊힌 얼굴", Hint = "등불이 한 번 더 무언가를 태우려 한다.",
                Opens = w => w.Hero.Rebirths >= 1,
                Text = "등불이 나를 다시 일으켰다. 몸은 가볍고, 손에는 힘이 남아 있다.\n\n" +
                       "그런데 이번엔 어머니의 얼굴이 기억나지 않는다. 목소리는 남아 있다. 저녁을 먹으라고 부르던 목소리.\n\n" +
                       "얼굴 없는 목소리를 품고, 나는 다시 내려간다.",
            },
            // GDD §26.2: each class carries one piece of the first lanternkeeper; its memory opens at the ultimate (Lv30).
            new LetterDef
            {
                Id = "frag_ash", ClassId = "ashknight", Title = "분노의 조각", Hint = "타오르는 것 아래 무엇이 있는가.",
                Opens = w => w.Hero.IntegratedLevel >= 30,
                Text = "안개가 마을을 삼키던 밤, 나는 문을 하나씩 두드렸다. 열린 문보다 열리지 않은 문이 많았다.\n\n" +
                       "분노는 그때 생겼다. 안개에게, 별에게, 그리고 끝내 모든 문을 두드리지 못한 나에게.\n\n" +
                       "이 불은 그 밤에서 왔다. 그래서 꺼지지 않는다.",
            },
            new LetterDef
            {
                Id = "frag_oath", ClassId = "oathknight", Title = "맹세의 조각", Hint = "지키겠다는 말은 어디에 묻혔는가.",
                Opens = w => w.Hero.IntegratedLevel >= 30,
                Text = "지키겠다고 약속했다. 아내에게, 두 아이에게. 약속은 쉬웠다. 지키는 것은 그렇지 않았다.\n\n" +
                       "나는 그들을 제 손으로 묻었다. 묘실 맨 아래, 이름을 새길 틈도 없이.\n\n" +
                       "그래서 나는 맞는다. 대신 맞는 동안만은, 아직 지키고 있는 것 같아서.",
            },
            new LetterDef
            {
                Id = "frag_wind", ClassId = "windhunter", Title = "두려움의 조각", Hint = "한 번 돌아선 발걸음이 있다.",
                Opens = w => w.Hero.IntegratedLevel >= 30,
                Text = "심연 입구에서 한 번 돌아섰다. 아래에서 올라오는 바람이 너무 차가웠다.\n\n" +
                       "누구도 탓하지 않았다. 나만 알았다. 그 한 걸음 동안 몇 개의 등이 더 꺼졌는지.\n\n" +
                       "그래서 나는 거리를 둔다. 다시는 돌아서지 않으려고, 처음부터 너무 가까이 다가서지 않는다.",
            },
            new LetterDef
            {
                Id = "frag_star", ClassId = "starseer", Title = "죄책감의 조각", Hint = "별이 떨어지기 전에 이미 말을 건넸다.",
                Opens = w => w.Hero.IntegratedLevel >= 30,
                Text = "별이 떨어질 것을 알았다. 하늘의 배열을 읽는 것이 내 일이었으니까.\n\n" +
                       "말하지 않았다. 믿지 않을 것 같아서, 아니면 믿을 것 같아서. 어느 쪽이 더 무서웠는지 이제는 모르겠다.\n\n" +
                       "시간을 되감는 법을 깨달은 건 그 뒤다. 한 번도 그 밤까지는 되감기지 않았다.",
            },
        };

        /// <summary>Letters this class can ever receive.</summary>
        public IEnumerable<LetterDef> LettersFor()
        {
            foreach (var l in Letters) if (l.ClassId == null || l.ClassId == Hero.ClassId) yield return l;
        }

        void TickLetters()
        {
            if (TickCount % 10 != 5) return; // once a second, off the achievements' beat
            foreach (var l in Letters)
            {
                if ((l.ClassId != null && l.ClassId != Hero.ClassId) || Hero.Letters.Contains(l.Id) || !l.Opens(this)) continue;
                Hero.Letters.Add(l.Id);
                Events.Add(new SimEvent { Type = SimEventType.Letter, Pos = Hero.Pos, SkillId = l.Title });
            }
        }

        public void MarkRead(string id) => Hero.LettersRead.Add(id);

        public int UnreadLetters
        {
            get
            {
                int n = 0;
                foreach (var id in Hero.Letters) if (!Hero.LettersRead.Contains(id)) n++;
                return n;
            }
        }
    }
}
