using NUnit.Framework;
using UnityEngine;

namespace AsykAtu.Tests
{
    public class BallisticsTests
    {
        const float G = 9.81f;

        /// <summary>Где окажется тело через расстояние по горизонтали, как у цели.</summary>
        static Vector3 PointAtTargetDistance(Vector3 from, Vector3 to, Vector3 velocity)
        {
            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            var flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float t = flat.magnitude / flatVelocity.magnitude;
            return from + velocity * t + 0.5f * Vector3.down * G * t * t;
        }

        [TestCase(0f)]
        [TestCase(10f)]
        [TestCase(-5f)]
        public void Velocity_HitsTarget(float angle)
        {
            var from = new Vector3(-1f, 0.35f, -3.1f);
            var to = new Vector3(0.1f, 0.0175f, 0f);
            Assert.IsTrue(Ballistics.LaunchVelocity(from, to, angle, G, out Vector3 v));
            Vector3 hit = PointAtTargetDistance(from, to, v);
            Assert.AreEqual(0f, Vector3.Distance(hit, to), 1e-3f);
            Assert.AreEqual(angle, Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg, 1e-3f);
        }

        [Test]
        public void Unreachable_ReturnsFalse()
        {
            // Цель выше линии броска: прямо или вниз до неё не долететь.
            var from = Vector3.zero;
            var to = new Vector3(0f, 1f, 2f);
            Assert.IsFalse(Ballistics.LaunchVelocity(from, to, 0f, G, out _));
            Assert.IsTrue(Ballistics.LaunchVelocity(from, to, 45f, G, out _));
        }
    }
}
