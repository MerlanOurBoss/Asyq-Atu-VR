using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AsykAtu
{
    /// <summary>
    /// Матч ордаға ату на одном шлеме: меню → бросок сақа на очерёдность → кон → ходы по очереди.
    /// Режимы: тренировка (один игрок, без очерёдности), против бота, двое с передачей шлема.
    /// После каждого броска ждёт, пока всё остановится: асыки за ордой засчитываются бросавшему,
    /// сдвинутые возвращаются на места. Кон выбит, а победителя нет — ставится новый.
    /// Кнопки: стик — выбор в меню, A / X — подтвердить, B / Y — язык (в меню) или дважды — выйти в меню,
    /// клик стика — сменить правила. Все тексты — через Loc (қазақша, русский, English).
    /// На клавиатуре: стрелки, Enter или 1, 2, Tab.
    /// </summary>
    public class AsykMatch : MonoBehaviour
    {
        public enum Mode { Training, VsBot, TwoPlayers }
        enum State { Menu, Handover, Ready, Flying, Pause, MatchOver }

        static readonly string[] ModeKeys = { "mode.training", "mode.vsbot", "mode.two" };

        [Header("Сцена")]
        public Orda orda;
        public Saqa saqa;
        public Asyk asykPrefab;
        [Tooltip("Бот для режима «Против бота». Стоит у линии броска слева от игрока.")]
        public BotPlayer bot;
        [Tooltip("Main Camera: по ней проверяем заступ.")]
        public Transform head;
        [Tooltip("XR Origin: при смене правил ставим игрока за линию броска.")]
        public Transform xrOrigin;
        public TMP_Text board;
        [Tooltip("Всплывающее «+2» над ордой, необязательно.")]
        public TMP_Text popup;

        [Header("Правила")]
        [Tooltip("Варианты правил, в меню переключаются по кругу. Первый — по умолчанию.")]
        public AsykRules[] rulesOptions;

        [Header("Кон")]
        [Tooltip("Шаг между асыками в ряду, м.")]
        public float spacing = 0.05f;

        [Header("Бросок")]
        [Tooltip("Медленнее этого рука просто разжалась: бросок не считается, м/с.")]
        public float minThrowSpeed = 1.5f;
        [Tooltip("Насколько голова может уйти за линию броска (наклон корпуса), м.")]
        public float lineTolerance = 0.25f;
        [Tooltip("Сколько всё должно лежать неподвижно, чтобы считать, с.")]
        public float settleTime = 0.5f;
        [Tooltip("Дольше этого не ждём, считаем как есть, с.")]
        public float maxWaitTime = 6f;

        public AsykGame Game { get; private set; }
        public AsykRules Rules => rulesOptions[rulesIndex];
        public Mode CurrentMode { get; private set; }
        public int PlayerCount => CurrentMode == Mode.Training ? 1 : 2;
        public IReadOnlyList<Asyk> Kon => kon;
        public int KonCount => kon.Count;
        public bool IsTossing => tossing;
        public string Status => status;
        public string StateName => state.ToString();

        /// <summary>Бросок засчитан: кто бросал, сколько выбил, был ли заступ.</summary>
        public event Action<int, int, bool> ThrowJudged;

        const float PopupDuration = 1.5f;
        const float StickThreshold = 0.6f;

        readonly List<Asyk> kon = new List<Asyk>();
        readonly List<Pose> homes = new List<Pose>();
        readonly List<AsykSide> tosses = new List<AsykSide>();
        State state;
        Mode menuMode = Mode.VsBot;
        int rulesIndex, activePlayer = -1, throwsInGame, stickX, stickY;
        bool tossing, throwFoul;
        float settleStart, restingSince, pauseUntil, popupStart = -10f, exitArmedUntil;
        string status = "", tossLog = "";
        Action afterPause;
        SaqaHand lastHand;
        InputAction primary, secondary, cycleRules, navigate;

        void OnEnable()
        {
            primary = Button("Primary", "{PrimaryButton}", "<Keyboard>/enter", "<Keyboard>/1");
            secondary = Button("Secondary", "{SecondaryButton}", "<Keyboard>/2");
            cycleRules = Button("Cycle Rules", "{Primary2DAxisClick}", "<Keyboard>/tab");
            navigate = new InputAction("Navigate", InputActionType.Value, expectedControlType: "Vector2");
            navigate.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            navigate.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            navigate.Enable();
            saqa.Thrown += OnThrown;
        }

        void OnDisable()
        {
            primary?.Dispose();
            secondary?.Dispose();
            cycleRules?.Dispose();
            navigate?.Dispose();
            primary = secondary = cycleRules = navigate = null;
            saqa.Thrown -= OnThrown;
        }

        static InputAction Button(string name, string usage, params string[] keys)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding($"<XRController>{{LeftHand}}/{usage}");
            action.AddBinding($"<XRController>{{RightHand}}/{usage}");
            foreach (var key in keys) action.AddBinding(key);
            action.Enable();
            return action;
        }

        void Start() => ShowMenu();

        void Update()
        {
            if (popup != null && popup.gameObject.activeSelf) AnimatePopup();

            if (primary.WasPressedThisFrame()) PressPrimary();
            else if (secondary.WasPressedThisFrame()) PressSecondary();
            else if (cycleRules.WasPressedThisFrame()) PressCycleRules();
            ReadStick();

            if (state == State.Flying) WaitForRest();
            else if (state == State.Pause && Time.time >= pauseUntil)
            {
                var next = afterPause;
                afterPause = null;
                next?.Invoke();
            }
        }

        // ── Кнопки ───────────────────────────────────────────────────

        /// <summary>Стик как кнопки: срабатывает, когда отклонён дальше порога.</summary>
        void ReadStick()
        {
            Vector2 stick = navigate.ReadValue<Vector2>();
            int x = stick.x > StickThreshold ? 1 : stick.x < -StickThreshold ? -1 : 0;
            int y = stick.y > StickThreshold ? 1 : stick.y < -StickThreshold ? -1 : 0;
            if (x != 0 && x != stickX) Navigate(x, 0);
            if (y != 0 && y != stickY) Navigate(0, y);
            stickX = x;
            stickY = y;
        }

        /// <summary>Стик в меню: вверх/вниз — режим, влево/вправо — сложность бота.</summary>
        public void Navigate(int dx, int dy)
        {
            if (state != State.Menu) return;
            if (dy != 0) menuMode = (Mode)(((int)menuMode - dy + ModeKeys.Length) % ModeKeys.Length);
            if (dx != 0 && menuMode == Mode.VsBot && bot != null)
                bot.level = Mathf.Clamp(bot.level + dx, 0, bot.levels.Length - 1);
            Refresh();
        }

        /// <summary>A / X: в меню — начать выбранный режим, при передаче шлема — начать ход, после матча — в меню.</summary>
        public void PressPrimary()
        {
            switch (state)
            {
                case State.Menu:
                    StartMatch(menuMode);
                    break;
                case State.Handover:
                    activePlayer = tossing ? tosses.Count : Game.Current;
                    SetReady(status);
                    break;
                case State.MatchOver:
                    ShowMenu();
                    break;
            }
        }

        /// <summary>B / Y: в меню — сменить язык, в матче — дважды подряд, чтобы выйти в меню.</summary>
        public void PressSecondary()
        {
            if (state == State.Menu)
            {
                Loc.Next();
                Refresh();
                return;
            }
            if (state == State.MatchOver) return;
            // Двойное нажатие, чтобы не потерять матч случайно.
            if (Time.time < exitArmedUntil)
            {
                ShowMenu();
                return;
            }
            exitArmedUntil = Time.time + 2f;
            ShowPopup(Loc.Get("exit.confirm"));
        }

        /// <summary>Клик стика: в меню — следующий вариант правил.</summary>
        public void PressCycleRules()
        {
            if (state != State.Menu) return;
            rulesIndex = (rulesIndex + 1) % rulesOptions.Length;
            ApplyRules();
            Refresh();
        }

        // ── Ход матча ────────────────────────────────────────────────

        void ShowMenu()
        {
            state = State.Menu;
            if (bot != null)
            {
                bot.Cancel();
                bot.gameObject.SetActive(false);
            }
            saqa.Locked = true;
            Game = null;
            activePlayer = -1;
            ClearKon();
            ApplyRules();
            Refresh();
        }

        public void StartMatch(Mode mode)
        {
            CurrentMode = mode;
            if (bot != null) bot.gameObject.SetActive(mode == Mode.VsBot);
            Game = new AsykGame(Rules, PlayerCount);
            activePlayer = -1;
            StartNextGame();
        }

        /// <summary>Игрок 2 в режиме «Против бота» — бот.</summary>
        bool IsBot(int player) => CurrentMode == Mode.VsBot && player == 1;

        void StartNextGame()
        {
            ClearKon();
            ResetSaqa();
            if (PlayerCount == 1)
            {
                BeginGame(0);
                return;
            }
            tossing = true;
            tosses.Clear();
            tossLog = "";
            BeginTurn(0, Loc.Get("toss.prompt"));
        }

        void BeginGame(int firstPlayer)
        {
            tossing = false;
            Game.StartGame(firstPlayer);
            throwsInGame = 0;
            BuildKon();
            BeginTurn(firstPlayer, Loc.F("game.start", Game.GameNumber));
        }

        /// <summary>Ход игрока. Если шлем у другого — сначала передать шлем и нажать A / X. Ход бота — бот бросает сам.</summary>
        void BeginTurn(int player, string message)
        {
            if (IsBot(player))
            {
                // Состояние Ready, чтобы бросок бота засчитался, но игроку сақа не взять.
                state = State.Ready;
                saqa.Locked = true;
                status = $"{message}\n{Loc.F("turn", Name(player))}";
                Refresh();
                if (tossing) bot.Toss();
                else bot.ThrowAt(ChooseTarget());
                return;
            }
            if (CurrentMode == Mode.TwoPlayers && player != activePlayer)
            {
                state = State.Handover;
                saqa.Locked = true;
                status = $"{Loc.F("turn", Name(player))}\n{message}\n<size=70%>{Loc.Get("handover")}</size>";
                Refresh();
                return;
            }
            SetReady(message);
        }

        void SetReady(string message)
        {
            state = State.Ready;
            saqa.Locked = false;
            status = message;
            Refresh();
        }

        void Pause(float seconds, Action next)
        {
            state = State.Pause;
            saqa.Locked = true;
            pauseUntil = Time.time + seconds;
            afterPause = next;
            Refresh();
        }

        void OnThrown(SaqaHand hand, Vector3 velocity)
        {
            if (state != State.Ready) return;
            lastHand = hand;
            if (!tossing && velocity.magnitude < minThrowSpeed)
            {
                status = Loc.Get("weak");
                Refresh();
                return;
            }
            throwFoul = !tossing && Rules.lineFoul && !IsBot(Game.Current) && HeadPastLine();
            if (!tossing) throwsInGame++;
            state = State.Flying;
            saqa.Locked = true;
            settleStart = Time.time;
            restingSince = -1f;
            status = "...";
            Refresh();
        }

        void WaitForRest()
        {
            bool resting = !saqa.InFlight && saqa.Bone.IsResting && kon.TrueForAll(a => a.IsResting);
            if (!resting) restingSince = -1f;
            else if (restingSince < 0f) restingSince = Time.time;

            if ((resting && Time.time - restingSince >= settleTime) || Time.time - settleStart > maxWaitTime)
            {
                if (tossing) JudgeToss();
                else JudgeThrow();
            }
        }

        void JudgeToss()
        {
            int player = tosses.Count;
            AsykSide side = saqa.Bone.UpSide;
            if (side == AsykSide.None)
            {
                BeginTurn(player, tossLog + Loc.Get("toss.edge"));
                return;
            }

            tosses.Add(side);
            tossLog += $"{Name(player)}: {Asyk.SideName(side)}\n";
            if (tosses.Count < PlayerCount)
            {
                BeginTurn(tosses.Count, tossLog + Loc.Get("toss.prompt"));
                return;
            }

            int first = AsykGame.FirstByToss(Rules, tosses);
            if (first < 0)
            {
                tosses.Clear();
                string log = tossLog;
                tossLog = "";
                BeginTurn(0, log + Loc.Get("toss.tie"));
                return;
            }

            status = tossLog + Loc.F("toss.first", Name(first));
            ResetSaqa();
            Pause(2.5f, () => BeginGame(first));
        }

        void JudgeThrow()
        {
            int thrower = Game.Current;
            int knocked = 0;
            if (throwFoul) ReturnKon();
            else knocked = RemoveKnocked();
            bool saqaInOrda = !orda.IsOutside(saqa.transform.position);

            TurnResult result = Game.Throw(knocked, saqaInOrda, throwFoul);
            ThrowJudged?.Invoke(thrower, knocked, throwFoul);
            if (knocked > 0)
            {
                ShowPopup($"+{knocked}");
                if (lastHand != null) lastHand.Pulse(0.8f, 0.15f);
            }

            string summary = throwFoul ? Loc.F("foul", Name(thrower))
                : knocked > 0 ? Loc.F("hit", Name(thrower), knocked)
                : Loc.F("miss", Name(thrower));

            switch (result)
            {
                case TurnResult.MatchWon:
                    state = State.MatchOver;
                    saqa.Locked = true;
                    string verdict = PlayerCount > 1 ? Loc.F("match.won", Name(thrower)) : Loc.Get("training.over");
                    status = $"{summary}\n{verdict}\n<size=70%>{Loc.Get("to.menu")}</size>";
                    Refresh();
                    return;

                case TurnResult.GameWon:
                    status = summary + "\n" + (PlayerCount > 1
                        ? Loc.F("game.won", Name(thrower), Game.GameNumber)
                        : Loc.F("training.game", Rules.winTarget, throwsInGame));
                    Pause(3f, StartNextGame);
                    return;

                default:
                    if (kon.Count == 0)
                    {
                        BuildKon();
                        summary += "\n" + Loc.Get("kon.new");
                    }
                    if (result == TurnResult.ThrowAgain) summary += "\n" + Loc.Get("again");
                    BeginTurn(Game.Current, summary);
                    return;
            }
        }

        bool HeadPastLine()
        {
            if (head == null) return false;
            float fromCenter = Vector3.Dot(head.position - orda.Center, -orda.transform.forward);
            return fromCenter < Rules.throwDistance - lineTolerance;
        }

        // ── Поле ─────────────────────────────────────────────────────

        void ApplyRules()
        {
            orda.radius = Rules.ordaRadius;
            orda.throwDistance = Rules.throwDistance;
            orda.Redraw();
            if (xrOrigin != null)
            {
                // Игрок стоит в 30 см за линией броска лицом к орде.
                Vector3 p = orda.Center - orda.transform.forward * (Rules.throwDistance + 0.3f);
                p.y = xrOrigin.position.y;
                xrOrigin.SetPositionAndRotation(p, orda.transform.rotation);
            }
            if (bot != null)
            {
                // Бот — у самой линии, в 80 см слева от игрока: виден краем глаза.
                Vector3 p = orda.Center - orda.transform.forward * (Rules.throwDistance + 0.15f) - orda.transform.right * 0.8f;
                bot.transform.SetPositionAndRotation(p, orda.transform.rotation);
            }
            ResetSaqa();
        }

        /// <summary>Куда бросает бот: случайный асык из оставшихся в коне.</summary>
        Vector3 ChooseTarget() =>
            kon.Count > 0 ? kon[UnityEngine.Random.Range(0, kon.Count)].transform.position : orda.Center;

        /// <summary>Сақа на землю рядом с линией броска, чтобы не лежал в коне.</summary>
        void ResetSaqa()
        {
            if (saqa.IsHeld) return;
            Vector3 p = orda.Center - orda.transform.forward * (Rules.throwDistance - 0.2f) + orda.transform.right * 0.4f;
            saqa.Bone.PlaceAt(p + Vector3.up * HalfHeight(saqa.Bone), orda.transform.rotation);
        }

        /// <summary>Кон тігу: каждый ставит свои асыки в ряд. В тренировке кон как на двоих.</summary>
        void BuildKon()
        {
            ClearKon();
            ResetSaqa();
            int count = Rules.asyksPerPlayer * Mathf.Max(2, PlayerCount);
            Quaternion rotation = orda.transform.rotation;
            for (int i = 0; i < count; i++)
            {
                Asyk a = Instantiate(asykPrefab, orda.KonPoint(i, count, spacing), rotation);
                a.name = $"Asyk {i + 1}";
                Vector3 p = orda.KonPoint(i, count, spacing) + Vector3.up * HalfHeight(a);
                a.PlaceAt(p, rotation);
                kon.Add(a);
                homes.Add(new Pose(p, rotation));
            }
        }

        void ClearKon()
        {
            foreach (var a in kon)
                if (a != null) Destroy(a.gameObject);
            kon.Clear();
            homes.Clear();
        }

        /// <summary>Убрать асыки за ордой (выбиты), остальные вернуть на места. Возвращает, сколько выбито.</summary>
        int RemoveKnocked()
        {
            int knocked = 0;
            for (int i = kon.Count - 1; i >= 0; i--)
            {
                Vector3 p = kon[i].transform.position;
                if (orda.IsOutside(p) || p.y < orda.Center.y - 1f)
                {
                    Destroy(kon[i].gameObject);
                    kon.RemoveAt(i);
                    homes.RemoveAt(i);
                    knocked++;
                }
                else
                {
                    kon[i].PlaceAt(homes[i].position, homes[i].rotation);
                }
            }
            return knocked;
        }

        /// <summary>Заступ: все асыки, даже выбитые, возвращаются на места.</summary>
        void ReturnKon()
        {
            for (int i = 0; i < kon.Count; i++)
                kon[i].PlaceAt(homes[i].position, homes[i].rotation);
        }

        /// <summary>От центра кости до её низа: столько поднять над землёй, чтобы стояла ровно.</summary>
        static float HalfHeight(Asyk a)
        {
            var bounds = a.GetComponentInChildren<Renderer>().bounds;
            return a.transform.position.y - bounds.min.y + 0.0005f;
        }

        // ── Табло ────────────────────────────────────────────────────

        string Name(int player) => IsBot(player) ? Loc.Get("bot") : Loc.F("player", player + 1);

        void ShowPopup(string text)
        {
            if (popup == null) return;
            popup.text = text;
            popupStart = Time.time;
            popup.gameObject.SetActive(true);
            AnimatePopup();
        }

        void AnimatePopup()
        {
            float t = (Time.time - popupStart) / PopupDuration;
            if (t >= 1f)
            {
                popup.gameObject.SetActive(false);
                return;
            }
            popup.transform.position = orda.Center + Vector3.up * (0.35f + 0.3f * t);
            popup.alpha = 1f - t * t;
        }

        void Refresh()
        {
            if (board == null) return;
            string title = Loc.GetOr("rules." + Rules.name, Rules.title);
            string rules = Loc.F("rules.line", title, Rules.throwDistance.ToString("0.#"), Rules.winTarget);

            if (state == State.Menu)
            {
                var menu = new System.Text.StringBuilder(Loc.Get("menu.title")).Append("\n\n");
                for (int i = 0; i < ModeKeys.Length; i++)
                {
                    bool selected = (int)menuMode == i;
                    if (selected) menu.Append("<color=#FFD54A>> ");
                    menu.Append(Loc.Get(ModeKeys[i]));
                    if ((Mode)i == Mode.VsBot && bot != null) menu.Append(": ").Append(Loc.GetOr("bot.level" + bot.level, bot.Level.name));
                    if (selected) menu.Append("</color>");
                    menu.Append('\n');
                }
                menu.Append("\n<size=70%>").Append(Loc.Get("menu.help")).Append('\n')
                    .Append(Loc.F("menu.rules", rules, Rules.gamesToWin)).Append('\n')
                    .Append(Loc.F("menu.language", Loc.Get("lang.name"))).Append("</size>");
                board.text = menu.ToString();
                return;
            }

            var text = new System.Text.StringBuilder();
            text.Append("<size=70%>").Append(rules);
            if (PlayerCount > 1) text.Append(Loc.F("rules.match", Rules.gamesToWin));
            text.Append("</size>\n");
            for (int i = 0; i < PlayerCount; i++)
            {
                bool current = !tossing && Game.Current == i && state != State.MatchOver;
                if (current) text.Append("<color=#FFD54A>");
                text.Append($"{Name(i)}: {Game.Knocked[i]}   ");
                text.Append(PlayerCount > 1 ? Loc.F("row.games", Game.GamesWon[i]) : Loc.F("row.throws", throwsInGame));
                if (current) text.Append("</color>");
                text.Append('\n');
            }
            text.Append('\n').Append(status);
            board.text = text.ToString();
        }
    }
}
