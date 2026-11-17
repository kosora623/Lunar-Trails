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

        // カーソルを動かす目標の位置（トラック、フリック、トレイルの始点と終点）と、フリックする時刻
        private readonly List<(double time, float x)> _cursorTargets = new List<(double, float)>();
        private readonly List<double> _flickTimes = new List<double>();
        private readonly List<Note> _trails = new List<Note>();

        public AutoPlay(Chart chart)
        {
            foreach (var note in chart.Notes)
            {
                // KeyLaneJudge が判定するノーツと同じ種類だけを押す
                if (note.Type == NoteType.Tap || note.Type == NoteType.Long)
                    _targets.Add(note);
                else if (note.Type == NoteType.Click || note.Type == NoteType.Trail)
                    _clicks.Add(note); // トレイルは始点でクリックする

                if (note.Type == NoteType.Track || note.Type == NoteType.Flick)
                    _cursorTargets.Add((note.Time, note.X));
                if (note.Type == NoteType.Flick)
                    _flickTimes.Add(note.Time);

                if (note.Type == NoteType.Trail)
                {
                    _trails.Add(note);
                    _cursorTargets.Add((note.Time, TrailPath.XAt(note, note.Time)));
                    _cursorTargets.Add((note.EndTime, TrailPath.XAt(note, note.EndTime)));
                    if (note.EndFlick) _flickTimes.Add(note.EndTime);
                }
            }
            _cursorTargets.Sort((a, b) => a.time.CompareTo(b.time));
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
        /// オートプレイのカーソル位置。トレイルの間はトレイルの軌道をなぞり、
        /// それ以外はトラック・フリック・トレイルの端の位置を、時刻に合わせて直線でつないで動く。
        /// </summary>
        public float CursorX(double time)
        {
            foreach (var trail in _trails)
            {
                if (time >= trail.Time && time <= trail.EndTime) return TrailPath.XAt(trail, time);
            }

            if (_cursorTargets.Count == 0) return 0.5f;
            if (time <= _cursorTargets[0].time) return _cursorTargets[0].x;

            for (int i = 1; i < _cursorTargets.Count; i++)
            {
                var b = _cursorTargets[i];
                if (time > b.time) continue;
                var a = _cursorTargets[i - 1];
                double span = b.time - a.time;
                float t = span <= 0 ? 1f : (float)((time - a.time) / span);
                return a.x + (b.x - a.x) * t;
            }
            return _cursorTargets[_cursorTargets.Count - 1].x;
        }

        /// <summary>オートプレイがマウスのボタンを押し続けているか（トレイルの間だけ押す）。</summary>
        public bool IsHolding(double time)
        {
            foreach (var trail in _trails)
            {
                if (time >= trail.Time && time <= trail.EndTime + 0.1) return true;
            }
            return false;
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
