using System;
using UnityEngine;

namespace AsykAtu
{
    /// <summary>Вариант сақа: как выглядит, сколько весит и насколько сильно его получается бросить.</summary>
    [Serializable]
    public struct SaqaVariant
    {
        [Tooltip("Ключ названия в Loc.")]
        public string key;
        public Material material;
        [Tooltip("Масса, кг. Тяжелее — сильнее бьёт по асыкам.")]
        public float mass;
        [Tooltip("Множитель скорости броска: тяжёлый сақа рука разгоняет хуже.")]
        [Range(0.5f, 1f)] public float speedFactor;
    }

    /// <summary>
    /// Сақа: бита, которой выбивают асыки. В руке жёстко привязан к ладони и не сталкивается
    /// ни с чем, иначе его можно было бы просто протащить через кон. Отпущенный летит со скоростью руки.
    /// Подсвечивается, когда до него может дотянуться рука.
    /// </summary>
    [RequireComponent(typeof(Asyk))]
    public class Saqa : MonoBehaviour
    {
        [Tooltip("Упал ниже этой высоты — считаем потерянным, м.")]
        public float lostBelowY = -3f;
        [Tooltip("Сколько лежать неподвижно подряд, чтобы считаться остановившимся, с. " +
                 "Иначе в верхней точке отскока скорость на миг почти ноль.")]
        public float restDuration = 0.2f;

        [Header("Варианты")]
        public SaqaVariant[] variants;
        [Tooltip("Цвет подсветки, когда рука рядом.")]
        public Color glowColor = new Color(0.45f, 0.35f, 0.12f);

        public Asyk Bone { get; private set; }
        public int VariantIndex { get; private set; }
        public SaqaVariant Variant => variants != null && variants.Length > 0 ? variants[VariantIndex] : default;
        /// <summary>Множитель скорости броска текущего варианта.</summary>
        public float SpeedFactor { get; private set; } = 1f;
        /// <summary>Рука игрока, которая держит сақа. null — держит бот или никто.</summary>
        public SaqaHand Holder { get; private set; }
        public bool IsHeld { get; private set; }
        /// <summary>Брошен и ещё не остановился.</summary>
        public bool InFlight { get; private set; }
        /// <summary>Сейчас не ваш ход: взять сақа нельзя.</summary>
        public bool Locked { get; set; }
        public bool CanTake => !IsHeld && !InFlight && !Locked;

        /// <summary>Бросок: какая рука и с какой скоростью.</summary>
        public event Action<SaqaHand, Vector3> Thrown;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        float restingSince = -1f, glowUntil = -1f;
        bool glowing;
        Renderer view;
        MaterialPropertyBlock block;

        void Awake()
        {
            Bone = GetComponent<Asyk>();
            view = GetComponentInChildren<Renderer>();
        }

        /// <summary>Сменить вариант: материал, масса и сила броска.</summary>
        public void ApplyVariant(int index)
        {
            if (variants == null || variants.Length == 0) return;
            if (Bone == null) Awake();
            VariantIndex = Mathf.Clamp(index, 0, variants.Length - 1);
            var v = variants[VariantIndex];
            if (v.material != null && view != null) view.sharedMaterial = v.material;
            if (v.mass > 0f) Bone.Body.mass = v.mass;
            SpeedFactor = v.speedFactor > 0f ? v.speedFactor : 1f;
        }

        /// <summary>Подсветить на этот кадр: рука может взять сақа.</summary>
        public void Glow()
        {
            glowUntil = Time.time + 0.1f;
            if (!glowing) SetGlow(true);
        }

        void Update()
        {
            if (glowing && Time.time > glowUntil) SetGlow(false);
        }

        void SetGlow(bool on)
        {
            glowing = on;
            if (view == null) return;
            block ??= new MaterialPropertyBlock();
            view.GetPropertyBlock(block);
            block.SetColor(EmissionId, on ? glowColor : Color.black);
            view.SetPropertyBlock(block);
        }

        void FixedUpdate()
        {
            if (!InFlight) return;
            if (transform.position.y < lostBelowY)
            {
                InFlight = false;
                return;
            }
            if (!Bone.IsResting)
            {
                restingSince = -1f;
                return;
            }
            if (restingSince < 0f) restingSince = Time.time;
            else if (Time.time - restingSince >= restDuration) InFlight = false;
        }

        /// <summary>Взять в руку. hand — рука игрока, для бота null.</summary>
        public void Hold(SaqaHand hand, Transform attach)
        {
            Holder = hand;
            IsHeld = true;
            InFlight = false;
            if (glowing) SetGlow(false);
            var body = Bone.Body;
            body.isKinematic = true;
            body.detectCollisions = false;
            // Интерполяция у кинематического тела, которое двигает родитель, даёт отставание от руки.
            body.interpolation = RigidbodyInterpolation.None;
            transform.SetParent(attach, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            var hand = Holder;
            Holder = null;
            IsHeld = false;
            transform.SetParent(null, true);
            var body = Bone.Body;
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
            body.WakeUp();
            InFlight = true;
            restingSince = -1f;
            Thrown?.Invoke(hand, velocity);
        }
    }
}
