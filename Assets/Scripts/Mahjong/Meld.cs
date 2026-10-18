using System;
using System.Collections.Generic;

namespace Mahjong
{
    public enum MeldType
    {
        Chi,     // チー
        Pon,     // ポン
        Minkan,  // 明槓(大明槓・加槓)
        Ankan,   // 暗槓
    }

    /// <summary>鳴いた面子(副露)と暗槓。</summary>
    public class Meld
    {
        public MeldType Type { get; private set; }

        /// <summary>面子の牌。チーなら一番小さい牌。</summary>
        public int Tile { get; private set; }

        public Meld(MeldType type, int tile)
        {
            if (type == MeldType.Chi && (Tiles.IsHonor(tile) || tile % 9 > 6))
                throw new ArgumentException("チーの先頭牌が不正です: " + Tiles.Name(tile));
            Type = type;
            Tile = tile;
        }

        /// <summary>暗槓以外は門前が崩れます。</summary>
        public bool IsOpen { get { return Type != MeldType.Ankan; } }
        public bool IsKan { get { return Type == MeldType.Minkan || Type == MeldType.Ankan; } }

        /// <summary>この面子に含まれる牌(槓子は 4 枚)。</summary>
        public List<int> GetTiles()
        {
            var list = new List<int>();
            if (Type == MeldType.Chi)
            {
                list.Add(Tile); list.Add(Tile + 1); list.Add(Tile + 2);
            }
            else
            {
                int n = IsKan ? 4 : 3;
                for (int i = 0; i < n; i++) list.Add(Tile);
            }
            return list;
        }

        public static Meld Chi(int firstTile) { return new Meld(MeldType.Chi, firstTile); }
        public static Meld Pon(int tile) { return new Meld(MeldType.Pon, tile); }
        public static Meld Minkan(int tile) { return new Meld(MeldType.Minkan, tile); }
        public static Meld Ankan(int tile) { return new Meld(MeldType.Ankan, tile); }

        /// <summary>例: Meld.Chi("345p")</summary>
        public static Meld Chi(string tiles)
        {
            var list = Tiles.Parse(tiles);
            list.Sort();
            if (list.Count != 3 || list[1] != list[0] + 1 || list[2] != list[0] + 2)
                throw new ArgumentException("チーは連続した 3 枚で指定してください: " + tiles);
            return Chi(list[0]);
        }

        /// <summary>例: Meld.Pon("7z")  ※1 枚だけ指定</summary>
        public static Meld Pon(string tile) { return Pon(Tiles.ParseOne(tile)); }
        public static Meld Minkan(string tile) { return Minkan(Tiles.ParseOne(tile)); }
        public static Meld Ankan(string tile) { return Ankan(Tiles.ParseOne(tile)); }

        public override string ToString()
        {
            return Type + "(" + Tiles.ToShortString(GetTiles()) + ")";
        }
    }
}
