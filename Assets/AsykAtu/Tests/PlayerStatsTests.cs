using NUnit.Framework;
using UnityEngine;

namespace AsykAtu.Tests
{
    public class PlayerStatsTests
    {
        string savedPrefix;

        [SetUp]
        public void SetUp()
        {
            savedPrefix = PlayerStats.Prefix;
            PlayerStats.Prefix = "AsykAtu.Test.Stats.";      // настоящую статистику не трогаем
            PlayerStats.Clear(3, "Training", "Sport");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerStats.Clear(3, "Training", "Sport");
            PlayerStats.Prefix = savedPrefix;
        }

        [Test]
        public void Accuracy_CountsThrowsWithAHit()
        {
            Assert.AreEqual(0, PlayerStats.AccuracyPercent);
            PlayerStats.RecordThrow(2);
            PlayerStats.RecordThrow(0);
            PlayerStats.RecordThrow(1);
            PlayerStats.RecordThrow(0);
            Assert.AreEqual(4, PlayerStats.Throws);
            Assert.AreEqual(2, PlayerStats.Hits);
            Assert.AreEqual(3, PlayerStats.Knocked);
            Assert.AreEqual(50, PlayerStats.AccuracyPercent);
        }

        [Test]
        public void TrainingRecord_KeepsFewestThrows_PerRules()
        {
            Assert.IsTrue(PlayerStats.RecordTrainingGame("Training", 14), "первая партия — сразу рекорд");
            Assert.IsFalse(PlayerStats.RecordTrainingGame("Training", 16));
            Assert.IsFalse(PlayerStats.RecordTrainingGame("Training", 14), "повторить рекорд — не новый рекорд");
            Assert.IsTrue(PlayerStats.RecordTrainingGame("Training", 11));
            Assert.AreEqual(11, PlayerStats.BestTraining("Training"));
            Assert.AreEqual(0, PlayerStats.BestTraining("Sport"), "у других правил свой рекорд");
        }

        [Test]
        public void BotMatches_CountedPerLevel()
        {
            PlayerStats.RecordBotMatch(1, true);
            PlayerStats.RecordBotMatch(1, false);
            PlayerStats.RecordBotMatch(1, true);
            PlayerStats.RecordBotMatch(2, false);
            Assert.AreEqual(2, PlayerStats.BotWins(1));
            Assert.AreEqual(1, PlayerStats.BotLosses(1));
            Assert.AreEqual(0, PlayerStats.BotWins(2));
            Assert.AreEqual(1, PlayerStats.BotLosses(2));
        }
    }

    public class SaqaVariantTests
    {
        [Test]
        public void ApplyVariant_ChangesMassSpeedAndMaterial()
        {
            var go = new GameObject("Saqa", typeof(MeshRenderer), typeof(Rigidbody), typeof(Asyk), typeof(Saqa));
            var red = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var lead = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var saqa = go.GetComponent<Saqa>();
                saqa.variants = new[]
                {
                    new SaqaVariant { key = "saqa.red", material = red, mass = 0.04f, speedFactor = 1f },
                    new SaqaVariant { key = "saqa.quyma", material = lead, mass = 0.09f, speedFactor = 0.85f },
                };
                saqa.ApplyVariant(1);
                Assert.AreEqual(0.09f, go.GetComponent<Rigidbody>().mass, 1e-5f);
                Assert.AreEqual(0.85f, saqa.SpeedFactor, 1e-5f);
                Assert.AreSame(lead, go.GetComponent<MeshRenderer>().sharedMaterial);

                saqa.ApplyVariant(0);
                Assert.AreEqual(0.04f, go.GetComponent<Rigidbody>().mass, 1e-5f);
                Assert.AreEqual(1f, saqa.SpeedFactor, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(red);
                Object.DestroyImmediate(lead);
            }
        }

        [Test]
        public void SaqaNames_AreTranslated()
        {
            foreach (var key in new[] { "saqa.red", "saqa.blue", "saqa.white", "saqa.quyma" })
                Assert.IsTrue(Loc.Entries.ContainsKey(key), key);
        }
    }
}
