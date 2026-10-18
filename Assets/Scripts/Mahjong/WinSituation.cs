using System.Collections.Generic;

namespace Mahjong
{
    /// <summary>和了したときの状況(ツモ/ロン、リーチ、場風、ドラなど)。</summary>
    public class WinSituation
    {
        public bool IsTsumo { get; set; }
        public bool IsRiichi { get; set; }
        public bool IsDoubleRiichi { get; set; }
        public bool IsIppatsu { get; set; }

        /// <summary>最後の牌での和了。ツモなら海底摸月、ロンなら河底撈魚。</summary>
        public bool IsLastTile { get; set; }
        public bool IsRinshan { get; set; }
        public bool IsChankan { get; set; }
        public bool IsTenhou { get; set; }
        public bool IsChiihou { get; set; }

        /// <summary>自風(Tiles.East〜Tiles.North)。東なら親。</summary>
        public int SeatWind { get; set; }

        /// <summary>場風(Tiles.East〜Tiles.North)。</summary>
        public int RoundWind { get; set; }

        /// <summary>ドラ表示牌(ドラそのものではなく表示牌を入れる)。</summary>
        public List<int> DoraIndicators { get; set; }

        /// <summary>裏ドラ表示牌。リーチしていない場合は無視されます。</summary>
        public List<int> UraDoraIndicators { get; set; }

        /// <summary>赤ドラの枚数。</summary>
        public int AkaDora { get; set; }

        /// <summary>本場数。</summary>
        public int Honba { get; set; }

        /// <summary>場に出ているリーチ棒の本数(和了者がもらう)。</summary>
        public int RiichiSticks { get; set; }

        public bool IsDealer { get { return SeatWind == Tiles.East; } }

        public WinSituation()
        {
            SeatWind = Tiles.South;
            RoundWind = Tiles.East;
            DoraIndicators = new List<int>();
            UraDoraIndicators = new List<int>();
        }
    }

    /// <summary>ローカルルールの設定。</summary>
    public class ScoreRules
    {
        /// <summary>喰いタンあり。</summary>
        public bool AllowKuitan { get; set; }

        /// <summary>13 翻以上を数え役満にする(false なら三倍満止まり)。</summary>
        public bool KazoeYakuman { get; set; }

        /// <summary>ダブル役満あり(国士十三面、四暗刻単騎、純正九蓮、大四喜)。</summary>
        public bool DoubleYakuman { get; set; }

        /// <summary>複数の役満の複合あり。</summary>
        public bool YakumanStacking { get; set; }

        /// <summary>切り上げ満貫(4翻30符・3翻60符を満貫にする)。</summary>
        public bool KiriageMangan { get; set; }

        /// <summary>連風牌(ダブ東など)の雀頭の符。一般的には 4 符、2 符とするルールもあります。</summary>
        public int DoubleWindPairFu { get; set; }

        public ScoreRules()
        {
            AllowKuitan = true;
            KazoeYakuman = true;
            DoubleYakuman = true;
            YakumanStacking = true;
            KiriageMangan = false;
            DoubleWindPairFu = 4;
        }
    }
}
