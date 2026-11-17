using LunarTrails.Core.Judging;

namespace LunarTrails.Core.Scoring
{
    /// <summary>達成ランプ。企画書「4. 判定とスコア」に対応。</summary>
    public enum Lamp
    {
        /// <summary>MISS あり（途中で落ちる仕組みはないので、最後まで遊べばクリア扱い）</summary>
        Clear,
        /// <summary>FULL TRACK：MISS なし</summary>
        FullTrack,
        /// <summary>PERFECT TRACK：全部 PERFECT 以上</summary>
        PerfectTrack,
        /// <summary>PERFECT TRACK PLUS：全部 PERFECT+（理論値）</summary>
        PerfectTrackPlus,
    }

    /// <summary>
    /// 1 回のプレイの結果。達成率とランプを、判定の集計から計算する。
    /// </summary>
    /// <remarks>
    /// 達成率は「判定ごとの点数の平均」：PERFECT+ 101%、PERFECT 100%、HIT 50%、MISS 0%（企画書の仮の値）。
    /// 全部 PERFECT+ なら 101%。点数（スコア）は出さず、達成率を主役にする前提。
    /// トレイルの途中のチェックやトラックも、1 つの判定として同じ重みで数える。
    /// </remarks>
    public sealed class PlayResult
    {
        public const double PerfectPlusRate = 101.0;
        public const double PerfectRate = 100.0;
        public const double HitRate = 50.0;

        public int PerfectPlus { get; }
        public int Perfect { get; }
        public int Hit { get; }
        public int Miss { get; }
        public int Total { get; }
        public int MaxCombo { get; }
        public int FastCount { get; }
        public int SlowCount { get; }
        /// <summary>全体のずれの平均（秒）。タイミングを問う判定がなければ null。</summary>
        public double? AverageOffset { get; }

        /// <summary>達成率（%）。0〜101。</summary>
        public double AchievementRate { get; }
        public Lamp Lamp { get; }

        public PlayResult(JudgeStats stats)
        {
            PerfectPlus = stats.Count(Judgement.PerfectPlus);
            Perfect = stats.Count(Judgement.Perfect);
            Hit = stats.Count(Judgement.Hit);
            Miss = stats.Count(Judgement.Miss);
            Total = stats.Total;
            MaxCombo = stats.MaxCombo;
            FastCount = stats.FastCount;
            SlowCount = stats.SlowCount;
            AverageOffset = stats.AverageOffset;

            AchievementRate = Total == 0
                ? 0.0
                : (PerfectPlus * PerfectPlusRate + Perfect * PerfectRate + Hit * HitRate) / Total;

            if (Total > 0 && PerfectPlus == Total) Lamp = Lamp.PerfectTrackPlus;
            else if (Total > 0 && PerfectPlus + Perfect == Total) Lamp = Lamp.PerfectTrack;
            else if (Total > 0 && Miss == 0) Lamp = Lamp.FullTrack;
            else Lamp = Lamp.Clear;
        }

        /// <summary>ランプの表示名（略称）</summary>
        public static string LampLabel(Lamp lamp)
        {
            switch (lamp)
            {
                case Lamp.PerfectTrackPlus: return "PT+ (PERFECT TRACK PLUS)";
                case Lamp.PerfectTrack: return "PT (PERFECT TRACK)";
                case Lamp.FullTrack: return "FT (FULL TRACK)";
                default: return "CLEAR";
            }
        }
    }
}
