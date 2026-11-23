using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Поза руки на контроллере: пальцы сгибаются вслед за кнопкой Grip,
    /// а с сақа в руке остаются сжатыми вокруг него. Сгиб — поворот суставов вокруг их локальной оси X
    /// (так устроены руки из примера XR Hands: палец идёт вдоль +Z, ладонь смотрит в −Y).
    /// Вешается на корень модели руки.
    /// </summary>
    public class HandPose : MonoBehaviour
    {
        [Tooltip("Рука, чью кнопку Grip повторяют пальцы.")]
        public SaqaHand hand;
        [Tooltip("Насколько сгибается каждый сустав четырёх пальцев при полном сжатии, град.")]
        public float fingerCurl = 65f;
        [Tooltip("То же для большого пальца, град.")]
        public float thumbCurl = 30f;
        [Tooltip("С сақа в руке пальцы сжаты ровно настолько (0..1): обхватывают кость, а не смыкаются сквозь неё.")]
        [Range(0f, 1f)] public float holdCurl = 0.55f;
        [Tooltip("Как быстро пальцы догоняют кнопку, долей в секунду.")]
        public float speed = 10f;

        /// <summary>Текущее сжатие пальцев (0 — раскрыта, 1 — кулак).</summary>
        public float Curl { get; private set; }

        Transform[] fingers, thumb;
        Quaternion[] fingerRest, thumbRest;

        void Awake()
        {
            var finger = new System.Collections.Generic.List<Transform>();
            var thumbs = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool joint = n.EndsWith("Proximal") || n.EndsWith("Intermediate") || n.EndsWith("Distal");
                if (!joint) continue;
                (n.Contains("Thumb") ? thumbs : finger).Add(t);
            }
            fingers = finger.ToArray();
            thumb = thumbs.ToArray();
            fingerRest = Rest(fingers);
            thumbRest = Rest(thumb);
        }

        static Quaternion[] Rest(Transform[] joints)
        {
            var rest = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) rest[i] = joints[i].localRotation;
            return rest;
        }

        void LateUpdate()
        {
            float target = 0f;
            if (hand != null) target = hand.IsHolding ? holdCurl : hand.Squeeze;
            Curl = Mathf.MoveTowards(Curl, target, speed * Time.deltaTime);

            for (int i = 0; i < fingers.Length; i++)
                fingers[i].localRotation = fingerRest[i] * Quaternion.Euler(fingerCurl * Curl, 0f, 0f);
            for (int i = 0; i < thumb.Length; i++)
                thumb[i].localRotation = thumbRest[i] * Quaternion.Euler(thumbCurl * Curl, 0f, 0f);
        }
    }
}
