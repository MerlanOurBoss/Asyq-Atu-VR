using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Бросающая рука персонажа бота: двухзвенная IK на костях скелета (плечо и предплечье).
    /// Кисть ведёт к цели BotPlayer (замах и бросок), рука сгибается в локте, рукав гнётся вместе с костями.
    /// Остальная поза (на колене, левая рука на колене) — исходная поза модели.
    /// </summary>
    public class BotBoneRig : MonoBehaviour
    {
        [Tooltip("Кость плеча бросающей руки (upperarm).")]
        public Transform upperArm;
        [Tooltip("Кость предплечья (forearm).")]
        public Transform forearm;
        [Tooltip("Кость кисти (hand): по ней меряем длину предплечья.")]
        public Transform hand;
        [Tooltip("Куда тянется кисть — её двигает BotPlayer.")]
        public Transform target;
        [Tooltip("Куда сгибается локоть, в координатах бота.")]
        public Vector3 elbowHint = new Vector3(1f, -0.3f, -0.5f);

        float upperLength, lowerLength;
        Vector3 upperAxis, lowerAxis;
        Quaternion upperRest, lowerRest;

        void Awake()
        {
            if (upperArm == null || forearm == null || hand == null) return;
            upperLength = (forearm.position - upperArm.position).magnitude;
            lowerLength = (hand.position - forearm.position).magnitude;
            // Вдоль какой локальной оси идёт кость — у разных скелетов по-разному, поэтому меряем.
            upperAxis = upperArm.InverseTransformDirection(forearm.position - upperArm.position).normalized;
            lowerAxis = forearm.InverseTransformDirection(hand.position - forearm.position).normalized;
            upperRest = upperArm.localRotation;
            lowerRest = forearm.localRotation;
        }

        void LateUpdate()
        {
            if (upperArm == null || target == null || upperLength <= 0f) return;
            // Каждый кадр от исходной позы: без накопления поворота вокруг кости.
            upperArm.localRotation = upperRest;
            forearm.localRotation = lowerRest;

            Vector3 shoulder = upperArm.position;
            Vector3 toTarget = target.position - shoulder;
            float a = upperLength, b = lowerLength;
            // Дальше вытянутой руки не дотянуться: рука выпрямляется в сторону цели.
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 0.001f, (a + b) * 0.999f);
            Vector3 dir = toTarget.normalized;

            // Теорема косинусов: где локоть вдоль линии плечо—цель и насколько он в стороне.
            float along = (a * a - b * b + d * d) / (2f * d);
            float side = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(transform.TransformDirection(elbowHint), dir).normalized;
            Vector3 elbow = shoulder + dir * along + bend * side;
            Vector3 wrist = shoulder + dir * d;

            Aim(upperArm, upperAxis, elbow - shoulder);
            Aim(forearm, lowerAxis, wrist - forearm.position);
        }

        static void Aim(Transform bone, Vector3 localAxis, Vector3 direction)
        {
            Vector3 current = bone.TransformDirection(localAxis);
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }
    }
}
