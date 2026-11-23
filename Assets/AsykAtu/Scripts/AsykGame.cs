using System.Collections.Generic;

namespace AsykAtu
{
    public enum TurnResult { ThrowAgain, NextPlayer, Foul, GameWon, MatchWon }

    /// <summary>
    /// Счёт и очерёдность матча, без физики и сцены: кто ходит, сколько выбито, кто выиграл.
    /// Сцена (AsykMatch) сообщает результат каждого броска, а этот класс решает, что дальше.
    /// </summary>
    public class AsykGame
    {
        public AsykRules Rules { get; }
        public int PlayerCount { get; }
        /// <summary>Чей ход.</summary>
        public int Current { get; private set; }
        /// <summary>Выбито каждым в текущей партии.</summary>
        public int[] Knocked { get; }
        /// <summary>Выиграно партий каждым.</summary>
        public int[] GamesWon { get; }
        /// <summary>Номер партии, с 1.</summary>
        public int GameNumber { get; private set; }
        /// <summary>Победитель матча, −1 — матч идёт.</summary>
        public int MatchWinner { get; private set; } = -1;

        public AsykGame(AsykRules rules, int playerCount)
        {
            Rules = rules;
            PlayerCount = playerCount;
            Knocked = new int[playerCount];
            GamesWon = new int[playerCount];
        }

        /// <summary>Новая партия: счёт партии с нуля, первым ходит firstPlayer.</summary>
        public void StartGame(int firstPlayer)
        {
            for (int i = 0; i < PlayerCount; i++) Knocked[i] = 0;
            Current = firstPlayer;
            GameNumber++;
        }

        /// <summary>Итог броска текущего игрока.</summary>
        public TurnResult Throw(int knocked, bool saqaInOrda, bool foul)
        {
            if (foul)
            {
                Current = (Current + 1) % PlayerCount;
                return TurnResult.Foul;
            }

            Knocked[Current] += knocked;
            if (Knocked[Current] >= Rules.winTarget)
            {
                GamesWon[Current]++;
                if (GamesWon[Current] < Rules.gamesToWin) return TurnResult.GameWon;
                MatchWinner = Current;
                return TurnResult.MatchWon;
            }

            bool again = knocked > 0 && Rules.throwAgainOnHit && !(saqaInOrda && Rules.saqaInOrdaEndsTurn);
            if (again) return TurnResult.ThrowAgain;
            Current = (Current + 1) % PlayerCount;
            return TurnResult.NextPlayer;
        }

        /// <summary>
        /// Кто ходит первым по броску сақа: у кого сторона выше в rules.sideRank.
        /// −1 — лучшая сторона выпала у нескольких, бросают заново.
        /// </summary>
        public static int FirstByToss(AsykRules rules, IList<AsykSide> sides)
        {
            int best = -1, bestRank = int.MaxValue;
            bool tie = false;
            for (int i = 0; i < sides.Count; i++)
            {
                int rank = System.Array.IndexOf(rules.sideRank, sides[i]);
                if (rank < 0) rank = rules.sideRank.Length; // сторона не в списке — хуже всех
                if (rank < bestRank)
                {
                    best = i;
                    bestRank = rank;
                    tie = false;
                }
                else if (rank == bestRank)
                {
                    tie = true;
                }
            }
            return tie ? -1 : best;
        }
    }
}
