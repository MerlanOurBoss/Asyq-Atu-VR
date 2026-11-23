using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AsykAtu.Tests
{
    /// <summary>Звуки костей в настоящей сцене и физике.</summary>
    public class BoneSoundPlayTests
    {
        AsykMatch match;

        IEnumerator StartTraining()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            match = Object.FindAnyObjectByType<AsykMatch>();
            match.StartMatch(AsykMatch.Mode.Training);
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator PlacingKon_IsSilent()
        {
            yield return StartTraining();
            foreach (var asyk in match.Kon)
                Assert.Less(asyk.GetComponent<BoneSound>().LastPlayTime, 0f, $"{asyk.name} звучит, хотя его просто поставили");
        }

        [UnityTest]
        public IEnumerator SaqaHittingKon_ClicksBoneOnBone()
        {
            yield return StartTraining();
            var saqa = match.saqa;
            Vector3 target = match.Kon[match.KonCount / 2].transform.position;
            saqa.Bone.PlaceAt(new Vector3(target.x, 0.02f, target.z - 0.5f), Quaternion.identity);
            float released = Time.time;
            saqa.Release(new Vector3(0f, 0f, 8f), Vector3.zero);

            bool boneClick = false;
            float deadline = Time.time + 3f;
            while (!boneClick && Time.time < deadline)
            {
                yield return null;
                foreach (var sound in Object.FindObjectsByType<BoneSound>())
                    if (sound.LastPlayTime >= released && sound.LastHitBone) boneClick = true;
            }
            Assert.IsTrue(boneClick, "удар сақа в кон должен дать щелчок кость о кость");
        }
    }
}
