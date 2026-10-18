using System;
using System.Collections.Generic;

namespace Mahjong
{
    public enum WaitType
    {
        Ryanmen,  // 両面
        Kanchan,  // 嵌張
        Penchan,  // 辺張
        Shanpon,  // 双碰
        Tanki,    // 単騎
    }

    /// <summary>
    /// リーチ麻雀の点数計算。
    /// 手牌を和了形に分解し、役・符・点数を求めます。
    /// 高目・安目など分解方法が複数ある場合は、最も点数が高くなる解釈を採用します。
    /// </summary>
    public static class ScoreCalculator
    {
        // ------------------------------------------------------------
        // 公開 API
        // ------------------------------------------------------------

        /// <summary>
        /// 和了の点数を計算します。
        /// </summary>
        /// <param name="concealedTiles">和了牌を除いた手牌(副露・暗槓を除く)。13 - 3×副露数 枚。</param>
        /// <param name="winTile">和了牌(ツモった牌、またはロンした牌)。</param>
        /// <param name="melds">副露・暗槓。なければ null。</param>
        /// <param name="situation">和了の状況。</param>
        /// <param name="rules">ルール設定。null なら標準ルール。</param>
        public static ScoreResult Calculate(List<int> concealedTiles, int winTile, List<Meld> melds,
            WinSituation situation, ScoreRules rules = null)
        {
            if (concealedTiles == null) throw new ArgumentNullException("concealedTiles");
            if (situation == null) throw new ArgumentNullException("situation");
            if (melds == null) melds = new List<Meld>();
            if (rules == null) rules = new ScoreRules();
            if (winTile < 0 || winTile >= Tiles.Count) throw new ArgumentException("和了牌が不正です");
            if (melds.Count > 4) throw new ArgumentException("副露が多すぎます");
            if (concealedTiles.Count != 13 - 3 * melds.Count)
                throw new ArgumentException("手牌の枚数が正しくありません(和了牌を除いて " + (13 - 3 * melds.Count) +
                                            " 枚必要、実際は " + concealedTiles.Count + " 枚)");

            var hand = new int[Tiles.Count];        // 手牌 + 和了牌
            var handNoWin = new int[Tiles.Count];   // 手牌のみ
            var all = new int[Tiles.Count];         // 副露も含めた全ての牌
            foreach (int t in concealedTiles)
            {
                if (t < 0 || t >= Tiles.Count) throw new ArgumentException("牌の番号が不正です: " + t);
                hand[t]++; handNoWin[t]++; all[t]++;
            }
            hand[winTile]++; all[winTile]++;
            foreach (var m in melds)
                foreach (int t in m.GetTiles()) all[t]++;
            for (int i = 0; i < Tiles.Count; i++)
                if (all[i] > 4) throw new ArgumentException(Tiles.Name(i) + " が 5 枚以上あります");

            var ctx = new Context();
            ctx.Hand = hand;
            ctx.HandNoWin = handNoWin;
            ctx.All = all;
            ctx.WinTile = winTile;
            ctx.Melds = melds;
            ctx.Menzen = true;
            foreach (var m in melds) if (m.IsOpen) ctx.Menzen = false;
            ctx.Sit = situation;
            ctx.Rules = rules;

            var shapes = FindShapes(ctx);
            if (shapes.Count == 0) return Invalid("和了形ではありません");

            ScoreResult best = null;
            foreach (var shape in shapes)
            {
                var r = Evaluate(shape, ctx);
                if (r == null) continue;
                if (best == null || IsBetter(r, best)) best = r;
            }
            if (best == null) return Invalid("役がありません");
            return best;
        }

        /// <summary>文字列で手牌を指定する版。例: Calculate("123m456p789s1122z", "2z", null, situation)</summary>
        public static ScoreResult Calculate(string concealedTiles, string winTile, List<Meld> melds,
            WinSituation situation, ScoreRules rules = null)
        {
            return Calculate(Tiles.Parse(concealedTiles), Tiles.ParseOne(winTile), melds, situation, rules);
        }

        /// <summary>
        /// 翻数と符から点数だけを求めます(役判定を自前で行う場合など)。
        /// </summary>
        public static ScoreResult CalculatePoints(int han, int fu, bool isDealer, bool isTsumo,
            int honba = 0, int riichiSticks = 0, ScoreRules rules = null)
        {
            if (rules == null) rules = new ScoreRules();
            var r = new ScoreResult();
            r.IsValid = true;
            r.Han = han;
            r.Fu = fu;
            FillPayments(r, isDealer, isTsumo, honba, riichiSticks, rules);
            return r;
        }

        /// <summary>役満の点数だけを求めます。multiplier は 1 で役満、2 でダブル役満。</summary>
        public static ScoreResult CalculateYakumanPoints(int multiplier, bool isDealer, bool isTsumo,
            int honba = 0, int riichiSticks = 0)
        {
            var r = new ScoreResult();
            r.IsValid = true;
            r.YakumanMultiplier = multiplier;
            FillPayments(r, isDealer, isTsumo, honba, riichiSticks, new ScoreRules());
            return r;
        }

        // ------------------------------------------------------------
        // 内部データ
        // ------------------------------------------------------------

        private enum BlockType { Sequence, Triplet, Kan }

        private class Block
        {
            public BlockType Type;
            public int Tile;          // 順子なら先頭の牌
            public bool IsConcealed;  // 暗刻・暗槓・門前の順子

            public Block(BlockType type, int tile, bool concealed)
            {
                Type = type; Tile = tile; IsConcealed = concealed;
            }

            public bool IsSet3 { get { return Type != BlockType.Sequence; } } // 刻子または槓子

            public bool HasTerminalOrHonor
            {
                get
                {
                    if (Type == BlockType.Sequence) return Tiles.IsTerminal(Tile) || Tiles.IsTerminal(Tile + 2);
                    return Tiles.IsTerminalOrHonor(Tile);
                }
            }
        }

        private class Shape
        {
            public List<Block> Sets = new List<Block>();
            public int Pair = -1;
            public WaitType Wait;
            public bool IsChiitoi;
            public bool IsKokushi;
        }

        private class Context
        {
            public int[] Hand;
            public int[] HandNoWin;
            public int[] All;
            public int WinTile;
            public List<Meld> Melds;
            public bool Menzen;
            public WinSituation Sit;
            public ScoreRules Rules;
        }

        private static ScoreResult Invalid(string message)
        {
            var r = new ScoreResult();
            r.IsValid = false;
            r.Error = message;
            return r;
        }

        private static bool IsBetter(ScoreResult a, ScoreResult b)
        {
            if (a.TotalGain != b.TotalGain) return a.TotalGain > b.TotalGain;
            if (a.Han != b.Han) return a.Han > b.Han;
            return a.Fu > b.Fu;
        }

        // ------------------------------------------------------------
        // 和了形の分解
        // ------------------------------------------------------------

        private static readonly int[] Yaochu = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 };

        private static List<Shape> FindShapes(Context ctx)
        {
            var shapes = new List<Shape>();
            var c = (int[])ctx.Hand.Clone();

            // 通常形(4面子1雀頭)
            for (int p = 0; p < Tiles.Count; p++)
            {
                if (c[p] < 2) continue;
                c[p] -= 2;
                FindSets(c, 0, new List<Block>(), p, ctx, shapes);
                c[p] += 2;
            }

            if (ctx.Melds.Count == 0)
            {
                // 七対子(同じ牌 4 枚を 2 対子とはみなさない)
                int pairs = 0;
                for (int i = 0; i < Tiles.Count; i++) if (c[i] == 2) pairs++;
                if (pairs == 7)
                {
                    var s = new Shape();
                    s.IsChiitoi = true;
                    s.Wait = WaitType.Tanki;
                    shapes.Add(s);
                }

                // 国士無双
                int kinds = 0, total = 0;
                foreach (int t in Yaochu)
                {
                    if (c[t] > 0) kinds++;
                    total += c[t];
                }
                if (kinds == 13 && total == 14)
                {
                    var s = new Shape();
                    s.IsKokushi = true;
                    s.Wait = WaitType.Tanki;
                    shapes.Add(s);
                }
            }
            return shapes;
        }

        // 残りの牌を刻子・順子に分解する。一番小さい牌は必ず刻子か順子の先頭になる。
        private static void FindSets(int[] c, int start, List<Block> current, int pair, Context ctx, List<Shape> shapes)
        {
            int i = start;
            while (i < Tiles.Count && c[i] == 0) i++;
            if (i == Tiles.Count)
            {
                AddWaitVariants(current, pair, ctx, shapes);
                return;
            }

            if (c[i] >= 3)
            {
                c[i] -= 3;
                current.Add(new Block(BlockType.Triplet, i, true));
                FindSets(c, i, current, pair, ctx, shapes);
                current.RemoveAt(current.Count - 1);
                c[i] += 3;
            }
            if (i < 27 && i % 9 <= 6 && c[i + 1] > 0 && c[i + 2] > 0)
            {
                c[i]--; c[i + 1]--; c[i + 2]--;
                current.Add(new Block(BlockType.Sequence, i, true));
                FindSets(c, i, current, pair, ctx, shapes);
                current.RemoveAt(current.Count - 1);
                c[i]++; c[i + 1]++; c[i + 2]++;
            }
        }

        // 和了牌がどの面子に入ったかで待ちの形が変わるので、全パターンを作る。
        private static void AddWaitVariants(List<Block> concealedSets, int pair, Context ctx, List<Shape> shapes)
        {
            int w = ctx.WinTile;

            if (pair == w) shapes.Add(MakeShape(concealedSets, pair, ctx, -1, WaitType.Tanki));

            for (int i = 0; i < concealedSets.Count; i++)
            {
                var b = concealedSets[i];
                if (b.Type == BlockType.Triplet && b.Tile == w)
                {
                    shapes.Add(MakeShape(concealedSets, pair, ctx, i, WaitType.Shanpon));
                }
                else if (b.Type == BlockType.Sequence && w >= b.Tile && w <= b.Tile + 2)
                {
                    WaitType wait;
                    int pos = b.Tile % 9; // 順子の先頭の数字 - 1
                    if (w == b.Tile + 1) wait = WaitType.Kanchan;
                    else if (w == b.Tile + 2 && pos == 0) wait = WaitType.Penchan;  // 12 で 3 待ち
                    else if (w == b.Tile && pos == 6) wait = WaitType.Penchan;      // 89 で 7 待ち
                    else wait = WaitType.Ryanmen;
                    shapes.Add(MakeShape(concealedSets, pair, ctx, i, wait));
                }
            }
        }

        private static Shape MakeShape(List<Block> concealedSets, int pair, Context ctx, int winBlockIndex, WaitType wait)
        {
            var s = new Shape();
            s.Pair = pair;
            s.Wait = wait;
            for (int i = 0; i < concealedSets.Count; i++)
            {
                var b = concealedSets[i];
                bool concealed = true;
                // ロンで完成した刻子は明刻扱い
                if (i == winBlockIndex && b.Type == BlockType.Triplet && !ctx.Sit.IsTsumo) concealed = false;
                s.Sets.Add(new Block(b.Type, b.Tile, concealed));
            }
            foreach (var m in ctx.Melds)
            {
                switch (m.Type)
                {
                    case MeldType.Chi: s.Sets.Add(new Block(BlockType.Sequence, m.Tile, false)); break;
                    case MeldType.Pon: s.Sets.Add(new Block(BlockType.Triplet, m.Tile, false)); break;
                    case MeldType.Minkan: s.Sets.Add(new Block(BlockType.Kan, m.Tile, false)); break;
                    case MeldType.Ankan: s.Sets.Add(new Block(BlockType.Kan, m.Tile, true)); break;
                }
            }
            return s;
        }

        // ------------------------------------------------------------
        // 役の判定
        // ------------------------------------------------------------

        private static ScoreResult Evaluate(Shape s, Context ctx)
        {
            var sit = ctx.Sit;
            var rules = ctx.Rules;

            var result = new ScoreResult();
            result.IsValid = true;
            result.IsDealer = sit.IsDealer;
            result.IsTsumo = sit.IsTsumo;

            // ---------- 役満 ----------
            var yakuman = EvaluateYakuman(s, ctx);
            if (yakuman.Count > 0)
            {
                if (!rules.YakumanStacking)
                {
                    YakuEntry max = yakuman[0];
                    foreach (var y in yakuman) if (y.YakumanMultiplier > max.YakumanMultiplier) max = y;
                    yakuman = new List<YakuEntry> { max };
                }
                int mult = 0;
                foreach (var y in yakuman) mult += y.YakumanMultiplier;
                result.Yaku = yakuman;
                result.YakumanMultiplier = mult;
                FillPayments(result, sit.IsDealer, sit.IsTsumo, sit.Honba, sit.RiichiSticks, rules);
                return result;
            }

            // ---------- 通常の役 ----------
            var yaku = new List<YakuEntry>();
            bool menzen = ctx.Menzen;
            var all = ctx.All;

            if (sit.IsDoubleRiichi) Add(yaku, "ダブル立直", 2);
            else if (sit.IsRiichi) Add(yaku, "立直", 1);
            if (sit.IsIppatsu && (sit.IsRiichi || sit.IsDoubleRiichi)) Add(yaku, "一発", 1);
            if (menzen && sit.IsTsumo) Add(yaku, "門前清自摸和", 1);
            if (sit.IsLastTile) Add(yaku, sit.IsTsumo ? "海底摸月" : "河底撈魚", 1);
            if (sit.IsRinshan && sit.IsTsumo) Add(yaku, "嶺上開花", 1);
            if (sit.IsChankan && !sit.IsTsumo) Add(yaku, "槍槓", 1);

            // 牌の種類から決まる役
            bool allSimple = true, allYaochu = true, hasHonor = false;
            var suits = new bool[3];
            for (int t = 0; t < Tiles.Count; t++)
            {
                if (all[t] == 0) continue;
                if (Tiles.IsTerminalOrHonor(t)) allSimple = false; else allYaochu = false;
                if (Tiles.IsHonor(t)) hasHonor = true; else suits[Tiles.Suit(t)] = true;
            }
            int suitCount = (suits[0] ? 1 : 0) + (suits[1] ? 1 : 0) + (suits[2] ? 1 : 0);

            if (allSimple && (menzen || rules.AllowKuitan)) Add(yaku, "断幺九", 1);
            if (suitCount == 1 && hasHonor) Add(yaku, "混一色", menzen ? 3 : 2);
            if (suitCount == 1 && !hasHonor) Add(yaku, "清一色", menzen ? 6 : 5);
            if (allYaochu) Add(yaku, "混老頭", 2);

            bool pinfu = false;
            if (s.IsChiitoi)
            {
                Add(yaku, "七対子", 2);
            }
            else
            {
                var sets = s.Sets;
                int seqCount = 0, set3Count = 0, ankou = 0, kans = 0, dragonSets = 0;
                foreach (var b in sets)
                {
                    if (b.Type == BlockType.Sequence) seqCount++;
                    else
                    {
                        set3Count++;
                        if (b.IsConcealed) ankou++;
                        if (b.Type == BlockType.Kan) kans++;
                        if (Tiles.IsDragon(b.Tile)) dragonSets++;
                    }
                }

                // 平和
                if (menzen && seqCount == 4 && s.Wait == WaitType.Ryanmen && !IsYakuhaiTile(s.Pair, sit))
                {
                    pinfu = true;
                    Add(yaku, "平和", 1);
                }

                // 一盃口・二盃口
                if (menzen)
                {
                    var seqCounts = new int[Tiles.Count];
                    foreach (var b in sets) if (b.Type == BlockType.Sequence) seqCounts[b.Tile]++;
                    int peikou = 0;
                    foreach (int n in seqCounts) peikou += n / 2;
                    if (peikou == 2) Add(yaku, "二盃口", 3);
                    else if (peikou == 1) Add(yaku, "一盃口", 1);
                }

                // 役牌
                foreach (var b in sets)
                {
                    if (!b.IsSet3) continue;
                    if (b.Tile == Tiles.Haku) Add(yaku, "役牌 白", 1);
                    if (b.Tile == Tiles.Hatsu) Add(yaku, "役牌 發", 1);
                    if (b.Tile == Tiles.Chun) Add(yaku, "役牌 中", 1);
                    if (b.Tile == sit.SeatWind) Add(yaku, "自風 " + Tiles.Name(b.Tile), 1);
                    if (b.Tile == sit.RoundWind) Add(yaku, "場風 " + Tiles.Name(b.Tile), 1);
                }

                // 三色同順・一気通貫・三色同刻
                for (int n = 0; n < 7; n++)
                {
                    if (HasSeq(sets, n) && HasSeq(sets, 9 + n) && HasSeq(sets, 18 + n))
                    {
                        Add(yaku, "三色同順", menzen ? 2 : 1);
                        break;
                    }
                }
                for (int suit = 0; suit < 3; suit++)
                {
                    int b0 = suit * 9;
                    if (HasSeq(sets, b0) && HasSeq(sets, b0 + 3) && HasSeq(sets, b0 + 6))
                    {
                        Add(yaku, "一気通貫", menzen ? 2 : 1);
                        break;
                    }
                }
                for (int n = 0; n < 9; n++)
                {
                    if (HasSet3(sets, n) && HasSet3(sets, 9 + n) && HasSet3(sets, 18 + n))
                    {
                        Add(yaku, "三色同刻", 2);
                        break;
                    }
                }

                // 混全帯幺九・純全帯幺九(順子がない場合は混老頭・清老頭になる)
                bool allHaveYaochu = Tiles.IsTerminalOrHonor(s.Pair);
                foreach (var b in sets) if (!b.HasTerminalOrHonor) allHaveYaochu = false;
                if (allHaveYaochu && seqCount > 0)
                {
                    if (hasHonor) Add(yaku, "混全帯幺九", menzen ? 2 : 1);
                    else Add(yaku, "純全帯幺九", menzen ? 3 : 2);
                }

                if (set3Count == 4) Add(yaku, "対々和", 2);
                if (ankou == 3) Add(yaku, "三暗刻", 2);
                if (kans == 3) Add(yaku, "三槓子", 2);
                if (dragonSets == 2 && Tiles.IsDragon(s.Pair)) Add(yaku, "小三元", 2);
            }

            int han = 0;
            foreach (var y in yaku) han += y.Han;
            if (han == 0) return null; // 役なし(ドラだけでは和了できない)

            // ---------- ドラ ----------
            int dora = CountDora(sit.DoraIndicators, all);
            int ura = (sit.IsRiichi || sit.IsDoubleRiichi) ? CountDora(sit.UraDoraIndicators, all) : 0;
            if (dora > 0) Add(yaku, "ドラ", dora);
            if (ura > 0) Add(yaku, "裏ドラ", ura);
            if (sit.AkaDora > 0) Add(yaku, "赤ドラ", sit.AkaDora);
            han += dora + ura + sit.AkaDora;

            result.Yaku = yaku;
            result.Han = han;
            result.Fu = CalculateFu(s, ctx, pinfu);
            FillPayments(result, sit.IsDealer, sit.IsTsumo, sit.Honba, sit.RiichiSticks, rules);
            return result;
        }

        private static List<YakuEntry> EvaluateYakuman(Shape s, Context ctx)
        {
            var list = new List<YakuEntry>();
            var sit = ctx.Sit;
            int dbl = ctx.Rules.DoubleYakuman ? 2 : 1;
            var all = ctx.All;

            if (sit.IsTenhou && sit.IsDealer && sit.IsTsumo) AddYakuman(list, "天和", 1);
            if (sit.IsChiihou && !sit.IsDealer && sit.IsTsumo) AddYakuman(list, "地和", 1);

            if (s.IsKokushi)
            {
                bool thirteenWait = true;
                foreach (int t in Yaochu) if (ctx.HandNoWin[t] != 1) thirteenWait = false;
                if (thirteenWait) AddYakuman(list, "国士無双十三面待ち", dbl);
                else AddYakuman(list, "国士無双", 1);
            }

            // 牌の種類だけで決まる役満
            bool allHonor = true, allTerminal = true, allGreen = true;
            for (int t = 0; t < Tiles.Count; t++)
            {
                if (all[t] == 0) continue;
                if (!Tiles.IsHonor(t)) allHonor = false;
                if (!Tiles.IsTerminal(t)) allTerminal = false;
                if (!IsGreen(t)) allGreen = false;
            }
            if (allHonor) AddYakuman(list, "字一色", 1);
            if (allTerminal) AddYakuman(list, "清老頭", 1);
            if (allGreen) AddYakuman(list, "緑一色", 1);

            // 九蓮宝燈(副露・暗槓なし、1 種類の数牌のみ)
            if (ctx.Melds.Count == 0 && !s.IsChiitoi && !s.IsKokushi)
            {
                for (int suit = 0; suit < 3; suit++)
                {
                    int b0 = suit * 9;
                    int inSuit = 0;
                    for (int k = 0; k < 9; k++) inSuit += ctx.Hand[b0 + k];
                    if (inSuit != 14) continue;
                    var need = new[] { 3, 1, 1, 1, 1, 1, 1, 1, 3 };
                    bool ok = true, pure = true;
                    for (int k = 0; k < 9; k++)
                    {
                        if (ctx.Hand[b0 + k] < need[k]) ok = false;
                        if (ctx.HandNoWin[b0 + k] != need[k]) pure = false;
                    }
                    if (ok)
                    {
                        if (pure) AddYakuman(list, "純正九蓮宝燈", dbl);
                        else AddYakuman(list, "九蓮宝燈", 1);
                    }
                }
            }

            if (s.IsChiitoi || s.IsKokushi) return list;

            int ankou = 0, kans = 0, dragons = 0, winds = 0;
            foreach (var b in s.Sets)
            {
                if (!b.IsSet3) continue;
                if (b.IsConcealed) ankou++;
                if (b.Type == BlockType.Kan) kans++;
                if (Tiles.IsDragon(b.Tile)) dragons++;
                if (Tiles.IsWind(b.Tile)) winds++;
            }
            if (ankou == 4)
            {
                if (s.Wait == WaitType.Tanki) AddYakuman(list, "四暗刻単騎", dbl);
                else AddYakuman(list, "四暗刻", 1);
            }
            if (dragons == 3) AddYakuman(list, "大三元", 1);
            if (winds == 4) AddYakuman(list, "大四喜", dbl);
            else if (winds == 3 && Tiles.IsWind(s.Pair)) AddYakuman(list, "小四喜", 1);
            if (kans == 4) AddYakuman(list, "四槓子", 1);
            return list;
        }

        private static void Add(List<YakuEntry> list, string name, int han)
        {
            list.Add(new YakuEntry(name, han, 0));
        }

        private static void AddYakuman(List<YakuEntry> list, string name, int multiplier)
        {
            list.Add(new YakuEntry(name, 0, multiplier));
        }

        private static bool HasSeq(List<Block> sets, int tile)
        {
            foreach (var b in sets) if (b.Type == BlockType.Sequence && b.Tile == tile) return true;
            return false;
        }

        private static bool HasSet3(List<Block> sets, int tile)
        {
            foreach (var b in sets) if (b.IsSet3 && b.Tile == tile) return true;
            return false;
        }

        // 緑一色に使える牌: 2・3・4・6・8索、發
        private static bool IsGreen(int t)
        {
            return t == 19 || t == 20 || t == 21 || t == 23 || t == 25 || t == Tiles.Hatsu;
        }

        private static bool IsYakuhaiTile(int t, WinSituation sit)
        {
            return Tiles.IsDragon(t) || t == sit.SeatWind || t == sit.RoundWind;
        }

        private static int CountDora(List<int> indicators, int[] all)
        {
            if (indicators == null) return 0;
            int n = 0;
            foreach (int ind in indicators) n += all[Tiles.DoraFromIndicator(ind)];
            return n;
        }

        // ------------------------------------------------------------
        // 符計算
        // ------------------------------------------------------------

        private static int CalculateFu(Shape s, Context ctx, bool pinfu)
        {
            var sit = ctx.Sit;
            if (s.IsChiitoi) return 25;
            if (pinfu) return sit.IsTsumo ? 20 : 30;

            int fu = 20;                                   // 副底
            if (ctx.Menzen && !sit.IsTsumo) fu += 10;      // 門前加符
            if (sit.IsTsumo) fu += 2;                      // ツモ符

            foreach (var b in s.Sets)
            {
                if (!b.IsSet3) continue;
                int v = 2;                                  // 明刻(中張牌)
                if (Tiles.IsTerminalOrHonor(b.Tile)) v *= 2;
                if (b.IsConcealed) v *= 2;
                if (b.Type == BlockType.Kan) v *= 4;
                fu += v;
            }

            // 雀頭
            int p = s.Pair;
            if (Tiles.IsDragon(p)) fu += 2;
            if (p == sit.SeatWind && p == sit.RoundWind) fu += ctx.Rules.DoubleWindPairFu;
            else if (p == sit.SeatWind || p == sit.RoundWind) fu += 2;

            // 待ち
            if (s.Wait == WaitType.Kanchan || s.Wait == WaitType.Penchan || s.Wait == WaitType.Tanki) fu += 2;

            // 鳴いて符がつかない手(喰い平和形)は 30 符
            if (!ctx.Menzen && fu == 20) fu = 30;

            return RoundUp(fu, 10);
        }

        // ------------------------------------------------------------
        // 点数計算
        // ------------------------------------------------------------

        private static void FillPayments(ScoreResult r, bool isDealer, bool isTsumo, int honba, int riichiSticks, ScoreRules rules)
        {
            r.IsDealer = isDealer;
            r.IsTsumo = isTsumo;

            int basePoints;
            if (r.YakumanMultiplier > 0)
            {
                basePoints = 8000 * r.YakumanMultiplier;
                r.LimitName = YakumanName(r.YakumanMultiplier);
            }
            else if (r.Han >= 13 && rules.KazoeYakuman)
            {
                basePoints = 8000;
                r.YakumanMultiplier = 1;
                r.LimitName = "数え役満";
            }
            else if (r.Han >= 11) { basePoints = 6000; r.LimitName = "三倍満"; }
            else if (r.Han >= 8) { basePoints = 4000; r.LimitName = "倍満"; }
            else if (r.Han >= 6) { basePoints = 3000; r.LimitName = "跳満"; }
            else
            {
                // 基本点 = 符 × 2^(翻+2)
                basePoints = r.Fu * (1 << (r.Han + 2));
                if (r.Han >= 5 || basePoints > 2000 || (rules.KiriageMangan && basePoints >= 1920))
                {
                    basePoints = 2000;
                    r.LimitName = "満貫";
                }
                else
                {
                    r.LimitName = "";
                }
            }

            if (isTsumo)
            {
                r.RonPayment = 0;
                if (isDealer)
                {
                    r.TsumoPaymentDealer = 0;
                    r.TsumoPaymentNonDealer = RoundUp(basePoints * 2, 100) + 100 * honba;
                    r.TotalGain = r.TsumoPaymentNonDealer * 3;
                }
                else
                {
                    r.TsumoPaymentDealer = RoundUp(basePoints * 2, 100) + 100 * honba;
                    r.TsumoPaymentNonDealer = RoundUp(basePoints, 100) + 100 * honba;
                    r.TotalGain = r.TsumoPaymentDealer + r.TsumoPaymentNonDealer * 2;
                }
            }
            else
            {
                r.TsumoPaymentDealer = 0;
                r.TsumoPaymentNonDealer = 0;
                r.RonPayment = RoundUp(basePoints * (isDealer ? 6 : 4), 100) + 300 * honba;
                r.TotalGain = r.RonPayment;
            }
            r.TotalGain += 1000 * riichiSticks;
        }

        private static string YakumanName(int multiplier)
        {
            switch (multiplier)
            {
                case 1: return "役満";
                case 2: return "ダブル役満";
                case 3: return "トリプル役満";
                default: return multiplier + "倍役満";
            }
        }

        private static int RoundUp(int value, int unit)
        {
            return (value + unit - 1) / unit * unit;
        }
    }
}
