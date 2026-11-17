using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Judging
{
    /// <summary>ノーツ1個の判定結果。</summary>
    public readonly struct JudgeResult
    {
        public readonly Note Note;
        public readonly Judgement Judgement;
        /// <summary>入力時刻 − ノーツの時刻（秒）。負なら早押し。見逃しの MISS では null。</summary>
        public readonly double? OffsetSeconds;

        public JudgeResult(Note note, Judgement judgement, double? offsetSeconds)
        {
            Note = note;
            Judgement = judgement;
            OffsetSeconds = offsetSeconds;
        }

        public override string ToString()
        {
            var offset = OffsetSeconds.HasValue ? $" ({OffsetSeconds.Value * 1000:+0;-0}ms)" : "";
            return $"{Judgement}{offset} {Note}";
        }
    }
}
