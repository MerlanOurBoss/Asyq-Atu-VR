using UnityEngine;

namespace AsykAtu
{
    /// <summary>Какой стороной вверх лежит кость. None — на торце или не лёг ровно.</summary>
    public enum AsykSide { None, Alshy, Taike, Buk, Shik }

    /// <summary>
    /// Кость на земле: асык в коне или сақа. Настраивает Rigidbody под мелкий лёгкий предмет,
    /// умеет встать на место неподвижно и сказать, какой стороной лежит.
    /// Общие настройки физики (contact offset, итерации, порог отскока) лежат в Project Settings → Physics.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Asyk : MonoBehaviour
    {
        [Tooltip("Быстрая кость (сақа): непрерывная проверка столкновений, иначе на скорости пролетит сквозь асык.")]
        public bool fast;
        [Tooltip("Медленнее этого кость считается остановившейся, м/с.")]
        public float restSpeed = 0.03f;
        [Tooltip("Медленнее этого вращение считается остановившимся, рад/с.")]
        public float restAngularSpeed = 0.6f;

        [Header("Стороны: какая локальная ось смотрит вверх")]
        public Vector3 alshyUp = Vector3.right;
        public Vector3 taikeUp = Vector3.left;
        public Vector3 bukUp = Vector3.up;
        public Vector3 shikUp = Vector3.down;
        [Tooltip("Ось стороны должна смотреть вверх не хуже этого (косинус угла), иначе сторона не определена.")]
        [Range(0f, 1f)] public float sideThreshold = 0.8f;

        public Rigidbody Body { get; private set; }

        /// <summary>Какой стороной вверх лежит кость сейчас.</summary>
        public AsykSide UpSide
        {
            get
            {
                AsykSide best = AsykSide.None;
                float bestDot = sideThreshold;
                Check(alshyUp, AsykSide.Alshy, ref best, ref bestDot);
                Check(taikeUp, AsykSide.Taike, ref best, ref bestDot);
                Check(bukUp, AsykSide.Buk, ref best, ref bestDot);
                Check(shikUp, AsykSide.Shik, ref best, ref bestDot);
                return best;
            }
        }

        void Check(Vector3 localAxis, AsykSide side, ref AsykSide best, ref float bestDot)
        {
            float dot = Vector3.Dot(transform.TransformDirection(localAxis.normalized), Vector3.up);
            if (dot <= bestDot) return;
            bestDot = dot;
            best = side;
        }

        public static string SideName(AsykSide side) => Loc.Get(side switch
        {
            AsykSide.Alshy => "side.alshy",
            AsykSide.Taike => "side.taike",
            AsykSide.Buk => "side.buk",
            AsykSide.Shik => "side.shik",
            _ => "side.none",
        });

        public bool IsResting =>
            Body.isKinematic || Body.IsSleeping() ||
            (Body.linearVelocity.sqrMagnitude < restSpeed * restSpeed &&
             Body.angularVelocity.sqrMagnitude < restAngularSpeed * restAngularSpeed);

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = fast
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>Поставить кость неподвижно в точку.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            Body.Sleep();
        }
    }
}
