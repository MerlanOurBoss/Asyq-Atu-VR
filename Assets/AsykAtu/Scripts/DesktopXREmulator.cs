using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace AsykAtu
{
    /// <summary>
    /// Эмулятор VR для теста на компьютере. Мышь — взгляд и прицел, ЛКМ — взять сақа и замахнуться,
    /// отпустить ЛКМ — бросок. Бросок идёт через настоящую руку: эмулятор проводит контроллер вперёд
    /// с нужной скоростью и отпускает Grip, а скорость считает SaqaHand, как в шлеме.
    /// В режиме Auto включается, только когда шлем не подключён. Вешается на XR Origin.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DesktopXREmulator : MonoBehaviour
    {
        public enum Mode { Auto, Always, Never }
        enum ThrowState { Idle, Reaching, Returning, Charging, Swinging }

        [Tooltip("Auto — только когда шлем не подключён.")]
        public Mode mode = Mode.Auto;

        [Header("Ссылки")]
        [Tooltip("Main Camera внутри XR Origin.")]
        public Transform head;
        [Tooltip("Контроллер, которым бросаем.")]
        public Transform hand;
        public SaqaHand saqaHand;
        [Tooltip("Другой контроллер: убираем вниз, чтобы не мешал.")]
        public Transform otherHand;

        [Header("Тело")]
        public float standingHeadHeight = 1.6f;
        public float crouchHeadHeight = 0.9f;
        public float walkSpeed = 1.5f;
        public float lookSensitivity = 0.12f;
        [Tooltip("Наклон взгляда вниз при старте, град.")]
        public float startPitch = 15f;
        public float desktopFieldOfView = 80f;

        [Header("Бросок")]
        [Tooltip("Где рука относительно головы, м.")]
        public Vector3 handRest = new Vector3(0.18f, -0.3f, 0.35f);
        public float minSpeed = 2f;
        public float maxSpeed = 12f;
        [Tooltip("За сколько секунд замах набирает полную силу.")]
        public float chargeTime = 1.2f;
        [Tooltip("Насколько рука уходит назад при полном замахе, м.")]
        public float windUp = 0.3f;
        [Tooltip("Путь руки вперёд от точки покоя до отпускания, м.")]
        public float followThrough = 0.2f;
        [Tooltip("Угол броска над линией взгляда, град. Меняется колесом.")]
        [Range(-20f, 45f)] public float lift = 8f;
        [Tooltip("Скорость руки при движении к сақа и обратно, м/с. Как у живой руки.")]
        public float handSpeed = 2.5f;

        public bool IsActive { get; private set; }
        /// <summary>Набранная сила замаха (0..1).</summary>
        public float Charge { get; private set; }

        const string HelpText =
            "Мышь — смотреть и целиться\n" +
            "ЛКМ — дотянуться до сақа у ног и взять, держать — замах, отпустить — бросок\n" +
            "Колесо — угол броска\n" +
            "WASD — шаг,  C — присесть\n" +
            "Стрелки — стик,  Enter или 1 — кнопка A,  2 — кнопка B,  Tab — клик стика\n" +
            "Esc — отпустить мышь,  H — скрыть подсказку";

        ThrowState state;
        float yaw, pitch, chargeStart, swingSpeed, swingLength, swingTravelled, savedFov;
        bool crouched, showHelp = true, ignoreClick;
        Vector3 swingStart, swingDir;
        Quaternion swingRotation;
        TrackedPoseDriver[] drivers;
        Camera headCamera;
        GUIStyle labelStyle;

        void Awake()
        {
            drivers = GetComponentsInChildren<TrackedPoseDriver>(true);
            if (head != null) headCamera = head.GetComponent<Camera>();
        }

        void OnDisable()
        {
            if (IsActive) SetActive(false);
        }

        void Update()
        {
            bool shouldBeActive = mode == Mode.Always || (mode == Mode.Auto && !XRSettings.isDeviceActive);
            if (shouldBeActive != IsActive) SetActive(shouldBeActive);
            if (!IsActive) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.deltaTime;

            if (kb != null && mouse != null)
            {
                // Мышь захватывается кликом в окне игры. Этот клик не считается хватом.
                if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    ignoreClick = true;
                }
                if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
                bool captured = Cursor.lockState == CursorLockMode.Locked;

                if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
                if (kb.cKey.wasPressedThisFrame) crouched = !crouched;

                if (captured)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * lookSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -80f, 80f);
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheel != 0f) lift = Mathf.Clamp(lift + Mathf.Sign(wheel) * 2f, -20f, 45f);

                    if (mouse.leftButton.wasPressedThisFrame && !ignoreClick) Grip();
                    if (mouse.leftButton.wasReleasedThisFrame)
                    {
                        if (ignoreClick) ignoreClick = false;
                        else LetGo();
                    }
                }

                var step = new Vector3(Axis(kb.dKey, kb.aKey), 0f, Axis(kb.wKey, kb.sKey));
                if (step != Vector3.zero)
                    transform.position += Quaternion.Euler(0f, yaw, 0f) * step.normalized * (walkSpeed * dt);
            }

            head.position = transform.position + Vector3.up * (crouched ? crouchHeadHeight : standingHeadHeight);
            head.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (otherHand != null) otherHand.position = transform.position + Vector3.down;

            UpdateHand(dt);
        }

        /// <summary>Нажать «хват»: рука тянется к сақа, сжимается рядом с ним и несёт обратно.</summary>
        public void Grip()
        {
            if (state != ThrowState.Idle) return;
            var saqa = saqaHand.saqa;
            if (saqa != null && saqa.CanTake) state = ThrowState.Reaching;
        }

        /// <summary>Отпустить «хват». Если сақа в руке — рука идёт вперёд и бросает.</summary>
        public void LetGo()
        {
            bool holding = saqaHand.IsHolding && (state == ThrowState.Charging || state == ThrowState.Returning);
            if (!holding)
            {
                if (state == ThrowState.Reaching || state == ThrowState.Idle)
                {
                    saqaHand.squeezeOverride = 0f;
                    state = ThrowState.Idle;
                }
                return;
            }
            if (state == ThrowState.Returning) Charge = 0f;
            swingSpeed = Mathf.Lerp(minSpeed, maxSpeed, Charge);
            swingStart = hand.position;
            swingDir = AimDirection();
            swingRotation = hand.rotation;
            swingLength = windUp * Charge + followThrough;
            swingTravelled = 0f;
            state = ThrowState.Swinging;
        }

        void UpdateHand(float dt)
        {
            Vector3 rest = head.TransformPoint(handRest);
            switch (state)
            {
                case ThrowState.Idle:
                    MoveHand(rest, handSpeed, dt);
                    break;

                case ThrowState.Reaching:
                    var saqa = saqaHand.saqa;
                    if (saqa == null || (!saqa.CanTake && !saqaHand.IsHolding))
                    {
                        state = ThrowState.Idle;
                        break;
                    }
                    MoveHand(saqa.transform.position + Vector3.up * 0.03f, handSpeed, dt);
                    // Ладонь рядом — сжимаем; SaqaHand возьмёт сақа в LateUpdate этого кадра.
                    if (saqaHand.DistanceToSaqa() <= saqaHand.grabRadius * 0.8f) saqaHand.squeezeOverride = 1f;
                    if (saqaHand.IsHolding) state = ThrowState.Returning;
                    break;

                case ThrowState.Returning:
                    MoveHand(rest, handSpeed, dt);
                    if (!saqaHand.IsHolding) state = ThrowState.Idle;
                    else if ((hand.position - rest).sqrMagnitude < 0.0001f)
                    {
                        state = ThrowState.Charging;
                        chargeStart = Time.time;
                    }
                    break;

                case ThrowState.Charging:
                    Charge = Mathf.Clamp01((Time.time - chargeStart) / chargeTime);
                    MoveHand(rest - AimDirection() * (windUp * Charge), handSpeed, dt);
                    if (!saqaHand.IsHolding) state = ThrowState.Idle;
                    break;

                case ThrowState.Swinging:
                    // Постоянная скорость: каждый кадр рука проходит swingSpeed·dt.
                    // На подвисании (dt большой) не даём руке улететь на метры вперёд.
                    swingTravelled += swingSpeed * Mathf.Min(dt, 0.05f);
                    hand.SetPositionAndRotation(swingStart + swingDir * swingTravelled, swingRotation);
                    if (swingTravelled >= swingLength)
                    {
                        saqaHand.squeezeOverride = 0f; // SaqaHand отпустит в LateUpdate этого же кадра
                        Charge = 0f;
                        state = ThrowState.Idle;
                    }
                    break;
            }
        }

        Vector3 AimDirection() => Quaternion.AngleAxis(-lift, head.right) * head.forward;

        /// <summary>Рука идёт к точке с человеческой скоростью, а не прыгает: иначе прыжок попал бы в скорость броска.</summary>
        void MoveHand(Vector3 target, float speed, float dt) =>
            hand.SetPositionAndRotation(Vector3.MoveTowards(hand.position, target, speed * dt), head.rotation);

        void SetActive(bool active)
        {
            IsActive = active;
            // TrackedPoseDriver без шлема ничего не пишет, но выключаем, чтобы не спорить со шлемом.
            foreach (var driver in drivers)
                if (driver != null) driver.enabled = !active;

            if (active)
            {
                if (headCamera != null) { savedFov = headCamera.fieldOfView; headCamera.fieldOfView = desktopFieldOfView; }
                yaw = transform.eulerAngles.y;
                pitch = startPitch;
                state = ThrowState.Idle;
                if (saqaHand != null) saqaHand.squeezeOverride = 0f;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                if (headCamera != null && savedFov > 0f) headCamera.fieldOfView = savedFov;
                if (saqaHand != null) saqaHand.squeezeOverride = -1f;
            }
        }

        static float Axis(UnityEngine.InputSystem.Controls.KeyControl positive, UnityEngine.InputSystem.Controls.KeyControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);

        void OnGUI()
        {
            if (!IsActive) return;
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };

            GUILayout.BeginArea(new Rect(10f, 10f, 480f, showHelp ? 280f : 110f), GUI.skin.box);
            GUILayout.Label("Эмулятор VR (H — подсказка)", labelStyle);
            GUILayout.Label($"Угол броска: {lift:0}°    Сила: {Mathf.Lerp(minSpeed, maxSpeed, Charge):0.0} м/с", labelStyle);
            Rect bar = GUILayoutUtility.GetRect(380f, 12f);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(Color.green, Color.red, Charge);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Charge, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (Cursor.lockState != CursorLockMode.Locked) GUILayout.Label("Кликните в окне игры, чтобы управлять мышью", labelStyle);
            if (showHelp)
            {
                GUILayout.Space(6f);
                GUILayout.Label(HelpText, labelStyle);
            }
            GUILayout.EndArea();
        }
    }
}
