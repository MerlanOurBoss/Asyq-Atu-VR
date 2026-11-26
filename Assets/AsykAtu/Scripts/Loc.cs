using System;
using System.Collections.Generic;
using UnityEngine;

namespace AsykAtu
{
    public enum Language { Kazakh, Russian, English }

    /// <summary>
    /// Тексты игры на трёх языках: ключ → [қазақша, русский, English].
    /// Выбранный язык хранится в PlayerPrefs, по умолчанию казахский.
    /// Нет перевода — берётся русский, нет ключа — сам ключ (так пропуск сразу виден на табло).
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "AsykAtu.Language";

        static bool loaded;
        static Language current;

        /// <summary>Язык сменился — тексты пора перерисовать.</summary>
        public static event Action Changed;

        public static Language Current
        {
            get { Load(); return current; }
        }

        public static IReadOnlyDictionary<string, string[]> Entries => Table;

        public static void Set(Language language)
        {
            Load();
            current = language;
            PlayerPrefs.SetInt(PrefKey, (int)language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Следующий язык по кругу.</summary>
        public static void Next() => Set((Language)(((int)Current + 1) % 3));

        public static string Get(string key)
        {
            if (!Table.TryGetValue(key, out var texts)) return key;
            string text = texts[(int)Current];
            return string.IsNullOrEmpty(text) ? texts[(int)Language.Russian] : text;
        }

        /// <summary>Перевод, а если ключа нет — fallback (например, название из ассета правил).</summary>
        public static string GetOr(string key, string fallback) => Table.ContainsKey(key) ? Get(key) : fallback;

        public static string F(string key, params object[] args) => string.Format(Get(key), args);

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            current = (Language)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Language.Kazakh), 0, 2);
        }

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // Меню
            ["lang.name"] = new[] { "Қазақша", "Русский", "English" },
            ["menu.title"] = new[] { "Asyq Atu VR", "Asyq Atu VR", "Asyq Atu VR" },
            ["mode.training"] = new[] { "Жаттығу", "Тренировка", "Training" },
            ["mode.vsbot"] = new[] { "Ботқа қарсы", "Против бота", "Versus bot" },
            ["mode.two"] = new[] { "Екі ойыншы, бір шлем", "Двое на одном шлеме", "Two players, one headset" },
            ["menu.help"] = new[]
            {
                "Стик жоғары/төмен — таңдау, солға/оңға — өзгерту, A / X — бастау",
                "Стик вверх/вниз — выбор, влево/вправо — изменить, A / X — начать",
                "Stick up/down — select, left/right — change, A / X — start",
            },
            ["menu.row.mode"] = new[] { "Режим: {0}", "Режим: {0}", "Mode: {0}" },
            ["menu.row.bot"] = new[] { "Бот: {0}", "Бот: {0}", "Bot: {0}" },
            ["menu.row.saqa"] = new[] { "Сақа: {0}", "Сақа: {0}", "Saqa: {0}" },
            ["menu.row.rules"] = new[] { "Ереже: {0}", "Правила: {0}", "Rules: {0}" },
            ["menu.row.lang"] = new[] { "Тіл: {0}", "Язык: {0}", "Language: {0}" },

            // Сақа
            ["saqa.red"] = new[] { "қызыл", "красный", "red" },
            ["saqa.blue"] = new[] { "көк", "синий", "blue" },
            ["saqa.white"] = new[] { "ақ", "белый", "white" },
            ["saqa.quyma"] = new[] { "құйма (ауыр)", "құйма (тяжёлый)", "quyma (heavy)" },
            ["pickup"] = new[] { "Сақаны аяғыңыздың жанынан қолмен алыңыз", "Поднимите сақа у ног рукой", "Pick up the saqa at your feet" },

            // Статистика
            ["stats.accuracy"] = new[] { "Дәлдік: {0}% ({1} лақтыру)", "Точность: {0}% ({1} бросков)", "Accuracy: {0}% ({1} throws)" },
            ["stats.best"] = new[] { "Рекорд: {0} асық {1} лақтырыспен", "Рекорд: {0} асыков за {1} бросков", "Best: {0} asyks in {1} throws" },
            ["stats.nobest"] = new[] { "Рекорд әлі жоқ", "Рекорда пока нет", "No record yet" },
            ["stats.bot"] = new[] { "Ботпен ({0}): {1} жеңіс, {2} жеңіліс", "С ботом ({0}): побед {1}, поражений {2}", "Vs bot ({0}): {1} wins, {2} losses" },
            ["record.new"] = new[] { "Жаңа рекорд!", "Новый рекорд!", "New record!" },

            // Правила и уровни бота
            ["rules.line"] = new[] { "{0}: {1} м, {2} асыққа дейін", "{0}: {1} м, до {2} асыков", "{0}: {1} m, first to {2} asyks" },
            ["rules.match"] = new[] { ", матч {0} жеңіске дейін", ", матч до {0} побед", ", match to {0} wins" },
            ["rules.Training"] = new[] { "Жаттығу", "Тренировка", "Training" },
            ["rules.Sport"] = new[] { "Спорт", "Спорт", "Sport" },
            ["bot.level0"] = new[] { "Жеңіл", "Лёгкий", "Easy" },
            ["bot.level1"] = new[] { "Орташа", "Средний", "Medium" },
            ["bot.level2"] = new[] { "Күшті", "Сильный", "Hard" },

            // Игроки и табло
            ["player"] = new[] { "{0}-ойыншы", "Игрок {0}", "Player {0}" },
            ["bot"] = new[] { "Бот", "Бот", "Bot" },
            ["row.games"] = new[] { "жеңген ойын: {0}", "партий: {0}", "games: {0}" },
            ["row.throws"] = new[] { "лақтыру: {0}", "бросков: {0}", "throws: {0}" },

            // Очерёдность
            ["toss.prompt"] = new[] { "Кезек анықтау: сақаны жоғары лақтырыңыз", "Бросок на очерёдность: подбросьте сақа", "Toss for order: throw the saqa up" },
            ["toss.edge"] = new[] { "Сақа қырынан тұрып қалды, қайта лақтырыңыз", "Сақа встал на торец, бросайте ещё раз", "The saqa landed on its edge, toss again" },
            ["toss.tie"] = new[] { "Тең түсті, қайта лақтырыңыз", "Ничья, бросайте заново", "A tie, toss again" },
            ["toss.first"] = new[] { "Бірінші {0} лақтырады", "Первым ходит {0}", "{0} goes first" },

            // Ход
            ["game.start"] = new[] { "{0}-ойын: конға лақтырыңыз", "Партия {0}: бросайте в кон", "Game {0}: throw at the kon" },
            ["turn"] = new[] { "Кезек: {0}", "Ход: {0}", "Turn: {0}" },
            ["handover"] = new[] { "Шлемді беріп, A / X басыңыз", "Передайте шлем и нажмите A / X", "Pass the headset and press A / X" },
            ["weak"] = new[] { "Есептелмейді: қол жай ашылып кетті. Қайта лақтырыңыз", "Не засчитано: рука просто разжалась. Бросайте ещё", "Not counted: the hand just opened. Throw again" },
            ["foul"] = new[] { "{0}: сызықтан аттады, лақтыру есептелмейді", "{0}: заступ, бросок не засчитан", "{0}: line foul, throw not counted" },
            ["hit"] = new[] { "{0}: {1} асық шықты", "{0}: выбито {1}", "{0}: knocked out {1}" },
            ["miss"] = new[] { "{0}: тимеді", "{0}: мимо", "{0}: miss" },
            ["again"] = new[] { "Тағы лақтырыңыз", "Бросайте ещё", "Throw again" },
            ["kon.new"] = new[] { "Кон таусылды, жаңа кон тігіледі", "Кон выбит, ставим новый", "The kon is cleared, setting a new one" },

            // Итоги
            ["game.won"] = new[] { "{0} {1}-ойында жеңді!", "{0} выиграл партию {1}!", "{0} wins game {1}!" },
            ["match.won"] = new[] { "{0} матчта жеңді!", "{0} выиграл матч!", "{0} wins the match!" },
            ["training.game"] = new[] { "Ойын: {0} асық {1} лақтырыспен", "Партия: {0} асыков за {1} бросков", "Game: {0} asyks in {1} throws" },
            ["training.over"] = new[] { "Жаттығу аяқталды!", "Тренировка окончена!", "Training complete!" },
            ["to.menu"] = new[] { "A / X — мәзірге", "A / X — в меню", "A / X — to menu" },
            ["exit.confirm"] = new[] { "Мәзірге шығу үшін B / Y тағы басыңыз", "B / Y ещё раз — в меню", "Press B / Y again for the menu" },

            // Стороны кости
            ["side.alshy"] = new[] { "алшы", "алшы", "alshy" },
            ["side.taike"] = new[] { "тәйке", "тәйке", "taike" },
            ["side.buk"] = new[] { "бүк", "бүк", "buk" },
            ["side.shik"] = new[] { "шік", "шік", "shik" },
            ["side.none"] = new[] { "қырынан", "на торце", "on edge" },
        };
    }
}
