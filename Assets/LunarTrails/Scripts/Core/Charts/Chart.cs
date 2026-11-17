using System.Collections.Generic;
using LunarTrails.Core.Timing;

namespace LunarTrails.Core.Charts
{
    /// <summary>
    /// ゲーム中に使う譜面。ノーツは時刻順に並んでいる。
    /// </summary>
    public sealed class Chart
    {
        public const int KeyLaneCount = 6;

        public string Song { get; }
        public string Difficulty { get; }
        public float Level { get; }
        public TempoMap Tempo { get; }
        /// <summary>音声ファイルの頭から 1 拍目までの秒数</summary>
        public double OffsetSeconds { get; }
        public IReadOnlyList<Note> Notes { get; }

        public Chart(string song, string difficulty, float level, TempoMap tempo, double offsetSeconds, IReadOnlyList<Note> notes)
        {
            Song = song;
            Difficulty = difficulty;
            Level = level;
            Tempo = tempo;
            OffsetSeconds = offsetSeconds;
            Notes = notes;
        }
    }
}
