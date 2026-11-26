using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AsykAtu.Tests
{
    /// <summary>Сақа появляется у ног игрока, и взять его можно только рукой рядом с ним.</summary>
    public class PickupPlayTests
    {
        AsykMatch match;
        SaqaHand hand;

        IEnumerator StartTraining()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            // Эмулятор ПК сам двигает руку — выключаем, рукой управляет тест.
            Object.FindAnyObjectByType<DesktopXREmulator>().enabled = false;
            match = Object.FindAnyObjectByType<AsykMatch>();
            match.StartMatch(AsykMatch.Mode.Training);
            foreach (var h in Object.FindObjectsByType<SaqaHand>())
                if (h.side == HandSide.Right) hand = h;
            hand.squeezeOverride = 0f;
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Squeeze(float value)
        {
            hand.squeezeOverride = value;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Saqa_LiesAtPlayersFeet_BehindTheLine()
        {
            yield return StartTraining();
            var saqa = match.saqa;
            Vector3 head = match.head.position;
            var flat = new Vector2(saqa.transform.position.x - head.x, saqa.transform.position.z - head.z);
            Assert.Less(flat.magnitude, 0.7f, "сақа далеко от ног");
            Assert.Less(saqa.transform.position.y, 0.05f, "сақа должен лежать на земле");
            float fromCenter = Vector3.Dot(saqa.transform.position - match.orda.Center, -match.orda.transform.forward);
            Assert.Greater(fromCenter, match.Rules.throwDistance, "сақа за линией броска");
        }

        [UnityTest]
        public IEnumerator Grip_FarAway_DoesNotTake_Near_Takes()
        {
            yield return StartTraining();
            var saqa = match.saqa;

            hand.transform.position = saqa.transform.position + Vector3.up * 0.8f;   // рука высоко
            yield return Squeeze(1f);
            Assert.IsFalse(saqa.IsHeld, "издалека сақа взять нельзя");
            yield return Squeeze(0f);

            hand.transform.position = saqa.transform.position + Vector3.up * 0.04f;  // ладонь над сақа
            yield return null;
            Assert.IsTrue(hand.InReach, "ладонь рядом — сақа в досягаемости");
            yield return Squeeze(1f);
            Assert.IsTrue(saqa.IsHeld, "рядом сақа берётся");
            Assert.IsTrue(hand.IsHolding);
        }

        [UnityTest]
        public IEnumerator AfterThrow_SaqaReturnsToFeet()
        {
            yield return StartTraining();
            var saqa = match.saqa;
            saqa.Bone.PlaceAt(new Vector3(1.5f, 0.02f, -0.6f), Quaternion.identity);
            saqa.Release(new Vector3(0f, 0f, 5f), Vector3.zero);              // мимо кона
            float deadline = Time.time + 10f;
            while (match.StateName != "Ready" && Time.time < deadline) yield return null;
            Assert.AreEqual("Ready", match.StateName);
            Vector3 head = match.head.position;
            var flat = new Vector2(saqa.transform.position.x - head.x, saqa.transform.position.z - head.z);
            Assert.Less(flat.magnitude, 0.7f, "после броска сақа снова у ног");
        }
    }
}
