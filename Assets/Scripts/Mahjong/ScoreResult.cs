using System.Collections.Generic;
using System.Text;

namespace Mahjong
{
    /// <summary>成立した役 1 つ分。</summary>
    public class YakuEntry
    {
        public string Name { get; private set; }

        /// <summary>翻数(役満の場合は 0)。</summary>
        public int Han { get; private set; }

        /// <summary>役満の倍数(通常の役は 0、役満 1、ダブル役満 2)。</summary>
        public int YakumanMultiplier { get; private set; }

        public YakuEntry(string name, int han, int yakumanMultiplier)
        {
            Name = name;
            Han = han;
            YakumanMultiplier = yakumanMultiplier;
        }

        public override string ToString()
        {
            if (YakumanMultiplier >= 2) return Name + " (ダブル役満)";
            if (YakumanMultiplier == 1) return Name + " (役満)";
            return Name + " " + Han + "翻";
        }
    }

    /// <summary>点数計算の結果。</summary>
    public class ScoreResult
    {
        /// <summary>和了として成立しているか(和了形で、役が 1 つ以上ある)。</summary>
        public bool IsValid { get; internal set; }

        /// <summary>IsValid が false のときの理由。</summary>
        public string Error { get; internal set; }

        public List<YakuEntry> Yaku { get; internal set; }

        /// <summary>合計翻数(ドラ込み)。役満の場合は 0。</summary>
        public int Han { get; internal set; }
        public int Fu { get; internal set; }

        /// <summary>役満の倍数(役満でなければ 0、数え役満は 1)。</summary>
        public int YakumanMultiplier { get; internal set; }

        /// <summary>"満貫" "跳満" "倍満" "三倍満" "役満" "数え役満" など。満貫未満は空文字。</summary>
        public string LimitName { get; internal set; }

        public bool IsDealer { get; internal set; }
        public bool IsTsumo { get; internal set; }

        /// <summary>ロン和了で放銃者が払う点数(本場込み)。</summary>
        public int RonPayment { get; internal set; }

        /// <summary>子のツモ和了で親が払う点数(本場込み)。親のツモ和了では 0。</summary>
        public int TsumoPaymentDealer { get; internal set; }

        /// <summary>ツモ和了で子がそれぞれ払う点数(本場込み)。</summary>
        public int TsumoPaymentNonDealer { get; internal set; }

        /// <summary>和了者が受け取る合計点数(本場・リーチ棒込み)。</summary>
        public int TotalGain { get; internal set; }

        public ScoreResult()
        {
            Yaku = new List<YakuEntry>();
            LimitName = "";
            Error = "";
        }

        /// <summary>点数の表記。例: "7700" "2000-3900" "3900オール"</summary>
        public string PaymentText
        {
            get
            {
                if (!IsValid) return "";
                if (!IsTsumo) return RonPayment.ToString();
                if (IsDealer) return TsumoPaymentNonDealer + "オール";
                return TsumoPaymentNonDealer + "-" + TsumoPaymentDealer;
            }
        }

        public override string ToString()
        {
            if (!IsValid) return "和了不成立: " + Error;
            var sb = new StringBuilder();
            foreach (var y in Yaku) sb.AppendLine(y.ToString());
            if (YakumanMultiplier > 0 && Han == 0)
                sb.Append(LimitName);
            else
            {
                sb.Append(Han + "翻" + Fu + "符");
                if (LimitName != "") sb.Append(" " + LimitName);
            }
            sb.Append(" " + PaymentText);
            sb.Append(" (合計 " + TotalGain + "点)");
            return sb.ToString();
        }
    }
}
