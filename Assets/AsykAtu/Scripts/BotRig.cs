using System;
using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Руки персонажа бота: двухзвенная IK от плеча к цели. Правую кисть двигает BotPlayer
    /// (замах и бросок), рука сгибается в локте вслед за ней. Левая лежит на колене.
    /// Сегменты рук — меши единичной длины вдоль +Y: длину задаём масштабом по Y.
    /// </summary>
    public class BotRig : MonoBehaviour
    {
        [Serializable]
        public struct Arm
        {
            [Tooltip("Точка плеча на теле.")]
            public Transform shoulder;
            [Tooltip("Куда тянется кисть.")]
            public Transform target;
            public Transform upper, lower, hand;
            public float upperLength, lowerLength;
            [Tooltip("Куда сгибается локоть, в координатах бота.")]
            public Vector3 elbowHint;
        }

        public Arm right, left;

        void LateUpdate()
        {
            Solve(right);
            Solve(left);
        }

        void Solve(Arm arm)
        {
            if (arm.shoulder == null || arm.target == null) return;
            Vector3 shoulder = arm.shoulder.position;
            Vector3 toTarget = arm.target.position - shoulder;
            float a = arm.upperLength, b = arm.lowerLength;
            // Дальше вытянутой руки не дотянуться: рука выпрямляется в сторону цели.
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 0.001f, (a + b) * 0.999f);
            Vector3 dir = toTarget.normalized;

            // Теорема косинусов: где локоть вдоль линии плечо—цель и насколько он в стороне.
            float along = (a * a - b * b + d * d) / (2f * d);
            float side = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(transform.TransformDirection(arm.elbowHint), dir).normalized;
            Vector3 elbow = shoulder + dir * along + bend * side;
            Vector3 wrist = shoulder + dir * d;

            Place(arm.upper, shoulder, elbow);
            Place(arm.lower, elbow, wrist);
            if (arm.hand != null)
                arm.hand.SetPositionAndRotation(wrist, Quaternion.FromToRotation(Vector3.up, (wrist - elbow).normalized));
        }

        static void Place(Transform segment, Vector3 from, Vector3 to)
        {
            if (segment == null) return;
            Vector3 v = to - from;
            segment.SetPositionAndRotation(from, Quaternion.FromToRotation(Vector3.up, v.normalized));
            segment.localScale = new Vector3(1f, v.magnitude, 1f);
        }
    }
}
