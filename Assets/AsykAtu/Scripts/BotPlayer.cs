using System;
using System.Collections;
using UnityEngine;

namespace AsykAtu
{
    /// <summary>Уровень бота: насколько он ошибается в направлении и силе броска.</summary>
    [Serializable]
    public struct BotLevel
    {
        public string name;
        [Tooltip("Разброс направления (стандартное отклонение), град.")]
        public float yawSpread;
        [Tooltip("Разброс силы броска (стандартное отклонение), доля скорости.")]
        public float speedSpread;

        public static readonly BotLevel Perfect = new BotLevel { name = "Без ошибок" };
    }

    /// <summary>
    /// Бот у линии броска. В свой ход берёт сақа, замахивается и бросает в выбранный асык:
    /// скорость считает по баллистике, а ошибается по своему уровню. Шлем ему не нужен.
    /// Рука бота (hand) двигается в его локальных координатах, сақа лежит в точке attach.
    /// </summary>
    public class BotPlayer : MonoBehaviour
    {
        public Saqa saqa;
        [Tooltip("Рука бота: её двигаем при замахе.")]
        public Transform hand;
        [Tooltip("Точка в руке, где лежит сақа. Пусто — сама рука.")]
        public Transform attach;

        [Header("Уровни")]
        [Tooltip("Проверены на моделях костей с 3 м (по 16 бросков на уровень): лёгкий попал в 50 % бросков, средний в 50 %, сильный в 87 %. " +
                 "Выборка маленькая (±12 %), лёгкий и средний стоит развести сильнее.")]
        public BotLevel[] levels =
        {
            new BotLevel { name = "Лёгкий", yawSpread = 2.2f, speedSpread = 0.05f },
            new BotLevel { name = "Средний", yawSpread = 1.2f, speedSpread = 0.025f },
            new BotLevel { name = "Сильный", yawSpread = 0.5f, speedSpread = 0.01f },
        };
        public int level = 1;

        [Header("Бросок")]
        [Tooltip("Угол броска к горизонту, град. Низкий бросок въезжает в ряд, а не перелетает.")]
        public float launchAngle = 0f;
        [Tooltip("Целиться в переднюю грань асыка: на столько ближе его центра, м.")]
        public float aimShort = 0.03f;
        [Tooltip("Насколько выше центра асыка целиться, м.")]
        public float aimHeight = 0.005f;

        [Header("Движение руки")]
        public float thinkTime = 0.8f;
        public float windUpTime = 0.6f;
        public float swingTime = 0.12f;
        public Vector3 handRest = new Vector3(0.25f, 0.35f, 0.1f);
        public Vector3 windUpOffset = new Vector3(0f, 0.05f, -0.3f);
        public Vector3 swingOffset = new Vector3(0f, -0.05f, 0.25f);

        public BotLevel Level => levels[Mathf.Clamp(level, 0, levels.Length - 1)];
        public bool IsBusy => routine != null;

        Coroutine routine;

        void Awake()
        {
            if (attach == null) attach = hand;
        }

        void OnDisable() => Cancel();

        /// <summary>Бросить в асык, который стоит в target, с ошибками текущего уровня.</summary>
        public void ThrowAt(Vector3 target) => ThrowAt(target, Level);

        public void ThrowAt(Vector3 target, BotLevel botLevel)
        {
            Cancel();
            routine = StartCoroutine(Turn(false, target, botLevel));
        }

        /// <summary>Подбросить сақа на очерёдность.</summary>
        public void Toss()
        {
            Cancel();
            routine = StartCoroutine(Turn(true, Vector3.zero, Level));
        }

        /// <summary>Прервать ход (выход в меню). Сақа, если в руке, просто падает.</summary>
        public void Cancel()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (saqa != null && saqa.IsHeld && saqa.Holder == null) saqa.Release(Vector3.zero, Vector3.zero);
            if (hand != null) hand.localPosition = handRest;
        }

        IEnumerator Turn(bool toss, Vector3 target, BotLevel botLevel)
        {
            hand.localPosition = handRest;
            yield return new WaitForSeconds(thinkTime);
            saqa.Hold(null, attach);

            Vector3 back = handRest + (toss ? new Vector3(0f, -0.15f, 0f) : windUpOffset);
            Vector3 front = handRest + (toss ? new Vector3(0f, 0.25f, 0.05f) : swingOffset);
            yield return MoveHand(handRest, back, windUpTime);
            yield return MoveHand(back, front, swingTime);

            Vector3 velocity, spin;
            if (toss)
            {
                // Разная сила и закрутка: иначе кость каждый раз делает одинаковое число оборотов.
                velocity = Vector3.up * UnityEngine.Random.Range(2f, 3f) + transform.forward * 0.3f;
                spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(8f, 18f);
            }
            else
            {
                velocity = AimVelocity(attach.position, target, botLevel);
                spin = UnityEngine.Random.insideUnitSphere * 3f;
            }
            saqa.Release(velocity, spin);
            routine = null;

            yield return MoveHand(front, handRest, 0.4f);
        }

        /// <summary>Скорость броска из from в асык target: точная по баллистике плюс ошибка уровня.</summary>
        public Vector3 AimVelocity(Vector3 from, Vector3 target, BotLevel botLevel)
        {
            Vector3 toTarget = target - from;
            toTarget.y = 0f;
            Vector3 aim = target - toTarget.normalized * aimShort + Vector3.up * aimHeight;
            if (!Ballistics.LaunchVelocity(from, aim, launchAngle, -Physics.gravity.y, out Vector3 velocity))
                Ballistics.LaunchVelocity(from, aim, 20f, -Physics.gravity.y, out velocity); // цель выше руки — навесом

            float yawError = Gaussian() * botLevel.yawSpread;
            float speedError = 1f + Gaussian() * botLevel.speedSpread;
            return Quaternion.AngleAxis(yawError, Vector3.up) * velocity * speedError;
        }

        IEnumerator MoveHand(Vector3 from, Vector3 to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                hand.localPosition = Vector3.Lerp(from, to, t / duration);
                yield return null;
            }
            hand.localPosition = to;
        }

        /// <summary>Нормальное распределение со средним 0 и отклонением 1 (Бокс — Мюллер).</summary>
        static float Gaussian()
        {
            float u1 = 1f - UnityEngine.Random.value;
            float u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
