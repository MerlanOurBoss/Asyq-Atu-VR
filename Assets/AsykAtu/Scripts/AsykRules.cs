using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Правила ордаға ату. Разные варианты игры — разные файлы правил, без правки кода.
    /// Создать: Assets → Create → Asyk Atu → Rules.
    /// </summary>
    [CreateAssetMenu(menuName = "Asyk Atu/Rules", fileName = "Rules")]
    public class AsykRules : ScriptableObject
    {
        [Tooltip("Название для табло.")]
        public string title = "Тренировка";

        [Header("Поле")]
        [Tooltip("От центра орды до линии броска, м. По спортивным правилам 6 м.")]
        public float throwDistance = 3f;
        [Tooltip("Радиус орды, м.")]
        public float ordaRadius = 0.75f;

        [Header("Кон")]
        [Tooltip("Сколько асыков каждый игрок ставит в кон.")]
        public int asyksPerPlayer = 5;

        [Header("Победа")]
        [Tooltip("Сколько асыков выбить, чтобы выиграть партию.")]
        public int winTarget = 8;
        [Tooltip("Сколько партий выиграть, чтобы выиграть матч.")]
        public int gamesToWin = 2;

        [Header("Ход")]
        [Tooltip("Выбил хоть один асык — бросаешь ещё раз.")]
        public bool throwAgainOnHit = true;
        [Tooltip("Сақа остался в орде — ход переходит, даже если выбил.")]
        public bool saqaInOrdaEndsTurn = false;
        [Tooltip("Заступ за линию броска: бросок не засчитывается, ход переходит.")]
        public bool lineFoul = true;

        [Header("Очерёдность")]
        [Tooltip("Стороны сақа от лучшей к худшей при броске на очерёдность.")]
        public AsykSide[] sideRank = { AsykSide.Alshy, AsykSide.Taike, AsykSide.Buk, AsykSide.Shik };
    }
}
