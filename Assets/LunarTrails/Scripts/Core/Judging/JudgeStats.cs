using System.Collections.Generic;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// 判定の集計：判定ごとの数、コンボ、最近のずれの平均。
    /// 最近のずれの平均は、オフセット調整の目安に使う（平均が +20ms なら、いつも 20ms 遅れて叩いている）。
    /// </summary>
    public sealed class JudgeStats
    {
        public const int RecentOffsetCount = 32;

        private readonly int[] _counts = new int[4];
        private readonly Queue<double> _recentOffsets = new Queue<double>();
        private double _recentOffsetSum;

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Total { get; private set; }

        public int Count(Judgement judgement) => _counts[(int)judgement];

        /// <summary>最近 RecentOffsetCount 回のずれの平均（秒）。まだ1回も取っていなければ null。</summary>
        public double? RecentAverageOffset =>
            _recentOffsets.Count == 0 ? (double?)null : _recentOffsetSum / _recentOffsets.Count;

        public void Add(JudgeResult result)
        {
            _counts[(int)result.Judgement]++;
            Total++;

            if (result.Judgement == Judgement.Miss)
            {
                Combo = 0;
            }
            else
            {
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
            }

            if (result.OffsetSeconds.HasValue)
            {
                _recentOffsets.Enqueue(result.OffsetSeconds.Value);
                _recentOffsetSum += result.OffsetSeconds.Value;
                if (_recentOffsets.Count > RecentOffsetCount)
                    _recentOffsetSum -= _recentOffsets.Dequeue();
            }
        }
    }
}
