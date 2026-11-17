using System.Collections.Generic;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// オートプレイ用：キーレーンのノーツを、ちょうどの時刻に押したことにする入力を作る。
    /// 譜面と曲がずれていないかを、叩かずに確かめるために使う。
    /// </summary>
    public sealed class AutoPlay
    {
        /// <summary>オートプレイが押すキー（レーンと時刻）</summary>
        public readonly struct Press
        {
            public readonly int Lane;
            public readonly double Time;

            public Press(int lane, double time)
            {
                Lane = lane;
                Time = time;
            }
        }

        private readonly List<Note> _targets = new List<Note>();
        private int _next;

        public AutoPlay(Chart chart)
        {
            foreach (var note in chart.Notes)
            {
                // KeyLaneJudge が判定するノーツと同じ種類だけを押す
                if (note.Type == NoteType.Tap || note.Type == NoteType.Long)
                    _targets.Add(note);
            }
        }

        /// <summary>前回呼んでから songTime までに押すべきキーを、時刻順に返す。毎フレーム呼ぶ。</summary>
        public List<Press> Collect(double songTime)
        {
            var presses = new List<Press>();
            while (_next < _targets.Count && _targets[_next].Time <= songTime)
            {
                var note = _targets[_next++];
                presses.Add(new Press(note.Lane, note.Time));
            }
            return presses;
        }
    }
}
