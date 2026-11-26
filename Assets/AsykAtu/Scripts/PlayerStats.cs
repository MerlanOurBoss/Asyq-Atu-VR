using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Статистика игрока между запусками (PlayerPrefs): точность бросков,
    /// рекорд тренировки (меньше всего бросков на партию) для каждого варианта правил,
    /// победы и поражения против бота по уровням. Считаются только броски людей, не бота.
    /// </summary>
    public static class PlayerStats
    {
        /// <summary>Префикс ключей. Тесты подменяют его, чтобы не трогать настоящую статистику.</summary>
        public static string Prefix = "AsykAtu.Stats.";

        public static int Throws => PlayerPrefs.GetInt(Prefix + "Throws", 0);
        /// <summary>Броски, после которых выбит хотя бы один асык.</summary>
        public static int Hits => PlayerPrefs.GetInt(Prefix + "Hits", 0);
        public static int Knocked => PlayerPrefs.GetInt(Prefix + "Knocked", 0);
        /// <summary>Доля удачных бросков, 0..100.</summary>
        public static int AccuracyPercent => Throws > 0 ? Mathf.RoundToInt(100f * Hits / Throws) : 0;

        public static void RecordThrow(int knocked)
        {
            Add("Throws", 1);
            if (knocked > 0) Add("Hits", 1);
            Add("Knocked", knocked);
            PlayerPrefs.Save();
        }

        /// <summary>Лучшая тренировка: за сколько бросков выбита партия. 0 — ещё не было.</summary>
        public static int BestTraining(string rules) => PlayerPrefs.GetInt(Prefix + "Best." + rules, 0);

        /// <summary>Записать партию тренировки. true — новый рекорд.</summary>
        public static bool RecordTrainingGame(string rules, int throws)
        {
            int best = BestTraining(rules);
            if (best > 0 && throws >= best) return false;
            PlayerPrefs.SetInt(Prefix + "Best." + rules, throws);
            PlayerPrefs.Save();
            return true;
        }

        public static int BotWins(int level) => PlayerPrefs.GetInt(Prefix + "BotWins." + level, 0);
        public static int BotLosses(int level) => PlayerPrefs.GetInt(Prefix + "BotLosses." + level, 0);

        public static void RecordBotMatch(int level, bool playerWon)
        {
            Add((playerWon ? "BotWins." : "BotLosses.") + level, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Стереть всю статистику (для тестов и кнопки «сбросить»).</summary>
        public static void Clear(int levels, params string[] rulesNames)
        {
            foreach (var key in new[] { "Throws", "Hits", "Knocked" }) PlayerPrefs.DeleteKey(Prefix + key);
            foreach (var rules in rulesNames) PlayerPrefs.DeleteKey(Prefix + "Best." + rules);
            for (int i = 0; i < levels; i++)
            {
                PlayerPrefs.DeleteKey(Prefix + "BotWins." + i);
                PlayerPrefs.DeleteKey(Prefix + "BotLosses." + i);
            }
        }

        static void Add(string key, int amount) => PlayerPrefs.SetInt(Prefix + key, PlayerPrefs.GetInt(Prefix + key, 0) + amount);
    }
}
