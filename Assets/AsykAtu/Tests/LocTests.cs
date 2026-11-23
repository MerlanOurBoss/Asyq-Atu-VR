using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AsykAtu.Tests
{
    public class LocTests
    {
        Language saved;

        [SetUp]
        public void SetUp() => saved = Loc.Current;

        [TearDown]
        public void TearDown() => Loc.Set(saved);

        [Test]
        public void EveryKey_HasAllThreeLanguages()
        {
            foreach (var entry in Loc.Entries)
            {
                Assert.AreEqual(3, entry.Value.Length, entry.Key);
                for (int i = 0; i < 3; i++)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value[i]), $"{entry.Key}: нет перевода на {(Language)i}");
            }
        }

        [Test]
        public void Placeholders_MatchAcrossLanguages()
        {
            foreach (var entry in Loc.Entries)
            {
                string Holes(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v));
                string expected = Holes(entry.Value[1]);
                Assert.AreEqual(expected, Holes(entry.Value[0]), $"{entry.Key} (қазақша)");
                Assert.AreEqual(expected, Holes(entry.Value[2]), $"{entry.Key} (English)");
            }
        }

        [Test]
        public void SwitchingLanguage_ChangesTexts()
        {
            Loc.Set(Language.Kazakh);
            Assert.AreEqual("Жаттығу", Loc.Get("mode.training"));
            Assert.AreEqual("1-ойыншы: 2 асық шықты", Loc.F("hit", Loc.F("player", 1), 2));
            Loc.Next();
            Assert.AreEqual(Language.Russian, Loc.Current);
            Assert.AreEqual("Тренировка", Loc.Get("mode.training"));
            Loc.Next();
            Assert.AreEqual("Training", Loc.Get("mode.training"));
            Loc.Next();
            Assert.AreEqual(Language.Kazakh, Loc.Current);
        }

        [Test]
        public void MissingKey_ShowsKeyOrFallback()
        {
            Assert.AreEqual("no.such.key", Loc.Get("no.such.key"));
            Assert.AreEqual("Тест", Loc.GetOr("no.such.key", "Тест"));
        }

        [Test]
        public void SideNames_AreLocalized()
        {
            Loc.Set(Language.English);
            Assert.AreEqual("alshy", Asyk.SideName(AsykSide.Alshy));
            Loc.Set(Language.Kazakh);
            Assert.AreEqual("тәйке", Asyk.SideName(AsykSide.Taike));
            Assert.AreEqual("қырынан", Asyk.SideName(AsykSide.None));
        }
    }
}
