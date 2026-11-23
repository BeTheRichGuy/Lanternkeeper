using System;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>
    /// UX plan B1 (희승 11-23 "스킬들도 각각 설명이 없고"): every skill a player can meet — each class's skills, their
    /// evolutions and the basic attacks — and every glyph, stat, rarity and resonance has its explanation. A skill added
    /// later without a line fails here instead of reaching the screen nameless.
    /// </summary>
    public class GameTextCoverageTests
    {
        [Test]
        public void Every_class_skill_and_its_evolution_is_explained()
        {
            foreach (var cls in SkillBook.PlayableClasses)
            {
                foreach (var s in SkillBook.For(cls))
                {
                    Assert.That(GameText.Skill(s.Id), Is.Not.Null.And.Not.Empty, cls + " · " + s.Id);
                    if (s.EvolveTo != null) Assert.That(GameText.Skill(s.EvolveTo), Is.Not.Null.And.Not.Empty, cls + " · " + s.EvolveTo);
                }
                Assert.That(GameText.Skill(SkillBook.BasicFor(cls).Id), Is.Not.Null.And.Not.Empty, cls + " basic");
            }
        }

        [Test]
        public void Every_glyph_stat_rarity_resonance_and_element_is_explained()
        {
            foreach (Sigil g in Enum.GetValues(typeof(Sigil)))
                if (g != Sigil.None) Assert.That(GameText.Sigil(g), Is.Not.Empty, g.ToString());
            foreach (Stat s in Enum.GetValues(typeof(Stat))) Assert.That(GameText.Stat(s), Is.Not.Empty, s.ToString());
            foreach (Rarity r in Enum.GetValues(typeof(Rarity))) Assert.That(GameText.Rarity(r), Is.Not.Empty, r.ToString());
            foreach (Resonance r in Enum.GetValues(typeof(Resonance)))
                if (r != Resonance.None) Assert.That(GameText.Resonance(r), Is.Not.Empty, r.ToString());
            foreach (Element e in Enum.GetValues(typeof(Element))) Assert.That(GameText.Element(e), Is.Not.Empty, e.ToString());
        }

        [Test]
        public void Every_class_has_an_intro_and_the_glossary_has_no_blank_line()
        {
            foreach (var cls in SkillBook.PlayableClasses) Assert.That(GameText.ClassIntro(cls), Is.Not.Null.And.Not.Empty, cls);
            foreach (var (term, meaning) in GameText.Glossary)
            {
                Assert.That(term, Is.Not.Empty);
                Assert.That(meaning, Is.Not.Empty, term);
            }
        }
    }
}
