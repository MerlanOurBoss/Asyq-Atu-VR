using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR.Input;

namespace AsykAtu
{
    public enum HandSide { Left, Right }

    /// <summary>
    /// Рука, которая бросает сақа. Grip пустой рукой — сақа сразу оказывается в ладони
    /// (поднимать его с пола не нужно). Отпустить Grip — бросок.
    /// Скорость броска — не последний кадр, а среднее нескольких самых быстрых кадров
    /// за последние ~0.1 с: игрок отпускает кнопку чуть позже пика скорости руки.
    /// Вешается на объект контроллера (с TrackedPoseDriver).
    /// </summary>
    public class SaqaHand : MonoBehaviour
    {
        public HandSide side = HandSide.Right;
        public Saqa saqa;
        [Tooltip("Точка в ладони, где лежит сақа. Пусто — центр контроллера.")]
        public Transform attach;

        [Header("Кнопка Grip")]
        [Range(0f, 1f)] public float pressThreshold = 0.55f;
        [Range(0f, 1f)] public float releaseThreshold = 0.35f;
        [Tooltip("Отладка без шлема: 0..1 заменяет кнопку, -1 — читать контроллер.")]
        [Range(-1f, 1f)] public float squeezeOverride = -1f;

        [Header("Бросок")]
        [Tooltip("Множитель скорости броска. В VR броски без него ощущаются слабее, чем в жизни.")]
        public float throwBoost = 1.15f;
        [Tooltip("За сколько последних секунд искать пик скорости руки.")]
        public float peakWindow = 0.1f;
        [Tooltip("Сколько самых быстрых кадров из окна усреднять.")]
        [Range(1, 8)] public int peakFrames = 3;

        public bool IsHolding => saqa != null && saqa.IsHeld && saqa.Holder == this;
        /// <summary>Насколько сжата кнопка (0..1).</summary>
        public float Squeeze { get; private set; }

        struct Sample
        {
            public float time;
            public Vector3 position;
            public Quaternion rotation;
        }

        const int BufferSize = 32;
        readonly Sample[] samples = new Sample[BufferSize];
        readonly Vector3[] linear = new Vector3[BufferSize];
        readonly Vector3[] angular = new Vector3[BufferSize];
        readonly bool[] used = new bool[BufferSize];
        int sampleCount, nextSample;
        bool pressed;
        InputAction gripAction, hapticAction;

        void Awake()
        {
            if (attach == null) attach = transform;
        }

        void OnEnable()
        {
            string usage = side == HandSide.Left ? "{LeftHand}" : "{RightHand}";
            gripAction = new InputAction(side + " Grip", InputActionType.Value, $"<XRController>{usage}/{{Grip}}", expectedControlType: "Axis");
            hapticAction = new InputAction(side + " Haptic", InputActionType.PassThrough, $"<XRController>{usage}/haptic");
            gripAction.Enable();
            hapticAction.Enable();
        }

        void OnDisable()
        {
            if (IsHolding) saqa.Release(Vector3.zero, Vector3.zero);
            gripAction?.Dispose();
            hapticAction?.Dispose();
            gripAction = hapticAction = null;
        }

        // LateUpdate: к этому моменту поза контроллера (и эмулятора) за кадр уже обновлена.
        void LateUpdate()
        {
            Squeeze = squeezeOverride >= 0f ? squeezeOverride : gripAction?.ReadValue<float>() ?? 0f;
            bool wasPressed = pressed;
            if (Squeeze >= pressThreshold) pressed = true;
            else if (Squeeze < releaseThreshold) pressed = false;

            Record();

            if (IsHolding)
            {
                if (!pressed) Throw();
            }
            else if (pressed && !wasPressed && saqa != null && saqa.CanTake)
            {
                saqa.Hold(this, attach);
                Pulse(0.3f, 0.04f);
            }
        }

        void Record()
        {
            samples[nextSample] = new Sample { time = Time.time, position = attach.position, rotation = attach.rotation };
            nextSample = (nextSample + 1) % BufferSize;
            if (sampleCount < BufferSize) sampleCount++;
        }

        void Throw()
        {
            Vector3 velocity = PeakVelocity(out Vector3 angularVelocity) * throwBoost;
            saqa.Release(velocity, angularVelocity);
            Pulse(0.5f, 0.05f);
        }

        /// <summary>Средняя скорость нескольких самых быстрых кадров за последние peakWindow секунд.</summary>
        Vector3 PeakVelocity(out Vector3 angularVelocity)
        {
            int newest = (nextSample - 1 + BufferSize) % BufferSize;
            float now = samples[newest].time;
            int n = 0;
            for (int i = 0; i < sampleCount - 1; i++)
            {
                Sample a = samples[(newest - i - 1 + BufferSize) % BufferSize];
                Sample b = samples[(newest - i + BufferSize) % BufferSize];
                // Последний интервал берём всегда: при просадке кадр может быть длиннее окна.
                if (n > 0 && now - a.time > peakWindow) break;
                float dt = b.time - a.time;
                if (dt <= 1e-5f) continue;
                linear[n] = (b.position - a.position) / dt;
                angular[n] = ToAngularVector(b.rotation * Quaternion.Inverse(a.rotation)) / dt;
                used[n] = false;
                n++;
            }

            Vector3 sumLinear = Vector3.zero, sumAngular = Vector3.zero;
            int picked = 0;
            for (; picked < peakFrames && picked < n; picked++)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                    if (!used[i] && (best < 0 || linear[i].sqrMagnitude > linear[best].sqrMagnitude))
                        best = i;
                used[best] = true;
                sumLinear += linear[best];
                sumAngular += angular[best];
            }

            angularVelocity = picked > 0 ? sumAngular / picked : Vector3.zero;
            return picked > 0 ? sumLinear / picked : Vector3.zero;
        }

        /// <summary>Вибрация контроллера этой руки.</summary>
        public void Pulse(float amplitude, float duration)
        {
            if (hapticAction != null) OpenXRInput.SendHapticImpulse(hapticAction, amplitude, duration);
        }

        /// <summary>Кватернион поворота → вектор (ось × угол в радианах), кратчайший путь.</summary>
        static Vector3 ToAngularVector(Quaternion q)
        {
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            q.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return Vector3.zero;
            if (angle > 180f) angle -= 360f;
            return axis * (angle * Mathf.Deg2Rad);
        }
    }
}
