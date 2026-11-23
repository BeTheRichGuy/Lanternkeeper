using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// In-game explanations for a newcomer (희승 11-23: "스킬들도 각각 설명이 없고 … 아이템 스탯같은것도 뭐가 더 유리한지
    /// 모르겠고 … 진입장벽이 크게 느껴진달까"): what each skill, glyph, stat, rarity, resonance, element and coined term
    /// does, in plain words. Every line states what the sim does (SkillData, SimWorld.Skills, SimWorld.Classes, Combat,
    /// Equipment, ItemGen) — when a number changes there, change it here. "공격력의 N%" is SkillDef.Power × 100;
    /// "스킬 피해의 N%" is a share of that skill's own hit.
    /// </summary>
    public static class GameText
    {
        // ---------- 1. skills ----------

        static readonly Dictionary<string, string> Skills = new Dictionary<string, string>
        {
            // 기본 공격
            { "basic", "앞의 적 하나를 공격력의 100%로 벱니다. 근접 직업은 대상 주변의 적도 40%로 함께 벱니다." },
            { "bow_shot", "멀리 있는 적 하나에게 공격력의 100%로 화살을 쏩니다. 준비된 스킬이 없을 때 나갑니다." },
            { "star_orb", "멀리 있는 적 하나에게 공격력의 100%로 별빛 구슬을 쏩니다. 준비된 스킬이 없을 때 나갑니다." },

            // 잿불기사
            { "ember_slash", "적 하나를 공격력의 160%로 베고 잿불 각인을 남깁니다. 자주 쓰는 기본기이자 공명의 시작점입니다." },
            { "ember_whirl", "2.5초 동안 돌면서 0.3초마다 주변 적을 공격력의 65%로 벱니다. 무리 속을 걸어 다니며 정리합니다." },
            { "frost_brand", "적 하나를 공격력의 130%로 치고 서리 각인을 남깁니다. 잿불 각인이 있는 적을 치면 파쇄가 터집니다." },
            { "heat_absorb", "주변을 공격력의 60%로 태우고, 쌓인 열기를 모두 써서 생명력을 회복합니다. 열기가 가득이면 20% 회복하는 생존기." },
            { "ash_leap", "적이 가장 뭉친 곳으로 뛰어들어 착지 범위에 공격력의 150% 피해를 줍니다. 무리 진입과 정리에 좋습니다." },
            { "storm_cleave", "앞쪽 부채꼴을 공격력의 150% 전격으로 벱니다. 서리 각인이 있는 적에게 과부하를 일으킵니다." },
            { "forge_strike", "적 하나를 공격력의 260%로 내려칩니다. 대상의 중첩을 모두 써서 중첩·각인 하나당 피해 +40%, 보스용." },
            { "chain_hook", "대상 주변의 적을 한곳으로 끌어모으며 공격력의 130% 공허 피해를 줍니다. 범위 스킬 앞에 쓰면 좋습니다." },
            { "cinder_echo", "장전해 두면 각인된 적이 죽은 자리가 1.2초 뒤 공격력의 180%로 다시 터집니다. 사망 연쇄가 한 번 더 시작됩니다." },
            { "thorn_mail", "6초 동안 맞을 때마다 때린 적에게 받은 피해의 60%를 잿불로 돌려줍니다(최소 공격력의 60%). 맞으며 싸울 때." },
            { "fissure", "대상 쪽으로 6칸을 달리는 분출이 지나는 적에게 공격력의 140% 피해를 줍니다. 길게 늘어선 무리 정리." },
            { "molten_heart", "생명력 8%를 태워 4초 동안, 각인된 적을 직접 때리기만 해도 사망 연쇄가 시작됩니다. 연쇄 빌드의 핵심." },
            { "frostfire_aura", "6초 동안 1초마다 주변 적을 공격력의 35%로 잿불과 서리를 번갈아 칩니다. 혼자서 파쇄가 계속 터집니다." },
            { "smith_hammer", "0.8초 뒤 대상 자리에 망치가 떨어져 공격력의 180% 전격 피해를 줍니다. 중첩·각인마다 망치가 하나씩 더(최대 3개)." },
            { "forge_judgment", "대상 주변 넓은 범위에 공격력의 300% 잿불 피해를 주고 공명 게이지를 절반 채웁니다. 큰 무리와 보스에." },
            { "undying_flame", "8초 동안 사망 연쇄가 약해지지 않고 3번 더 이어집니다. 큰 무리 한복판에서 쓰면 방 전체가 터집니다." },

            { "evo_twin_ember", "잿불 베기가 두 번 벱니다. 단일 대상 피해가 두 배가 되어 정예와 보스에게 강해집니다." },
            { "evo_flame_vortex", "휠윈드가 넓어지고(2.2→2.8칸) 돌면서 주변 적을 끌어당깁니다. 무리가 알아서 모여듭니다." },
            { "evo_glacial_brand", "서리 낙인이 진화한 모습입니다. 지금은 기본형과 성능이 같습니다." },
            { "evo_ember_heart", "열기 흡수로 회복한 만큼의 피해를 주변 적에게 한 번 더 줍니다. 회복과 공격을 함께 합니다." },
            { "evo_meteor_fall", "잿더미 도약이 착지한 자리에 3초 동안 불타는 장판을 남깁니다. 뛰어든 자리의 무리를 계속 태웁니다." },
            { "evo_thunder_cleave", "폭풍 가르기가 놓친 적에게 번개 3줄기가 튀어 스킬 피해의 60%를 줍니다. 부채꼴 밖까지 닿습니다." },
            { "evo_molten_execution", "용광 일격으로 적을 처치하면 주변 적에게 잿불 각인과 중첩 1을 남깁니다. 다음 일격이 더 세집니다." },
            { "evo_abyss_chain", "쇠사슬 끌기로 끌어온 적들이 사슬로 묶입니다. 한 명이 직접 맞은 피해의 30%를 나머지도 받습니다." },
            { "evo_cinder_grave", "재의 메아리가 2번 장전됩니다. 각인된 적 둘이 죽은 자리가 다시 터집니다." },
            { "evo_lava_mail", "가시 갑주의 반격이 때린 적 하나가 아니라 주변 1.5칸의 모든 적에게 들어갑니다." },
            { "evo_triple_fissure", "화산 균열이 세 갈래로 퍼져 나갑니다. 넓게 흩어진 무리도 한 번에 덮습니다." },
            { "evo_erupting_heart", "녹은 심장의 지속 시간이 4초에서 6초로 늘어납니다." },
            { "evo_eternal_frostfire", "서리불꽃 갑옷의 범위가 2칸에서 3칸으로 넓어집니다." },
            { "evo_heaven_anvil", "대장장이의 망치가 떨어진 자리에 절반 위력의 폭발이 항상 한 번 더 일어납니다." },

            // 서약기사
            { "blessed_hammer", "주변의 모든 적을 공격력의 110% 전격으로 내려칩니다. 1.5초마다 쓰는 주력 무리 정리기입니다." },
            { "zeal", "적 하나를 공격력의 50%로 빠르게 4번 칩니다. 단일 대상과 보스에게 좋습니다." },
            { "shield_bash", "적 하나를 공격력의 130%로 쳐 0.8초 기절시키고 서리 각인을 남깁니다. 전격 스킬 앞에 쓰면 과부하." },
            { "hallowed_aura", "6초 동안 1초마다 주변 적에게 공격력의 35% 전격 피해를 줍니다. 켜 두고 싸우는 보조 피해입니다." },
            { "rallying_cry", "주변 적을 끌어당겨 공격력의 60%로 치고, 4초 동안 받는 피해를 30% 줄입니다. 맹세도 20 찹니다." },
            { "judgment", "적 하나를 공격력의 220% 전격으로 내려칩니다. 재사용이 짧은 큰 한 방으로 정예와 보스에게 좋습니다." },
            { "oath_barrier", "6초 동안 최대 생명력의 25%만큼 보호막을 두릅니다. 보호막이 깨지면 주변에 공격력의 150% 폭발." },
            { "martyr_chains", "6초 동안 주변 적 최대 6명을 사슬로 묶습니다. 내가 받을 피해의 30%를 그들이 두 배로 대신 받습니다." },
            { "consecrate", "5초 동안 발밑 대지가 0.5초마다 공격력의 45%로 적을 칩니다. 그 위에 서 있으면 초당 생명력 2% 회복." },
            { "shield_throw", "방패를 던져 공격력의 100% 서리 피해를 주고 근처 적 3명에게 80%로 튕깁니다. 여럿에게 서리 각인." },
            { "bell_of_judgment", "주변 적을 공격력의 120%로 치며 각인을 한꺼번에 터뜨려 저마다 공명을 일으킵니다. 맹세를 모두 써 최대 2배." },
            { "holy_charge", "대상에게 돌진해 주변에 공격력의 140% 전격 피해를 주고 0.5초 기절시킵니다. 무리 진입용." },
            { "grace_echo", "3번 장전되어, 맞을 때마다 주변에 공격력의 100% 전격 폭발로 되받아칩니다. 둘러싸였을 때 좋습니다." },
            { "unbroken_vow", "5초 동안 생명력이 1 밑으로 내려가지 않습니다. 끝나면 그동안 받은 피해만큼 큰 폭발로 돌려줍니다." },
            { "heaven_spears", "가까운 적 3명 위로 창이 떨어져 각각 공격력의 140% 전격 피해를 줍니다. 흩어진 적 처리에 좋습니다." },
            { "warding_sigil", "대상 자리에 6초짜리 지대를 깔아 0.5초마다 공격력의 25% 서리 피해를 줍니다. 서리 각인을 계속 남깁니다." },
            { "grand_judgment", "주변 넓은 범위에 공격력의 350% 전격 피해를 주고 공명 게이지를 절반 채웁니다. 큰 무리와 보스에." },
            { "sanctuary", "8초 동안 받는 피해가 60% 줄고, 맞을 때마다 공격력의 80% 전격 파동으로 되받아칩니다. 위기 탈출." },

            { "evo_rampart_bash", "방패 강타가 앞쪽 부채꼴 전체를 쳐서 범위 안의 모든 적을 기절시킵니다." },
            { "evo_pilgrim_light", "성화 오라가 넓어지고(2.2→3칸) 켜져 있는 동안 초당 생명력 0.5%를 회복합니다." },
            { "evo_iron_cry", "도발의 외침이 각인 없는 적에게 서리 각인을 먼저 새기고 쳐서 중첩까지 쌓습니다." },
            { "evo_double_judgment", "심판이 두 번 내려칩니다. 단일 대상 피해가 두 배가 됩니다." },
            { "evo_retort_barrier", "맹세의 방벽이 깨질 때의 폭발 피해가 두 배(공격력의 300%)가 됩니다." },
            { "evo_confession_chains", "순교자의 사슬이 대신 받는 몫이 30%에서 50%로 늘어납니다." },
            { "evo_holy_ground", "축성된 대지의 범위가 2.5칸에서 3.5칸으로 넓어집니다." },
            { "evo_returning_shield", "방패 투척이 튕기는 횟수가 3번에서 6번으로 늘어납니다." },
            { "evo_cathedral_bell", "심판의 종 피해가 공격력의 120%에서 180%로 오릅니다." },
            { "evo_charge_of_light", "성스러운 돌진이 착지한 자리에 3초 동안 전격 장판을 남깁니다." },
            { "evo_grace_chorus", "은총의 메아리 장전이 3번에서 5번으로 늘어납니다." },
            { "evo_deathless_vow", "무너지지 않는 서약의 지속 시간이 5초에서 7초로 늘어납니다." },
            { "evo_spear_rain", "천벌의 창이 3개에서 5개로 늘어납니다." },
            { "evo_hammer_storm", "축복의 망치 범위가 2.6칸에서 3.4칸으로 넓어집니다." },
            { "evo_fanaticism", "열성이 4번이 아니라 6번 칩니다." },
            { "evo_sealing_sigil", "수호의 각인 지대가 칠 때마다 적을 0.3초 기절시킵니다. 지대 위의 적이 거의 움직이지 못합니다." },

            // 바람사냥꾼
            { "chain_arrow", "공격력의 120% 화살이 맞은 자리에서 근처 적 3명에게 갈라집니다(스킬 피해의 60%). 주력 무리 정리기." },
            { "venom_arrow", "공격력의 130% 부패 화살을 쏘고 3초 동안 독(공격력의 90%)을 겁니다. 전격 각인에 쏘면 역병 전도." },
            { "whirling_blades", "내 주변의 모든 적을 공격력의 120% 전격으로 벱니다. 적이 붙었을 때 쓰는 근접 정리기입니다." },
            { "roll_shot", "뒤로 굴러 거리를 벌린 뒤 공격력의 140% 화살을 쏩니다. 붙은 적에게서 빠져나올 때 좋습니다." },
            { "shock_trap", "대상 자리에 4초짜리 덫을 깔아 0.5초마다 공격력의 50% 피해를 줍니다. 각인된 적은 살아 있어도 사망 연쇄가 터집니다." },
            { "wind_path", "대상 쪽으로 6칸 바람길을 내 공격력의 90% 피해를 주고 적을 길 위로 끌어모읍니다. 범위 스킬 앞에." },
            { "storm_eye", "6초 동안 공격 속도가 50% 오르고 단일 화살이 근처 적 2명에게 더 튑니다. 몰아칠 때 켭니다." },
            { "rot_cloud", "대상 자리에 5초짜리 구름을 깔아 0.5초마다 공격력의 50% 부패 피해를 줍니다. 부패 각인을 계속 남깁니다." },
            { "arrow_rain", "가까운 적 6명 위로 화살이 떨어져 각각 공격력의 110% 범위 피해를 줍니다. 무리 마무리용." },
            { "ricochet_arrow", "곧게 날아 벽에 3번 튕기는 화살이 지나가는 모든 적에게 공격력의 90% 피해를 줍니다. 좁은 방에서 강합니다." },
            { "hunters_mark", "공격력의 80%로 쏘고 대상이 6초 동안 피해를 30% 더 받게 합니다. 그동안 죽으면 사망 연쇄가 두 배 멀리 퍼집니다." },
            { "shadow_decoy", "대상 자리에 분신을 세워 4초 동안 적을 끌어모으고, 끝날 때 공격력의 180%로 터집니다. 몰이용." },
            { "void_lance", "대상 쪽 8칸을 꿰뚫어 공격력의 200% 공허 피해를 줍니다. 부패 각인이 있는 적에게 포식을 일으킵니다." },
            { "gale_volley", "화살 8발을 연달아 쏩니다(한 발에 공격력의 55%). 대상이 죽으면 남은 화살은 근처 적에게 갑니다." },
            { "viper_nest", "4번 장전되어, 각인된 적이 죽으면 그 자리에 2초짜리 부패 구름이 남습니다. 무리가 이어서 녹습니다." },
            { "thunderstorm", "8초 동안 0.5초마다 6칸 안의 적 하나에게 공격력의 150% 벼락이 떨어집니다. 싸우는 내내 추가 피해." },
            { "hunt_time", "8초 동안 공격 속도가 2배가 되고 스킬 재사용 대기시간이 절반이 됩니다. 보스전에서 몰아칠 때 씁니다." },

            { "evo_storm_arrow", "갈래 화살에서 갈라진 화살이 한 번 더 갈라집니다. 큰 무리 전체로 번집니다." },
            { "evo_plague_arrow", "독침 화살이 맞은 자리 1.5칸 안의 적에게도 스킬 피해의 50%를 줍니다." },
            { "evo_blade_storm", "회오리 칼날이 넓어지고(2.2→3칸) 주변 적을 안쪽으로 끌어당깁니다." },
            { "evo_triple_shot", "구르기 사격이 구른 뒤 화살을 3발 쏩니다." },
            { "evo_chain_trap", "전격 덫의 지속 시간이 4초에서 6초로 늘어납니다." },
            { "evo_storm_path", "바람길이 세 갈래로 퍼져 나갑니다." },
            { "evo_typhoon_eye", "폭풍의 눈 지속 시간이 6초에서 8초로 늘어납니다." },
            { "evo_plague_fog", "부패의 구름 범위가 2.5칸에서 3.5칸으로 넓어집니다." },
            { "evo_steel_rain", "화살비의 화살이 6발에서 10발로 늘어납니다." },
            { "evo_prey_brand", "사냥꾼의 표식이 걸린 적이 더 받는 피해가 30%에서 50%로 오릅니다." },
            { "evo_ricochet_storm", "도탄 화살이 벽에 6번까지 튕기고 더 멀리(16→24칸) 날아갑니다." },
            { "evo_shadow_legion", "그림자 분신이 적을 끌어당기는 힘이 두 배가 됩니다." },
            { "evo_abyss_lance", "공허 관통의 피해가 공격력의 200%에서 300%로 오릅니다." },
            { "evo_storm_volley", "속사의 화살이 8발에서 12발로 늘어납니다." },
            { "evo_viper_den", "독사의 둥지 장전이 4번에서 6번으로 늘어납니다." },

            // 별점술사
            { "fireball", "공격력의 130% 화염구가 터지며 주변 적에게도 절반 피해를 줍니다. 맞은 적은 3초 동안 불탑니다(화상)." },
            { "combustion", "대상 주변을 공격력의 100%로 폭발시키고 남은 화상을 2.5배로 한꺼번에 터뜨립니다. 화상이 주변 적에게 옮겨붙습니다." },
            { "ice_bolt", "공격력의 130% 얼음 화살을 쏩니다. 서리 피해를 3번 맞은 적은 1.5초 동안 얼어붙습니다(빙결)." },
            { "ice_shatter", "대상 주변에 공격력의 140% 서리 피해를 줍니다. 얼어붙은 적에게는 피해가 2.5배입니다." },
            { "blizzard", "대상 자리에 5초 동안 눈보라가 0.5초마다 공격력의 45% 서리 피해를 줍니다. 무리를 계속 얼립니다." },
            { "chain_lightning", "공격력의 110% 번개가 대상을 치고 근처 적 4명에게 튑니다(스킬 피해의 80%). 주력 무리 정리기." },
            { "thunder_nova", "내 주변의 모든 적에게 공격력의 160% 전격 피해를 줍니다. 적이 붙었을 때의 큰 한 방입니다." },
            { "lightning_storm", "가까운 적 5명 위로 벼락이 떨어져 각각 공격력의 100% 범위 피해를 줍니다. 흩어진 무리 처리." },
            { "ball_lightning", "대상 쪽으로 7칸을 굴러가는 번개 구슬이 지나는 적에게 공격력의 120% 피해를 줍니다." },
            { "meteor", "0.9초 뒤 가장 가까운 적 위로 운석이 떨어져 넓은 범위에 공격력의 240% 피해를 줍니다. 큰 한 방." },
            { "frost_nova", "내 주변의 적에게 공격력의 100% 서리 피해를 주고 0.6초 기절시킵니다. 적이 붙었을 때의 생존기." },
            { "nebula_burst", "대상 주변을 공격력의 160% 잿불 피해로 폭발시킵니다. 뭉친 무리를 멀리서 터뜨립니다." },
            { "supernova", "내 주변 넓은 범위에 공격력의 280% 잿불 피해를 줍니다. 재사용이 길지만 가장 큰 범위 한 방입니다." },
            { "starfall_field", "내 발밑에 5초짜리 장판을 깔아 0.5초마다 공격력의 35% 서리 피해를 줍니다. 다가오는 적을 얼립니다." },
            { "star_collapse", "대상 주변 넓은 범위에 공격력의 400% 공허 피해를 주고 공명 게이지를 절반 채웁니다. 큰 무리와 보스에." },
            { "eternal_moment", "6초 동안 재사용 대기시간 1/3, 공격 속도 +40%, 치명타 확률 +20%. 고리 마지막 스킬이 첫 스킬을 60%로 다시 부릅니다." },

            { "evo_great_fireball", "화염구가 터지는 범위가 1.2칸에서 2칸으로 넓어집니다." },
            { "evo_inferno", "연소가 남은 화상을 2.5배가 아니라 4배로 터뜨립니다." },
            { "evo_ice_lance", "얼음 화살이 두 번 맞습니다. 피해가 두 배가 되고 빙결도 더 빨리 걸립니다." },
            { "evo_glacier_break", "빙결 파쇄의 범위가 2.2칸에서 3칸으로 넓어집니다." },
            { "evo_whiteout", "눈보라의 범위가 2.6칸에서 3.6칸으로 넓어집니다." },
            { "evo_storm_chain", "연쇄 번개가 튀는 횟수가 4번에서 7번으로 늘어납니다." },
            { "evo_thunder_crown", "번개 폭발의 범위가 2.8칸에서 3.6칸으로 넓어집니다." },
            { "evo_tempest", "번개 폭풍의 벼락이 5개에서 8개로 늘어납니다." },
            { "evo_twin_ball", "구슬 번개가 두 갈래로 나갑니다." },
            { "evo_meteor_shower", "운석이 1개에서 3개로 늘어 가까운 적 셋 위로 떨어집니다." },
            { "evo_glacial_nebula", "서리 별빛의 범위가 2.5칸에서 3.5칸으로 넓어집니다." },
            { "evo_great_nebula", "성운 폭발의 범위가 2.5칸에서 3.5칸으로 넓어집니다." },
            { "evo_hypernova", "초신성의 범위가 3.5칸에서 4.5칸으로 넓어집니다." },
            { "evo_radiant_field", "별빛 장판의 범위가 3칸에서 4칸으로 넓어집니다." },

            // 별점술사 이전 버전 스킬 (옛 저장 파일에 남아 있을 수 있음)
            { "star_bolt", "멀리 있는 적 하나에게 공격력의 130% 공허 피해를 주고 공허 각인을 남깁니다." },
            { "star_blessing", "6초 동안 공격 속도 +40%, 치명타 확률 +20%. 고리 마지막 스킬이 첫 스킬을 60%로 다시 부릅니다." },
            { "black_hole", "대상 자리에 4초 동안 적을 빨아들이는 구멍을 만들고, 끝날 때 공격력의 200%로 터집니다. 몰이용." },
            { "constellation", "주변의 각인된 적을 최대 8명까지 잇습니다. 이어진 수가 많을수록 피해가 커집니다(4명이면 2배)." },
            { "prism_star", "곧게 날아 벽에 4번 튕기는 별탄이 지나가는 모든 적에게 공격력의 100% 피해를 줍니다." },
            { "time_stop", "주변 적을 3초 동안 멈춥니다. 그동안 준 피해는 모였다가 끝날 때 1.5배로 한꺼번에 들어갑니다." },
            { "rewind", "고리의 다른 스킬들의 재사용 대기시간을 3초씩 앞당깁니다." },
            { "star_veil", "5초 동안 최대 생명력의 20%만큼 보호막을 두릅니다. 보호막이 깨지면 주변에 공격력의 120% 서리 폭발." },
            { "gravity_well", "대상 주변 4칸의 적을 한 점으로 강하게 끌어모으며 공격력의 80% 공허 피해를 줍니다." },
            { "comet_trail", "대상 쪽 7칸을 가르는 혜성이 공격력의 150% 피해를 주고, 지나간 자리가 2초 동안 불탑니다." },

            { "evo_twin_star", "별똥 화살이 두 번 맞습니다." },
            { "evo_galaxy_blessing", "별의 축복 지속 시간이 6초에서 8초로 늘어납니다." },
            { "evo_event_horizon", "블랙홀의 범위가 2.8칸에서 3.5칸으로 넓어집니다." },
            { "evo_great_constellation", "별자리 잇기가 최대 8명이 아니라 12명까지 잇습니다." },
            { "evo_eternal_stop", "시간 정지가 끝날 때 모인 피해가 1.5배가 아니라 2배로 들어갑니다." },
            { "evo_prism_nova", "반사 별탄이 벽에 7번까지 튕기고 더 멀리(18→26칸) 날아갑니다." },
            { "evo_reverse_time", "되감기가 앞당기는 시간이 3초에서 5초로 늘어납니다." },
            { "evo_galaxy_veil", "별의 장막 보호막이 최대 생명력의 20%에서 30%로 커집니다." },
            { "evo_singularity", "중력 붕괴가 끌어모으는 범위가 4칸에서 5칸으로 넓어집니다." },
            { "evo_twin_comet", "혜성 꼬리가 두 갈래로 나갑니다." },
        };

        /// <summary>What the skill (or evolution) does and what it is good for; null when the id is unknown.</summary>
        public static string Skill(string id) => id != null && Skills.TryGetValue(id, out var text) ? text : null;

        // ---------- 2. roles and shapes ----------

        public static string Role(SkillRole r)
        {
            switch (r)
            {
                case SkillRole.Opener: return "개시 스킬 — 싸움을 여는 스킬. 재사용이 짧고 적에게 각인을 남깁니다.";
                case SkillRole.Extender: return "연계 스킬 — 무리를 모으거나 넓게 쓸어, 다음 스킬이 잘 들어가게 잇습니다.";
                case SkillRole.Finisher: return "종결 스킬 — 앞 스킬이 남긴 각인을 터뜨리는 큰 한 방. 고리 뒤쪽 칸에 둡니다.";
                case SkillRole.Defense: return "방어 — 회복·보호막·피해 감소로 버티게 해 주는 스킬.";
                case SkillRole.Special: return "특수 — 일정 시간 전투 규칙을 바꾸는 스킬(강화·오라·장전).";
                case SkillRole.Ultimate: return "궁극기 — 재사용이 30~40초인 가장 강한 스킬. 정예·보스나 큰 무리를 만나면 알아서 씁니다.";
                default: return "";
            }
        }

        public static string Shape(SkillShape s)
        {
            switch (s)
            {
                case SkillShape.Single: return "단일 — 적 하나를 칩니다.";
                case SkillShape.Circle: return "원형 — 내 주변(또는 대상 주변)의 모든 적을 칩니다.";
                case SkillShape.Cone: return "부채꼴 — 바라보는 쪽 앞을 넓게 칩니다.";
                case SkillShape.Line: return "직선 — 대상 쪽으로 뻗어 나가며 지나는 적을 칩니다.";
                case SkillShape.Leap: return "도약 — 뛰어서 자리를 옮기고 착지한 곳 주변을 칩니다.";
                case SkillShape.Pull: return "끌기 — 대상 주변의 적을 한곳으로 모읍니다.";
                case SkillShape.Delayed: return "낙하 — 잠시 뒤 적이 있던 자리에 떨어집니다.";
                case SkillShape.Buff: return "강화 — 일정 시간 나에게 효과를 겁니다.";
                case SkillShape.Aura: return "오라 — 일정 시간 내 주변의 적을 계속 칩니다.";
                case SkillShape.Armed: return "장전 — 걸어 두면 조건이 맞을 때(적 처치·피격) 터집니다.";
                default: return "";
            }
        }

        // ---------- 3. sigils (보조 문양) ----------

        /// <summary>Name and effect of a 보조 문양. A glyph works on its own slot and on the next slot clockwise.</summary>
        public static string Sigil(Sigil s)
        {
            switch (s)
            {
                case Lanternkeeper.Sim.Sigil.Chain: return "연쇄 — 근접 단일 스킬이 근처 적 하나에게 한 번 더 튑니다(스킬 피해의 70%). 원래 튕기는 스킬은 1번 더 튕깁니다.";
                case Lanternkeeper.Sim.Sigil.Split: return "분열 — 직선 스킬은 2갈래, 낙하 스킬은 2발이 늘어납니다. 대신 피해 −25%.";
                case Lanternkeeper.Sim.Sigil.Wide: return "확산 — 스킬 범위 +40%. 적 하나만 치는 스킬에는 효과가 없습니다.";
                case Lanternkeeper.Sim.Sigil.Focus: return "집중 — 범위 −30%, 피해 +35%. 적 하나만 치는 스킬은 피해만 오릅니다.";
                case Lanternkeeper.Sim.Sigil.Haste: return "가속 — 재사용 대기시간 −25%, 피해 −15%. 더 자주 씁니다.";
                case Lanternkeeper.Sim.Sigil.Execute: return "처형 — 생명력이 30% 아래인 적에게 피해 +60%.";
                case Lanternkeeper.Sim.Sigil.Throng: return "군중 제어 — 대상 주변 3칸의 적 하나당 피해 +3%(최대 +30%). 큰 무리에 강합니다.";
                case Lanternkeeper.Sim.Sigil.Echo: return "메아리 — 0.6초 뒤 같은 자리에 절반 위력의 폭발이 한 번 더 일어납니다.";
                case Lanternkeeper.Sim.Sigil.DeepMark: return "깊은 각인 — 이 스킬이 일으키는 공명의 피해 +25%.";
                case Lanternkeeper.Sim.Sigil.Conductor: return "전도체 — 이 스킬로 시작된 사망 연쇄가 1번 더 이어집니다.";
                case Lanternkeeper.Sim.Sigil.Ripple: return "파문 — 이 스킬로 시작된 사망 연쇄의 폭발 범위 +30%.";
                case Lanternkeeper.Sim.Sigil.ToEmber: return "변환: 잿불 — 스킬의 원소가 잿불로 바뀝니다. 고리의 공명 순서를 맞출 때 씁니다.";
                case Lanternkeeper.Sim.Sigil.ToFrost: return "변환: 서리 — 스킬의 원소가 서리로 바뀝니다. 고리의 공명 순서를 맞출 때 씁니다.";
                case Lanternkeeper.Sim.Sigil.ToShock: return "변환: 전격 — 스킬의 원소가 전격으로 바뀝니다. 고리의 공명 순서를 맞출 때 씁니다.";
                case Lanternkeeper.Sim.Sigil.ToRot: return "변환: 부패 — 스킬의 원소가 부패로 바뀝니다. 고리의 공명 순서를 맞출 때 씁니다.";
                case Lanternkeeper.Sim.Sigil.ToVoid: return "변환: 공허 — 스킬의 원소가 공허로 바뀝니다. 고리의 공명 순서를 맞출 때 씁니다.";
                case Lanternkeeper.Sim.Sigil.EmberGround: return "잔불 지대 — 대상 자리에 2초 동안 불타는 장판을 남깁니다(0.5초마다 스킬 피해의 20%).";
                case Lanternkeeper.Sim.Sigil.Leech: return "흡혈 — 이 스킬로 준 피해의 2%만큼 생명력을 회복합니다.";
                case Lanternkeeper.Sim.Sigil.Rewind: return "되감기 — 이 스킬을 쓰면 1번 칸 스킬의 재사용 대기시간이 1초 줄어듭니다.";
                case Lanternkeeper.Sim.Sigil.Feed: return "공명 공급 — 이 스킬이 일으킨 공명이 공명 게이지를 50% 더 채웁니다.";
                default: return "";
            }
        }

        // ---------- 4. item stats ----------

        /// <summary>What the stat does in plain words, and who wants it.</summary>
        public static string Stat(Stat s)
        {
            switch (s)
            {
                case Lanternkeeper.Sim.Stat.FlatDamage: return "공격력 — 모든 스킬 피해의 바탕이 되는 수치입니다. 어느 직업이든 가장 확실한 공격 옵션입니다.";
                case Lanternkeeper.Sim.Stat.DamagePct: return "피해 % — 내가 주는 모든 피해가 늘어납니다. 모든 직업에게 좋습니다.";
                case Lanternkeeper.Sim.Stat.AttackSpeedPct: return "공격 속도 % — 스킬과 기본 공격을 더 자주 씁니다. 재사용이 짧은 스킬과 기본 공격 위주일수록 좋습니다.";
                case Lanternkeeper.Sim.Stat.MaxHp: return "최대 생명력 — 더 많이 맞고 버팁니다. 자주 죽는다면 가장 먼저 챙기세요.";
                case Lanternkeeper.Sim.Stat.DamageReductionPct: return "받는 피해 감소 % — 모든 피해를 덜 받습니다(장비 합계 최대 60%). 적 한가운데서 싸우는 근접 직업에게 중요합니다.";
                case Lanternkeeper.Sim.Stat.CritChancePct: return "치명타 확률 % — 치명타가 더 자주 터집니다(기본 5%, 최대 75%). 치명타 피해와 함께 올리면 좋습니다.";
                case Lanternkeeper.Sim.Stat.CritDamagePct: return "치명타 피해 % — 치명타가 더 세집니다(기본 1.5배). 치명타 확률이 높을수록 값어치가 큽니다.";
                case Lanternkeeper.Sim.Stat.MoveSpeedPct: return "이동 속도 % — 방과 방 사이를 빨리 다녀 사냥이 빨라집니다. 피해는 그대로입니다.";
                case Lanternkeeper.Sim.Stat.EmberPct: return "잿불 피해 % — 잿불(화염) 스킬의 피해만 늘어납니다. 잿불기사와 화염 별점술사에게 좋습니다.";
                case Lanternkeeper.Sim.Stat.FrostPct: return "서리 피해 % — 서리(냉기) 스킬의 피해만 늘어납니다. 서리 스킬이 고리에 많을 때만 좋습니다.";
                case Lanternkeeper.Sim.Stat.ShockPct: return "전격 피해 % — 전격(번개) 스킬의 피해만 늘어납니다. 서약기사·바람사냥꾼·번개 별점술사에게 좋습니다.";
                case Lanternkeeper.Sim.Stat.ResonancePct: return "공명 피해 % — 각인을 다른 원소로 터뜨릴 때 붙는 추가 피해가 늘어납니다. 원소를 섞어 쓰는 고리에 좋습니다.";
                case Lanternkeeper.Sim.Stat.ChainRadiusPct: return "연쇄 범위 % — 각인된 적이 죽을 때의 폭발이 넓어져 사망 연쇄가 더 잘 이어집니다. 무리 사냥용.";
                case Lanternkeeper.Sim.Stat.RegenPct: return "초당 생명력 회복 % — 매초 최대 생명력의 이만큼을 더 회복합니다(전투 중 기본 1%). 오래 버티는 데 좋습니다.";
                case Lanternkeeper.Sim.Stat.XpPct: return "경험치 획득량 % — 레벨이 더 빨리 오릅니다. 지금 당장의 전투력은 그대로입니다.";
                case Lanternkeeper.Sim.Stat.MagicFindPct: return "마법 아이템 발견 % — 희귀·전설·고유 장비가 더 자주 나옵니다. 전투력은 그대로입니다.";
                case Lanternkeeper.Sim.Stat.AreaPct: return "스킬 범위 % — 범위 스킬이 넓어져 한 번에 더 많은 적을 칩니다. 무리 정리, 특히 원거리 직업에게 좋습니다.";
                case Lanternkeeper.Sim.Stat.ProjectileCount: return "투사체 및 분신 — 쏘거나 대상에게 쓰는 스킬이 근처 다른 적에게 한 번 더 나갑니다(위력 60%). 원거리 직업에게 아주 좋습니다.";
                case Lanternkeeper.Sim.Stat.RangePct: return "사거리 % — 원거리 공격과 직선·낙하 스킬이 더 멀리서 나갑니다. 바람사냥꾼·별점술사용이고 근접 직업에겐 효과가 작습니다.";
                case Lanternkeeper.Sim.Stat.CooldownPct: return "재사용 대기시간 감소 % — 고리 스킬이 더 빨리 돌아옵니다(장비 합계 최대 40%). 재사용이 긴 스킬을 쓸수록 좋습니다.";
                default: return "";
            }
        }

        /// <summary>0 공격, 1 방어, 2 유틸.</summary>
        public static int StatGroup(Stat s)
        {
            switch (s)
            {
                case Lanternkeeper.Sim.Stat.MaxHp:
                case Lanternkeeper.Sim.Stat.DamageReductionPct:
                case Lanternkeeper.Sim.Stat.RegenPct:
                    return 1;
                case Lanternkeeper.Sim.Stat.MoveSpeedPct:
                case Lanternkeeper.Sim.Stat.XpPct:
                case Lanternkeeper.Sim.Stat.MagicFindPct:
                    return 2;
                default:
                    return 0;
            }
        }

        public static string StatGroupName(int g) => g == 1 ? "방어" : g == 2 ? "유틸" : "공격";

        // ---------- 5. rarity ----------

        /// <summary>일반 &lt; 마법 &lt; 희귀 &lt; 전설 &lt; 고유. Every item also has its base's own line (기본 옵션).</summary>
        public static string Rarity(Rarity r)
        {
            switch (r)
            {
                case Lanternkeeper.Sim.Rarity.Common: return "일반 — 가장 낮은 등급. 장비 종류마다 정해진 기본 옵션 한 줄만 있습니다.";
                case Lanternkeeper.Sim.Rarity.Magic: return "마법 — 일반보다 위. 기본 옵션에 추가 옵션이 1~2줄 붙습니다.";
                case Lanternkeeper.Sim.Rarity.Rare: return "희귀 — 마법보다 위. 추가 옵션이 3~4줄 붙고, ★ 대옵션(수치 1.5배인 줄)이 나올 수 있습니다.";
                case Lanternkeeper.Sim.Rarity.Legendary: return "전설 — 희귀보다 위. 추가 옵션 3줄에 전설 효과 하나가 붙고, ★ 대옵션도 나올 수 있습니다.";
                case Lanternkeeper.Sim.Rarity.Unique: return "고유 — 가장 높은 등급. 이름과 옵션 4줄, 고유 효과가 정해져 있는 장비로 통합 레벨 15부터 나옵니다.";
                default: return "";
            }
        }

        // ---------- 6. resonance ----------

        /// <summary>How it is set off (the mark on the enemy + the element that hits it) and what happens.</summary>
        public static string Resonance(Resonance r)
        {
            switch (r)
            {
                case Lanternkeeper.Sim.Resonance.Shatter: return "파쇄 — 잿불 각인이 있는 적을 서리로 치면 터집니다. 그 타격 피해 +50%, 주변 1.2칸의 적에게 타격 피해의 50%가 파편으로 튑니다.";
                case Lanternkeeper.Sim.Resonance.Overload: return "과부하 — 서리 각인이 있는 적을 전격으로 치면 터집니다. 대상이 1초 기절하고 근처 적 2명에게 타격 피해의 40%가 튑니다.";
                case Lanternkeeper.Sim.Resonance.Plague: return "역병 전도 — 전격 각인이 있는 적을 부패로 치면 터집니다. 주변 2칸의 적에게 부패 각인이 옮고 3초 동안 독(타격 피해의 60%)이 걸립니다.";
                case Lanternkeeper.Sim.Resonance.Devour: return "포식 — 부패 각인이 있는 적을 공허로 치면 터집니다. 타격 피해 +20%, 최대 생명력 2% 회복, 공명 게이지가 더 찹니다.";
                case Lanternkeeper.Sim.Resonance.Collapse: return "붕괴 — 공허 각인이 있는 적을 잿불로 치면 터집니다. 타격 피해 +50%, 주변 2.5칸의 적이 대상 쪽으로 끌려옵니다.";
                case Lanternkeeper.Sim.Resonance.Spark: return "약공명 — 순환 순서가 아닌 다른 원소로 각인을 치면 납니다. 타격 피해 +30%뿐이고 각인은 새 원소로 바뀝니다.";
                case Lanternkeeper.Sim.Resonance.Stack: return "중첩 — 각인과 같은 원소로 또 치면 쌓입니다(최대 5). 중첩 하나당 그 타격 피해 +10%.";
                default: return "";
            }
        }

        // ---------- 7. elements ----------

        /// <summary>What the element's mark is good for. The cycle: 잿불 → 서리 → 전격 → 부패 → 공허 → 잿불.</summary>
        public static string Element(Element e)
        {
            switch (e)
            {
                case Lanternkeeper.Sim.Element.Ember: return "잿불(화염) — 잿불 각인을 남깁니다. 그 각인을 서리로 치면 파쇄, 공허 각인을 잿불로 치면 붕괴. 별점술사의 잿불은 적을 3초간 불태웁니다(화상).";
                case Lanternkeeper.Sim.Element.Frost: return "서리(냉기) — 서리 각인을 남깁니다. 그 각인을 전격으로 치면 과부하, 잿불 각인을 서리로 치면 파쇄. 별점술사의 서리는 3번 맞히면 적을 얼립니다(빙결).";
                case Lanternkeeper.Sim.Element.Shock: return "전격(번개) — 전격 각인을 남깁니다. 그 각인을 부패로 치면 역병 전도, 서리 각인을 전격으로 치면 과부하. 별점술사의 전격은 짧게 기절시키고 옆의 적에게 튑니다(감전).";
                case Lanternkeeper.Sim.Element.Rot: return "부패 — 부패 각인을 남깁니다. 그 각인을 공허로 치면 포식, 전격 각인을 부패로 치면 역병 전도.";
                case Lanternkeeper.Sim.Element.Void: return "공허 — 공허 각인을 남깁니다. 그 각인을 잿불로 치면 붕괴, 부패 각인을 공허로 치면 포식.";
                default: return "무속성 — 각인을 남기지 않고 공명도 일으키지 않습니다.";
            }
        }

        // ---------- 8. glossary ----------

        /// <summary>Coined terms the UI uses, from the most basic to the advanced; one plain sentence each.</summary>
        public static readonly (string term, string meaning)[] Glossary =
        {
            ("공격력", "모든 스킬 피해의 바탕이 되는 수치로, 스킬 설명의 「공격력의 150%」는 공격력의 1.5배 피해라는 뜻입니다."),
            ("막", "모래에서 심연까지 이어지는 여섯 개의 큰 지역으로, 막마다 13층을 내려가면 마지막 층에서 막 보스를 만납니다."),
            ("정예", "「증식하는」「보호막」 같은 특수 능력이 붙은 강한 적으로, 장비와 룬을 더 많이 떨어뜨립니다."),
            ("떠도는 용사", "이벤트 방에서 만나는 아주 튼튼한 정예로, 쓰러뜨리면 마법 등급 이상의 장비 3개를 떨어뜨립니다."),
            ("막 보스", "막의 마지막 층을 지키는 보스로, 쓰러뜨리면 장비 8개(전설 1개 확정)를 떨어뜨립니다."),
            ("스킬 배치", "스킬을 넣는 네 칸(R)으로, 영웅은 1번 칸부터 순서대로 준비된 스킬을 알아서 쓰고 모두 대기 중이면 기본 공격을 합니다."),
            ("재사용 대기시간", "스킬을 한 번 쓴 뒤 다시 쓸 수 있을 때까지 기다리는 시간입니다."),
            ("궁극기", "통합 레벨 30에 스킬 트리에서 둘 중 하나를 고르는 가장 강한 스킬로, 정예·보스나 큰 무리 앞에서 알아서 씁니다."),
            ("원소", "스킬의 속성으로 잿불 → 서리 → 전격 → 부패 → 공허 → 다시 잿불의 순서로 돌아갑니다."),
            ("각인", "원소 스킬에 맞은 적에게 남는 그 원소의 표식으로, 다음에 맞는 원소에 따라 공명이 일어납니다."),
            ("공명", "각인이 있는 적을 순환의 바로 다음 원소로 치면 터지는 추가 효과로, 파쇄·과부하·역병 전도·포식·붕괴 다섯 가지가 있습니다."),
            ("약공명", "순서가 맞지 않는 다른 원소로 각인을 쳤을 때 나는 작은 추가 피해(+30%)입니다."),
            ("중첩", "각인과 같은 원소로 계속 치면 쌓이는 수치(최대 5)로, 하나당 그 타격의 피해가 10%씩 늘어납니다."),
            ("사망 연쇄", "각인이 있는 적이 죽으면 폭발하면서 주변 적에게 각인을 옮기는 현상으로, 그 폭발에 죽은 적이 또 터지며 기본 6번까지 이어집니다."),
            ("공명 게이지", "공명이 일어날 때마다 차오르는 막대로, 가득 차면 대공명이 터집니다."),
            ("대공명", "공명 게이지가 가득 찼을 때 주변 5칸의 모든 적에게 마지막으로 일으킨 공명이 한꺼번에 터지는 큰 폭발입니다."),
            ("자원", "직업마다 있는 막대(잿불기사 열기, 서약기사 맹세, 바람사냥꾼 집중, 별점술사 시간)로, 가득 차면 다음 고리 스킬이 위력 1.5배·범위 +30%로 나갑니다."),
            ("숙련", "스킬을 쓴 횟수만큼 오르는 스킬별 레벨(최대 60)로, 1레벨마다 그 스킬의 위력 +5%, 범위 +1.5%가 붙습니다."),
            ("분신", "스킬이 근처의 다른 적에게 위력 60%로 한 번 더 나가는 복제로, 숙련 10레벨마다 하나씩 늘어납니다."),
            ("진화", "숙련 10이 된 스킬을 스킬 트리의 진화 칸에서 더 강한 모습으로 바꾸는 것입니다."),
            ("스킬 트리", "통합 레벨이 오를 때마다 받는 포인트로 스킬·변형·진화·핵심 칸을 찍는 성장판으로, 초기화는 언제든 무료입니다."),
            ("갈래", "스킬 트리의 세 줄기(공격·방어·공명 계열)로, 한 갈래에 포인트를 모을수록 그 갈래의 핵심이 열립니다."),
            ("변형", "스킬 하나에만 걸리는 트리 강화로, 위력 +20%·범위 +20%·재사용 대기시간 −15%·분신 +1 중 두 가지가 스킬마다 정해져 있습니다."),
            ("핵심", "한 갈래에 포인트를 4·9·15 이상 넣으면 찍을 수 있는 칸으로, 수치가 아니라 전투 규칙 하나를 바꿉니다."),
            ("자동 분배", "지금 쓰는 스킬 배치에 맞춰 스킬 트리 포인트를 알아서 찍어 주는 기능으로, 켜 두면 트리를 열지 않아도 됩니다."),
            ("보조 문양", "통합 레벨 12부터 고리 칸마다 하나씩 끼우는 강화로, 그 칸의 스킬과 다음 칸의 스킬에 함께 적용됩니다."),
            ("추천 배치", "직업마다 준비된 스킬 배치 구성으로, 고르기만 하면 레벨에 맞춰 스킬과 문양이 알아서 채워집니다."),
            ("순서 시험", "지금 고리의 스킬 순서를 전부 40초씩 돌려 보고 피해가 가장 큰 순서를 알려 주는 기능입니다."),
            ("기본 옵션", "장비 종류마다 정해져 있는 첫 줄 옵션으로, 등급과 상관없이 항상 붙습니다."),
            ("★ 대옵션", "희귀 이상 장비에 드물게 붙는 금색 줄로, 같은 옵션보다 수치가 1.5배 높습니다."),
            ("요구 레벨", "장비가 효과를 내는 데 필요한 통합 레벨로, 모자라면 착용해도 옵션이 적용되지 않습니다."),
            ("전설·고유 효과", "전설과 고유 장비에 붙는 특수 효과 한 줄로, 수치가 아니라 스킬이나 사망 연쇄의 동작을 바꿉니다."),
            ("소켓", "무기·갑옷·투구에 뚫려 있을 수 있는 구멍(2~3칸)으로, 여기에 룬을 꽂습니다."),
            ("룬", "소켓에 꽂으면 옵션 한 줄을 더해 주는 돌로, 적을 잡으면 가끔 나오고 정예와 보스가 더 잘 줍니다."),
            ("룬어", "정해진 룬을 정해진 순서로 한 장비에 모두 꽂으면 생기는 강력한 추가 효과입니다."),
            ("잿 파편", "장비를 분해하면 나오는 재화로, 야영지의 명품화·재굴림·재련과 상인에게 씁니다."),
            ("등불 기름", "층에서 가끔 만나는 등불 기름 샘에서 얻는 귀한 재화로, 야영지 강화에 씁니다."),
            ("자동 분해", "정한 등급 이하의 장비를 줍는 즉시 잿 파편으로 바꾸는 필터로, 전설 이상·★·소켓 2칸 이상 장비는 항상 남깁니다."),
            ("야영지", "막 I의 보스를 처음 쓰러뜨리면 열리는 거점으로, 장비 강화·룬 승급·상인·환생을 여기서 합니다."),
            ("명품화", "야영지 대장장이가 장비 한 벌의 모든 옵션을 한 단계에 8%씩, 최대 12단계까지 올려 주는 강화입니다."),
            ("재굴림", "장비의 추가 옵션 한 줄을 새로 뽑는 제작으로, 한 장비에서는 처음 고른 한 줄만 계속 바꿀 수 있습니다."),
            ("재련", "장비에 추가 옵션 한 줄을 더하는 제작으로, 마법은 2줄, 희귀와 전설은 4줄까지 늘릴 수 있습니다."),
            ("안정도", "장비마다 정해진 제작 횟수 예산(마법 10, 희귀 20, 전설 30)으로, 재굴림에 4, 재련에 6이 들고 다 써도 장비는 부서지지 않습니다."),
            ("등불 점화", "Space나 등불 클릭으로 30초 동안 피해를 15% 올리는 기능으로, 3분마다 쓸 수 있고 층에서 처음 켜면 숨은 보물(희귀 장비 2개)도 찾습니다."),
            ("태세", "영웅이 싸우는 방식으로, 균형·맹공(주는 피해와 받는 피해 +12%)·신중(주는 피해 −8%, 받는 피해 −12%) 중에서 고릅니다."),
            ("통합 레벨", "현재 레벨에 잔불 레벨을 더한 값(최대 100)으로, 능력치·스킬 해금·장비 착용이 모두 이 레벨을 따릅니다."),
            ("환생", "레벨 60에서 야영지에서 할 수 있으며, 레벨은 1로 돌아가지만 장비는 그대로이고 쌓아 둔 잔불 레벨만큼 더 강해져 다시 내려갑니다."),
            ("잔불 레벨", "레벨 60 이후에 쌓인 경험치가 환생할 때 바뀌는 보너스 레벨(최대 40)로, 환생해도 사라지지 않고 계속 쌓입니다."),
            ("심지 레벨", "통합 레벨 100 이후의 경험치로 끝없이 오르는 레벨로, 1레벨마다 피해 +1%, 스킬 범위 +0.5%, 공격 속도 +0.2%, 이동 속도 +0.1%가 붙습니다."),
            ("증강 카드", "한 번의 삶마다 새로 고르는 강화 카드로, 시작할 때·레벨 20·레벨 40에 세 장 중 한 장을 고르고 환생하면 다시 뽑습니다."),
        };

        // ---------- 9. classes ----------

        /// <summary>Playstyle, what to look for on items, which skills to try first; null when the class is unknown.</summary>
        public static string ClassIntro(string classId)
        {
            switch (classId)
            {
                case "ashknight":
                    return "대검을 들고 적 무리 한가운데서 싸우는 전사입니다. 휠윈드로 돌면서 쓸어 담고, 잿불 베기 다음에 서리 낙인을 쳐서 파쇄를 터뜨려 보세요. "
                        + "장비는 공격력·잿불 피해·공격 속도를 먼저 보고, 자주 죽는다면 최대 생명력과 받는 피해 감소를 챙기세요.";
                case "oathknight":
                    return "방패와 금빛 번개로 버티면서 싸우는 성기사입니다. 축복의 망치로 주변을 치고, 방패 강타(서리) 다음에 심판(전격)을 써서 과부하를 일으켜 보세요. "
                        + "장비는 공격력·전격 피해에 최대 생명력과 받는 피해 감소를 함께 챙기면 안정적입니다.";
                case "windhunter":
                    return "거리를 두고 화살을 퍼붓는 궁수입니다. 갈래 화살과 속사가 주력이고, 적이 붙으면 회오리 칼날이나 구르기 사격으로 벗어납니다. "
                        + "장비는 공격력·전격 피해·투사체 및 분신·사거리가 좋고, 몸이 약하니 최대 생명력도 조금 챙기세요.";
                case "starseer":
                    return "잿불(화염)·서리(냉기)·전격(번개) 마법을 멀리서 쏟아붓는 마법사입니다. 화염구(화상)·얼음 화살(빙결)·연쇄 번개(감전) 중 한 원소로 시작해 같은 갈래의 스킬을 이어서 배우세요. "
                        + "장비는 고른 원소의 피해 %와 스킬 범위·투사체 및 분신·재사용 대기시간 감소가 좋습니다.";
                default:
                    return null;
            }
        }
    }
}
