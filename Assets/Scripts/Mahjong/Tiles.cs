using System;
using System.Collections.Generic;
using System.Text;

namespace Mahjong
{
    /// <summary>
    /// 牌は 0〜33 の整数で表します。
    ///  0- 8: 萬子 1〜9
    ///  9-17: 筒子 1〜9
    /// 18-26: 索子 1〜9
    /// 27-30: 東 南 西 北
    /// 31-33: 白 發 中
    /// </summary>
    public static class Tiles
    {
        public const int Count = 34;

        public const int East = 27;
        public const int South = 28;
        public const int West = 29;
        public const int North = 30;
        public const int Haku = 31;
        public const int Hatsu = 32;
        public const int Chun = 33;

        public static bool IsHonor(int t) { return t >= 27; }
        public static bool IsWind(int t) { return t >= 27 && t <= 30; }
        public static bool IsDragon(int t) { return t >= 31; }

        /// <summary>0:萬子 1:筒子 2:索子 3:字牌</summary>
        public static int Suit(int t) { return t / 9; }

        /// <summary>数牌の数字(1〜9)。字牌は 0。</summary>
        public static int Number(int t) { return IsHonor(t) ? 0 : t % 9 + 1; }

        public static bool IsTerminal(int t) { return !IsHonor(t) && (t % 9 == 0 || t % 9 == 8); }
        public static bool IsTerminalOrHonor(int t) { return IsHonor(t) || IsTerminal(t); }
        public static bool IsSimple(int t) { return !IsTerminalOrHonor(t); }

        /// <summary>ドラ表示牌から実際のドラを求めます。</summary>
        public static int DoraFromIndicator(int indicator)
        {
            if (indicator < 27)
            {
                int start = indicator / 9 * 9;
                return start + (indicator - start + 1) % 9;
            }
            if (indicator <= 30) return 27 + (indicator - 27 + 1) % 4;
            return 31 + (indicator - 31 + 1) % 3;
        }

        /// <summary>
        /// "123m456p789s1122z" のような文字列を牌のリストに変換します。
        /// m=萬子 p=筒子 s=索子 z=字牌(1東 2南 3西 4北 5白 6發 7中)。
        /// 0 は赤5として 5 と同じ牌に変換します(赤ドラの枚数は WinSituation.AkaDora で指定)。
        /// </summary>
        public static List<int> Parse(string text)
        {
            var result = new List<int>();
            var pending = new List<int>();
            foreach (char c in text)
            {
                if (c == ' ') continue;
                if (c >= '0' && c <= '9')
                {
                    pending.Add(c == '0' ? 5 : c - '0');
                    continue;
                }
                int offset;
                switch (c)
                {
                    case 'm': offset = 0; break;
                    case 'p': offset = 9; break;
                    case 's': offset = 18; break;
                    case 'z': offset = 27; break;
                    default: throw new ArgumentException("不明な文字です: " + c);
                }
                if (pending.Count == 0) throw new ArgumentException("数字のない種類指定があります: " + text);
                foreach (int n in pending)
                {
                    if (c == 'z' && (n < 1 || n > 7)) throw new ArgumentException("字牌は 1〜7 で指定してください: " + text);
                    result.Add(offset + n - 1);
                }
                pending.Clear();
            }
            if (pending.Count > 0) throw new ArgumentException("末尾に種類(m/p/s/z)がありません: " + text);
            return result;
        }

        /// <summary>1 枚だけの牌を文字列から取得します。例: ParseOne("5m")</summary>
        public static int ParseOne(string text)
        {
            var list = Parse(text);
            if (list.Count != 1) throw new ArgumentException("牌を 1 枚だけ指定してください: " + text);
            return list[0];
        }

        private static readonly string[] HonorNames = { "東", "南", "西", "北", "白", "發", "中" };
        private static readonly string[] SuitNames = { "萬", "筒", "索" };
        private static readonly string[] Kanji = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        /// <summary>牌の日本語名。例: 一萬、5筒、東</summary>
        public static string Name(int t)
        {
            if (IsHonor(t)) return HonorNames[t - 27];
            if (Suit(t) == 0) return Kanji[t % 9] + SuitNames[0];
            return (t % 9 + 1) + SuitNames[Suit(t)];
        }

        /// <summary>"123m" 形式の短い表記。</summary>
        public static string ToShortString(IEnumerable<int> tiles)
        {
            var sorted = new List<int>(tiles);
            sorted.Sort();
            var sb = new StringBuilder();
            int currentSuit = -1;
            foreach (int t in sorted)
            {
                int s = Suit(t);
                if (currentSuit != -1 && s != currentSuit) sb.Append("mpsz"[currentSuit]);
                currentSuit = s;
                sb.Append(IsHonor(t) ? t - 27 + 1 : t % 9 + 1);
            }
            if (currentSuit != -1) sb.Append("mpsz"[currentSuit]);
            return sb.ToString();
        }
    }
}
