using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AsykAtu.Tests
{
    /// <summary>Бот в настоящей сцене и физике.</summary>
    public class BotPlayTests
    {
        [UnityTest]
        public IEnumerator PerfectBot_KnocksAsykOut()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var match = Object.FindAnyObjectByType<AsykMatch>();
            Assert.IsNotNull(match, "в сцене Main нет AsykMatch");
            match.StartMatch(AsykMatch.Mode.Training);
            var bot = match.bot;
            bot.gameObject.SetActive(true);
            bot.thinkTime = 0.1f;

            int knocked = -1;
            match.ThrowJudged += (player, count, foul) => knocked = count;
            Vector3 target = match.Kon[match.KonCount / 2].transform.position;
            bot.ThrowAt(target, BotLevel.Perfect);

            float deadline = Time.time + 10f;
            while (knocked < 0 && Time.time < deadline) yield return null;
            Assert.GreaterOrEqual(knocked, 1, "бот без ошибок должен выбить хотя бы один асык");
        }
    }
}
