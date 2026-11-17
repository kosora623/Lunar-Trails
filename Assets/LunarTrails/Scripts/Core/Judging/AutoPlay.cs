using System.Collections.Generic;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// オートプレイ用：ノーツをちょうどの時刻に取ったことにする入力を作る。
    /// キーレーンはキーを押す時刻、マウスレーンはクリックの時刻とカーソルの位置・速さを返す。
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

        private readonly List<Note> _clicks = new List<Note>();
        private int _nextClick;

        // カーソルを動かす目標になるノーツ（トラックとフリック）と、フリックだけの一覧
        private readonly List<Note> _cursorTargets = new List<Note>();
        private readonly List<double> _flickTimes = new List<double>();

        public AutoPlay(Chart chart)
        {
            foreach (var note in chart.Notes)
            {
                // KeyLaneJudge が判定するノーツと同じ種類だけを押す
                if (note.Type == NoteType.Tap || note.Type == NoteType.Long)
                    _targets.Add(note);
                else if (note.Type == NoteType.Click)
                    _clicks.Add(note);

                if (note.Type == NoteType.Track || note.Type == NoteType.Flick)
                    _cursorTargets.Add(note);
                if (note.Type == NoteType.Flick)
                    _flickTimes.Add(note.Time);
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

        /// <summary>前回呼んでから songTime までにクリックすべき時刻を返す。毎フレーム呼ぶ。</summary>
        public List<double> CollectClicks(double songTime)
        {
            var clicks = new List<double>();
            while (_nextClick < _clicks.Count && _clicks[_nextClick].Time <= songTime)
                clicks.Add(_clicks[_nextClick++].Time);
            return clicks;
        }

        /// <summary>
        /// オートプレイのカーソル位置。トラックとフリックの位置を、時刻に合わせて直線でつないで動く。
        /// </summary>
        public float CursorX(double time)
        {
            if (_cursorTargets.Count == 0) return 0.5f;
            if (time <= _cursorTargets[0].Time) return _cursorTargets[0].X;

            for (int i = 1; i < _cursorTargets.Count; i++)
            {
                var b = _cursorTargets[i];
                if (time > b.Time) continue;
                var a = _cursorTargets[i - 1];
                double span = b.Time - a.Time;
                float t = span <= 0 ? 1f : (float)((time - a.Time) / span);
                return a.X + (b.X - a.X) * t;
            }
            return _cursorTargets[_cursorTargets.Count - 1].X;
        }

        /// <summary>
        /// オートプレイのカーソルの速さ。前のフレーム（previousTime）からこのフレーム（time）までの間に
        /// フリックの時刻があれば、払ったことにして十分に大きい値を返す。
        /// </summary>
        public float CursorSpeed(double previousTime, double time)
        {
            foreach (var t in _flickTimes)
            {
                if (t > previousTime && t <= time) return float.MaxValue;
            }
            return 0f;
        }
    }
}
