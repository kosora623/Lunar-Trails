using System;
using System.Collections.Generic;

namespace LunarTrails.Core.Timing
{
    /// <summary>
    /// BPM の変化点の一覧から、「拍」と「秒」を相互に変換する。
    /// 譜面はノーツの位置を拍で持つので、再生時にはここで秒に直す。
    /// </summary>
    /// <remarks>
    /// 秒は「1拍目（beat 0）からの経過秒」。音声ファイルの頭とのずれ（offsetMs）は
    /// ここでは扱わず、呼び出し側（ChartBuilder）で足す。
    /// </remarks>
    public sealed class TempoMap
    {
        private readonly struct Segment
        {
            public readonly double StartBeat;
            public readonly double StartSeconds;
            public readonly double Bpm;

            public Segment(double startBeat, double startSeconds, double bpm)
            {
                StartBeat = startBeat;
                StartSeconds = startSeconds;
                Bpm = bpm;
            }

            public double SecondsPerBeat => 60.0 / Bpm;
        }

        private readonly List<Segment> _segments = new List<Segment>();

        /// <param name="changes">(拍の位置, BPM) の組。順番はばらばらでもよい。beat 0 の指定は必須。</param>
        public TempoMap(IEnumerable<(double beat, double bpm)> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            var sorted = new List<(double beat, double bpm)>(changes);
            sorted.Sort((a, b) => a.beat.CompareTo(b.beat));

            if (sorted.Count == 0 || sorted[0].beat > 0)
                throw new ArgumentException("beat 0 の BPM が指定されていません。");

            double seconds = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                var (beat, bpm) = sorted[i];
                if (bpm <= 0) throw new ArgumentException($"BPM は正の数にしてください（beat {beat}: {bpm}）。");

                if (i > 0)
                {
                    var prev = _segments[_segments.Count - 1];
                    seconds = prev.StartSeconds + (beat - prev.StartBeat) * prev.SecondsPerBeat;
                }
                _segments.Add(new Segment(beat, seconds, bpm));
            }
        }

        /// <summary>拍の位置を、1拍目からの秒に変換する。</summary>
        public double BeatToSeconds(double beat)
        {
            var seg = FindSegmentByBeat(beat);
            return seg.StartSeconds + (beat - seg.StartBeat) * seg.SecondsPerBeat;
        }

        /// <summary>1拍目からの秒を、拍の位置に変換する。</summary>
        public double SecondsToBeat(double seconds)
        {
            var seg = _segments[0];
            for (int i = 1; i < _segments.Count && _segments[i].StartSeconds <= seconds; i++)
                seg = _segments[i];
            return seg.StartBeat + (seconds - seg.StartSeconds) / seg.SecondsPerBeat;
        }

        /// <summary>指定した拍の位置での BPM。</summary>
        public double BpmAt(double beat) => FindSegmentByBeat(beat).Bpm;

        private Segment FindSegmentByBeat(double beat)
        {
            var seg = _segments[0];
            for (int i = 1; i < _segments.Count && _segments[i].StartBeat <= beat; i++)
                seg = _segments[i];
            return seg;
        }
    }
}
