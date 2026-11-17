using System;
using System.Collections.Generic;
using System.Linq;
using LunarTrails.Core.Timing;

namespace LunarTrails.Core.Charts
{
    /// <summary>
    /// 読み込んだ ChartData を検証し、拍を秒に直して Chart を作る。
    /// JSON の読み込み自体は Unity 側（Runtime/Charts/ChartLoader）で行う。
    /// </summary>
    public static class ChartBuilder
    {
        public const int SupportedVersion = 1;

        public static Chart Build(ChartData data)
        {
            if (data == null) throw new ChartFormatException("譜面データが空です。");
            if (data.version != SupportedVersion)
                throw new ChartFormatException($"譜面の version {data.version} には対応していません（対応: {SupportedVersion}）。");
            if (data.bpm == null || data.bpm.Length == 0)
                throw new ChartFormatException("bpm が指定されていません。");

            TempoMap tempo;
            try
            {
                tempo = new TempoMap(data.bpm.Select(b => (b.beat, b.value)));
            }
            catch (ArgumentException e)
            {
                throw new ChartFormatException(e.Message);
            }

            double offset = data.offsetMs / 1000.0;
            double ToTime(double beat) => offset + tempo.BeatToSeconds(beat);

            var notes = new List<Note>();
            var source = data.notes ?? Array.Empty<NoteData>();
            for (int i = 0; i < source.Length; i++)
            {
                notes.Add(BuildNote(source[i], i, ToTime));
            }

            // 同じ時刻なら譜面に書かれた順を保つ（List.Sort は安定ではないので OrderBy を使う）
            var sorted = notes.OrderBy(n => n.Time).ToList();
            return new Chart(data.song, data.difficulty, data.level, tempo, offset, sorted);
        }

        private static Note BuildNote(NoteData d, int index, Func<double, double> toTime)
        {
            var type = ParseType(d.type, index);
            var note = new Note
            {
                Index = index,
                Type = type,
                Beat = d.beat,
                Time = toTime(d.beat),
                Lane = d.lane,
                Width = d.width <= 0 ? 1 : d.width, // 省略時は 1 レーン幅
                X = d.x,
                Size = d.size,
                EndFlick = d.endFlick,
            };
            note.EndTime = note.Time;

            if (note.IsKeyLaneNote)
            {
                if (note.Lane < 0 || note.Lane + note.Width > Chart.KeyLaneCount)
                    throw new ChartFormatException($"notes[{index}]: lane {note.Lane} と width {note.Width} がキーレーン（0〜5）からはみ出しています。");
            }

            if (type == NoteType.Long)
            {
                if (d.length <= 0)
                    throw new ChartFormatException($"notes[{index}]: ロングの length は正の数にしてください。");
                note.EndTime = toTime(d.beat + d.length);
            }

            if (type == NoteType.Trail)
            {
                if (d.points == null || d.points.Length < 2)
                    throw new ChartFormatException($"notes[{index}]: トレイルには points が 2 つ以上必要です。");

                note.TrailPoints = d.points
                    .Select(p => new TrailPoint(toTime(d.beat + p.beat), p.x, ParseCurve(p.curve, index)))
                    .ToArray();
                note.EndTime = note.TrailPoints[note.TrailPoints.Count - 1].Time;
            }

            return note;
        }

        private static NoteType ParseType(string type, int index)
        {
            switch (type)
            {
                case "tap": return NoteType.Tap;
                case "long": return NoteType.Long;
                case "guide": return NoteType.Guide;
                case "click": return NoteType.Click;
                case "track": return NoteType.Track;
                case "flick": return NoteType.Flick;
                case "trail": return NoteType.Trail;
                default: throw new ChartFormatException($"notes[{index}]: 知らないノーツの種類です: \"{type}\"");
            }
        }

        private static CurveType ParseCurve(string curve, int index)
        {
            switch (curve)
            {
                case null:
                case "":
                case "linear": return CurveType.Linear;
                case "smooth": return CurveType.Smooth;
                default: throw new ChartFormatException($"notes[{index}]: 知らない curve です: \"{curve}\"");
            }
        }
    }

    /// <summary>譜面ファイルの中身がおかしいときに投げる例外。メッセージはそのまま画面やログに出せる文にする。</summary>
    public sealed class ChartFormatException : Exception
    {
        public ChartFormatException(string message) : base(message) { }
    }
}
