using System;
using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Сақа: бита, которой выбивают асыки. В руке жёстко привязан к ладони и не сталкивается
    /// ни с чем, иначе его можно было бы просто протащить через кон. Отпущенный летит со скоростью руки.
    /// </summary>
    [RequireComponent(typeof(Asyk))]
    public class Saqa : MonoBehaviour
    {
        [Tooltip("Упал ниже этой высоты — считаем потерянным, м.")]
        public float lostBelowY = -3f;
        [Tooltip("Сколько лежать неподвижно подряд, чтобы считаться остановившимся, с. " +
                 "Иначе в верхней точке отскока скорость на миг почти ноль.")]
        public float restDuration = 0.2f;

        public Asyk Bone { get; private set; }
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

        float restingSince = -1f;

        void Awake() => Bone = GetComponent<Asyk>();

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
