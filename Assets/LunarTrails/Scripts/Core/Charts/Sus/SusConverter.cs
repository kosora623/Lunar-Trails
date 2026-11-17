using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LunarTrails.Core.Charts.Sus
{
    /// <summary>
    /// SUS の中身を Lunar Trails の譜面（ChartData）に置き換える。置き換え方は docs/chart-format.md の「Ched で作る」を参照。
    /// </summary>
    /// <remarks>
    /// Ched の 16 レーンを、ノーツの種類で 6 キーとマウスに分ける。
    /// - キー：TAP と HOLD。Ched のレーン 2〜13 を 2 レーンずつ 6 キーにする（0, 1, 14, 15 には置けない）
    /// - マウス：ExTAP（クリック）、フリック、ダメージ（トラック）、SLIDE（トレイル）。
    ///   横位置は 16 レーン全体を 0〜1 にしたもの。SLIDE の終点に AIR を付けると、終点フリックになる
    /// </remarks>
    public static class SusConverter
    {
        public const int LaneCount = 16;
        public const int KeyAreaStart = 2;   // キーのノーツを置ける最初のレーン
        public const int KeyAreaEnd = 14;    // キーのノーツを置ける範囲の終わり（このレーンは含まない）
        public const int LanesPerKey = 2;

        /// <summary>#DIFFICULTY が数字のときの難易度の略称（0 から順に）</summary>
        public static readonly string[] DifficultyNames = { "stl", "ast", "lnr", "nov", "inv" };

        // TAP チャンネルの種類
        private const int TapNormal = 1;
        private const int TapEx = 2;
        private const int TapFlick = 3;
        private const int TapDamage = 4;

        // HOLD / SLIDE の点の種類
        private const int PointStart = 1;
        private const int PointEnd = 2;
        private const int PointStep = 3;
        private const int PointControl = 4;     // SLIDE の曲がり具合を決める点（通らない）
        private const int PointInvisible = 5;   // 見えない中継点

        private const double SameBeat = 1e-6;

        /// <summary>置き換えた結果。読み飛ばしたものは Warnings に入る。</summary>
        public sealed class Result
        {
            public ChartData Data;
            public readonly List<string> Warnings = new List<string>();
        }

        /// <exception cref="ChartFormatException">置き換えられないノーツがあるとき</exception>
        public static Result Convert(SusScore score)
        {
            var result = new Result();
            var notes = new List<NoteData>();
            var airs = score.Events.Where(e => e.Channel == SusChannel.Air).ToList();
            var usedAirs = new HashSet<int>();

            foreach (var e in score.Events.Where(e => e.Channel == SusChannel.Tap))
            {
                switch (e.Type)
                {
                    case TapNormal:
                        var (lane, width) = ToKeyLane(e);
                        notes.Add(new NoteData { type = "tap", beat = e.Beat, lane = lane, width = width });
                        break;
                    case TapEx:
                        notes.Add(new NoteData { type = "click", beat = e.Beat });
                        break;
                    case TapFlick:
                        notes.Add(new NoteData { type = "flick", beat = e.Beat, x = CenterX(e), size = Size(e) });
                        break;
                    case TapDamage:
                        notes.Add(new NoteData { type = "track", beat = e.Beat, x = CenterX(e), size = Size(e) });
                        break;
                    default:
                        result.Warnings.Add($"小節 {e.Measure}: 種類 {e.Type} の TAP は使わないので読み飛ばしました。");
                        break;
                }
            }

            foreach (var path in GroupPaths(score, SusChannel.Hold))
            {
                var start = path[0];
                var end = path[path.Count - 1];
                var (lane, width) = ToKeyLane(start);
                notes.Add(new NoteData { type = "long", beat = start.Beat, lane = lane, width = width, length = end.Beat - start.Beat });
            }

            foreach (var path in GroupPaths(score, SusChannel.Slide))
            {
                var start = path[0];
                var end = path[path.Count - 1];
                var points = new List<TrailPointData>();
                bool curved = false;
                foreach (var p in path)
                {
                    if (p.Type == PointControl)
                    {
                        curved = true; // 次の点までをなめらかにつなぐ
                        continue;
                    }
                    points.Add(new TrailPointData { beat = p.Beat - start.Beat, x = CenterX(p), curve = curved ? "smooth" : "linear" });
                    curved = false;
                }

                int air = airs.FindIndex(a => Math.Abs(a.Beat - end.Beat) < SameBeat && a.Lane == end.Lane);
                if (air >= 0) usedAirs.Add(air);
                notes.Add(new NoteData { type = "trail", beat = start.Beat, size = Size(start), endFlick = air >= 0, points = points.ToArray() });
            }

            for (int i = 0; i < airs.Count; i++)
            {
                if (!usedAirs.Contains(i))
                    result.Warnings.Add($"小節 {airs[i].Measure}: SLIDE の終点以外の AIR は使わないので読み飛ばしました。");
            }
            if (score.Events.Any(e => e.Channel == SusChannel.AirHold))
                result.Warnings.Add("AIR ACTION は使わないので読み飛ばしました。");

            if (score.BpmChanges.Count == 0)
                throw new ChartFormatException("BPM が指定されていません（#BPM01: 120 と #00008: 01 のような行が必要です）。");

            // 最初の BPM は 0 拍目に置く（Ched は 0 拍目に書くが、念のため）
            var bpm = score.BpmChanges.Select(b => new BpmChangeData { beat = b.Beat, value = b.Bpm }).ToList();
            bpm[0].beat = 0;

            result.Data = new ChartData
            {
                version = ChartBuilder.SupportedVersion,
                song = score.GetHeader("SONGID") ?? score.GetHeader("TITLE") ?? "",
                difficulty = Difficulty(score.GetHeader("DIFFICULTY")),
                level = Level(score.GetHeader("PLAYLEVEL")),
                offsetMs = WaveOffsetMs(score.GetHeader("WAVEOFFSET")),
                bpm = bpm.ToArray(),
                notes = notes.OrderBy(n => n.beat).ToArray(),
            };
            return result;
        }

        /// <summary>SUS のテキストを読んで、そのまま Chart にする。</summary>
        public static Chart ToChart(string susText, List<string> warnings = null)
        {
            var result = Convert(SusParser.Parse(susText));
            warnings?.AddRange(result.Warnings);
            return ChartBuilder.Build(result.Data);
        }

        // ===== 位置 =====

        // Ched のレーン（2〜13）を、キーレーン（0〜5）と幅に直す
        private static (int Lane, int Width) ToKeyLane(SusEvent e)
        {
            int right = e.Lane + e.Width; // 含まない
            if (e.Lane < KeyAreaStart || right > KeyAreaEnd)
                throw new ChartFormatException(
                    $"小節 {e.Measure}: キーのノーツ（TAP / HOLD）はレーン {KeyAreaStart}〜{KeyAreaEnd - 1} に置いてください（今はレーン {e.Lane}〜{right - 1}）。");
            int first = (e.Lane - KeyAreaStart) / LanesPerKey;
            int last = (right - 1 - KeyAreaStart) / LanesPerKey;
            return (first, last - first + 1);
        }

        private static float CenterX(SusEvent e) => (e.Lane + e.Width / 2f) / LaneCount;

        private static float Size(SusEvent e) => (float)e.Width / LaneCount;

        // HOLD / SLIDE の点を、つなぐ記号ごとに始点〜終点の並びにまとめる
        private static IEnumerable<List<SusEvent>> GroupPaths(SusScore score, SusChannel channel)
        {
            string name = channel == SusChannel.Hold ? "HOLD" : "SLIDE";
            foreach (var group in score.Events.Where(e => e.Channel == channel).GroupBy(e => e.Id))
            {
                List<SusEvent> current = null;
                // 同じ拍に終点と始点が並んだら、終点を先に扱う
                foreach (var e in group.OrderBy(e => e.Beat).ThenBy(e => e.Type == PointEnd ? 0 : 1))
                {
                    switch (e.Type)
                    {
                        case PointStart:
                            if (current != null)
                                throw new ChartFormatException($"小節 {e.Measure}: {name}（記号 {e.Id}）が終わる前に、次の {name} が始まっています。");
                            current = new List<SusEvent> { e };
                            break;
                        case PointEnd:
                            if (current == null)
                                throw new ChartFormatException($"小節 {e.Measure}: {name}（記号 {e.Id}）の終点に対応する始点がありません。");
                            current.Add(e);
                            yield return current;
                            current = null;
                            break;
                        case PointStep:
                        case PointControl:
                        case PointInvisible:
                            if (current == null)
                                throw new ChartFormatException($"小節 {e.Measure}: {name}（記号 {e.Id}）の中継点が始点の前にあります。");
                            if (channel == SusChannel.Slide) current.Add(e);
                            break;
                        default:
                            throw new ChartFormatException($"小節 {e.Measure}: {name} の点の種類 {e.Type} が分かりません。");
                    }
                }
                if (current != null)
                    throw new ChartFormatException($"小節 {current[0].Measure}: {name}（記号 {group.Key}）に終点がありません。");
            }
        }

        // ===== 見出し =====

        private static string Difficulty(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && index < DifficultyNames.Length)
                return DifficultyNames[index];
            return value;
        }

        // "12" → 12、"12+" → 12.5、"12.7" → 12.7
        private static float Level(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            bool plus = value.EndsWith("+");
            string number = plus ? value.Substring(0, value.Length - 1) : value;
            if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float level)) return 0;
            return plus ? level + 0.5f : level;
        }

        // #WAVEOFFSET は秒。音声の頭から 1 拍目までの時間として扱う
        private static double WaveOffsetMs(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ? seconds * 1000 : 0;
        }
    }
}
