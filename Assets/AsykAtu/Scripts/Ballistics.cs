using UnityEngine;

namespace AsykAtu
{
    /// <summary>Баллистика без сопротивления воздуха: с какой скоростью бросить, чтобы попасть в точку.</summary>
    public static class Ballistics
    {
        /// <summary>
        /// Скорость броска из from в to под углом angle к горизонту (град, плюс — вверх).
        /// false — под этим углом в точку не попасть (например, цель выше линии броска).
        /// </summary>
        public static bool LaunchVelocity(Vector3 from, Vector3 to, float angle, float gravity, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            Vector3 delta = to - from;
            var flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;
            float a = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a);
            // y(x) = x·tg(a) − g·x² / (2·v²·cos²(a))  →  v² = g·x² / (2·cos²(a)·(x·tg(a) − h))
            float denominator = 2f * cos * cos * (distance * Mathf.Tan(a) - delta.y);
            if (distance < 1e-4f || denominator <= 0f) return false;

            float speed = Mathf.Sqrt(gravity * distance * distance / denominator);
            velocity = flat / distance * (speed * cos) + Vector3.up * (speed * Mathf.Sin(a));
            return true;
        }
    }
}
