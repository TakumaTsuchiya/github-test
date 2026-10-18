using System.Collections.Generic;
using UnityEngine;
using Mahjong;

/// <summary>
/// 点数計算の使い方のサンプル。空の GameObject に付けて再生すると Console に結果が出ます。
/// </summary>
public class MahjongScoreExample : MonoBehaviour
{
    void Start()
    {
        // 例1: 門前の手。立直・ツモ、ドラ表示牌 4p
        var situation = new WinSituation();
        situation.IsTsumo = true;
        situation.IsRiichi = true;
        situation.SeatWind = Tiles.South;   // 南家(子)
        situation.RoundWind = Tiles.East;   // 東場
        situation.DoraIndicators.Add(Tiles.ParseOne("4p"));

        // 手牌(和了牌を除く 13 枚)と和了牌
        ScoreResult result = ScoreCalculator.Calculate("23m456p345678s55p", "4m", null, situation);
        Debug.Log(result.ToString());

        // 例2: 鳴いた手(中ポン、9萬ポン)でロン
        var melds = new List<Meld> { Meld.Pon("7z"), Meld.Pon("9m") };
        var ron = new WinSituation();
        ron.IsTsumo = false;
        ron.SeatWind = Tiles.East;   // 親
        ScoreResult result2 = ScoreCalculator.Calculate("222p33s44s", "4s", melds, ron);
        Debug.Log(result2.ToString());

        // 結果の値は個別に取り出せます
        if (result2.IsValid)
        {
            Debug.Log("翻: " + result2.Han + " 符: " + result2.Fu + " 点数: " + result2.PaymentText);
            foreach (var yaku in result2.Yaku) Debug.Log(" - " + yaku);
        }

        // 例3: 翻と符だけ分かっているとき
        Debug.Log(ScoreCalculator.CalculatePoints(3, 40, false, false).PaymentText); // 5200
    }
}
