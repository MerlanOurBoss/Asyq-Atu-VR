using NUnit.Framework;
using UnityEngine;

namespace AsykAtu.Tests
{
    public class AsykGameTests
    {
        AsykRules rules;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<AsykRules>();
            rules.winTarget = 8;
            rules.gamesToWin = 2;
            rules.throwAgainOnHit = true;
            rules.saqaInOrdaEndsTurn = false;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        AsykGame NewGame(int first = 0)
        {
            var game = new AsykGame(rules, 2);
            game.StartGame(first);
            return game;
        }

        [Test]
        public void Hit_ThrowsAgain()
        {
            var game = NewGame();
            Assert.AreEqual(TurnResult.ThrowAgain, game.Throw(2, false, false));
            Assert.AreEqual(0, game.Current);
            Assert.AreEqual(2, game.Knocked[0]);
        }

        [Test]
        public void Hit_PassesTurn_WhenThrowAgainOff()
        {
            rules.throwAgainOnHit = false;
            var game = NewGame();
            Assert.AreEqual(TurnResult.NextPlayer, game.Throw(2, false, false));
            Assert.AreEqual(1, game.Current);
        }

        [Test]
        public void Miss_PassesTurn()
        {
            var game = NewGame();
            Assert.AreEqual(TurnResult.NextPlayer, game.Throw(0, false, false));
            Assert.AreEqual(1, game.Current);
        }

        [Test]
        public void SaqaInOrda_EndsTurn_OnlyWhenRuleOn()
        {
            var game = NewGame();
            Assert.AreEqual(TurnResult.ThrowAgain, game.Throw(1, true, false));

            rules.saqaInOrdaEndsTurn = true;
            Assert.AreEqual(TurnResult.NextPlayer, game.Throw(1, true, false));
            Assert.AreEqual(1, game.Current);
        }

        [Test]
        public void Foul_DoesNotScore_AndPassesTurn()
        {
            var game = NewGame();
            Assert.AreEqual(TurnResult.Foul, game.Throw(3, false, true));
            Assert.AreEqual(0, game.Knocked[0]);
            Assert.AreEqual(1, game.Current);
        }

        [Test]
        public void ReachingTarget_WinsGame_ThenMatch()
        {
            var game = NewGame();
            game.Throw(5, false, false);
            Assert.AreEqual(TurnResult.GameWon, game.Throw(4, false, false));
            Assert.AreEqual(1, game.GamesWon[0]);
            Assert.AreEqual(-1, game.MatchWinner);

            game.StartGame(1);
            Assert.AreEqual(0, game.Knocked[0], "счёт партии обнуляется");
            Assert.AreEqual(2, game.GameNumber);
            game.Throw(0, false, false); // игрок 2 мимо
            Assert.AreEqual(TurnResult.MatchWon, game.Throw(8, false, false));
            Assert.AreEqual(0, game.MatchWinner);
        }

        [Test]
        public void SinglePlayer_KeepsTurn()
        {
            var game = new AsykGame(rules, 1);
            game.StartGame(0);
            Assert.AreEqual(TurnResult.NextPlayer, game.Throw(0, false, false));
            Assert.AreEqual(0, game.Current);
        }

        [Test]
        public void Toss_BestSideGoesFirst()
        {
            Assert.AreEqual(1, AsykGame.FirstByToss(rules, new[] { AsykSide.Shik, AsykSide.Alshy }));
            Assert.AreEqual(0, AsykGame.FirstByToss(rules, new[] { AsykSide.Taike, AsykSide.Buk }));
        }

        [Test]
        public void Toss_TieAtBest_Retoss()
        {
            Assert.AreEqual(-1, AsykGame.FirstByToss(rules, new[] { AsykSide.Buk, AsykSide.Buk }));
        }

        [Test]
        public void Toss_TieBelowBest_DoesNotMatter()
        {
            var sides = new[] { AsykSide.Shik, AsykSide.Alshy, AsykSide.Shik };
            Assert.AreEqual(1, AsykGame.FirstByToss(rules, sides));
        }

        [Test]
        public void UpSide_FollowsRotation()
        {
            var go = new GameObject("Asyk", typeof(Rigidbody), typeof(Asyk));
            var asyk = go.GetComponent<Asyk>();
            try
            {
                Assert.AreEqual(AsykSide.Buk, asyk.UpSide);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                Assert.AreEqual(AsykSide.Shik, asyk.UpSide);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // +X смотрит вверх
                Assert.AreEqual(AsykSide.Alshy, asyk.UpSide);
                go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                Assert.AreEqual(AsykSide.Taike, asyk.UpSide);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // на торце
                Assert.AreEqual(AsykSide.None, asyk.UpSide);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
