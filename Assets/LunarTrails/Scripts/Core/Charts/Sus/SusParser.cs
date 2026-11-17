using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LunarTrails.Core.Charts.Sus
{
    /// <summary>
    /// SUS 形式（Ched などの譜面エディタが書き出す形式）のテキストを読む。
    /// </summary>
    /// <remarks>
    /// 読むもの：
    /// - 見出し（#TITLE "曲名" など）
    /// - BPM の定義（#BPM01: 120）と、その使用（#mmm08: 01）
    /// - 小節の長さ（#mmm02: 4 … 4 拍。指定した小節から後ろすべてに効く）
    /// - ノーツ（#mmmCL: または #mmmCLX: のあとに 2 文字ずつの記号）
    /// mmm は小節番号（10 進 3 桁）、C はチャンネル、L はレーン（36 進 1 文字）、X はつなぐための記号。
    /// 記号の 2 文字は「種類」と「幅（36 進）」。00 は空き。1 行の記号は小節を等分した位置に並ぶ。
    ///
    /// それ以外の行（#HISPEED、#TIL、コメントなど）は読み飛ばす。
    /// </remarks>
    public static class SusParser
    {
        private const double DefaultMeasureBeats = 4;

        private readonly struct RawLine
        {
            public readonly int Measure;
            public readonly string Channel;  // "02"、"08"、"1a"、"3a0" など、小節番号のあとから : の前まで
            public readonly string Data;     // 空白を除いた記号の並び
            public readonly int LineNumber;

            public RawLine(int measure, string channel, string data, int lineNumber)
            {
                Measure = measure;
                Channel = channel;
                Data = data;
                LineNumber = lineNumber;
            }
        }

        /// <exception cref="ChartFormatException">SUS として読めないとき</exception>
        public static SusScore Parse(string text)
        {
            if (text == null) throw new ChartFormatException("SUS の中身が空です。");

            var score = new SusScore();
            var bpmDefinitions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var measureLengths = new SortedDictionary<int, double>();
            var lines = new List<RawLine>();

            var rows = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i].Trim();
                int lineNumber = i + 1;
                if (row.Length < 2 || row[0] != '#') continue; // # で始まらない行はコメント

                if (char.IsDigit(row[1]))
                {
                    ReadDataLine(row, lineNumber, lines);
                }
                else
                {
                    ReadHeaderLine(row, lineNumber, score, bpmDefinitions);
                }
            }

            // 小節の長さ
            foreach (var line in lines.Where(l => l.Channel == "02"))
            {
                if (!double.TryParse(line.Data, NumberStyles.Float, CultureInfo.InvariantCulture, out double beats) || beats <= 0)
                    throw Error(line.LineNumber, $"小節の長さ「{line.Data}」が読めません。");
                measureLengths[line.Measure] = beats;
            }
            var measures = new MeasureMap(measureLengths);

            // BPM の変化点
            foreach (var line in lines.Where(l => l.Channel == "08"))
            {
                foreach (var (index, count, symbol) in Symbols(line))
                {
                    if (!bpmDefinitions.TryGetValue(symbol, out double bpm))
                        throw Error(line.LineNumber, $"BPM{symbol} が定義されていません。");
                    score.BpmChanges.Add((measures.BeatAt(line.Measure, index, count), bpm));
                }
            }
            score.BpmChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));

            // ノーツ
            foreach (var line in lines)
            {
                if (line.Channel.Length < 2 || line.Channel[0] == '0') continue;
                if (!TryChannel(line.Channel[0], out var channel)) continue; // 知らないチャンネル（ガイドなど）は読み飛ばす
                int lane = Base36(line.Channel[1]);
                if (lane < 0) throw Error(line.LineNumber, $"レーン「{line.Channel[1]}」が読めません。");

                bool needsId = channel == SusChannel.Hold || channel == SusChannel.Slide || channel == SusChannel.AirHold;
                if (needsId && line.Channel.Length < 3)
                    throw Error(line.LineNumber, "HOLD / SLIDE の行に、つなぐための記号がありません。");
                char id = needsId ? char.ToLowerInvariant(line.Channel[2]) : '\0';

                foreach (var (index, count, symbol) in Symbols(line))
                {
                    int type = Base36(symbol[0]);
                    int width = Base36(symbol[1]);
                    if (type < 0 || width <= 0)
                        throw Error(line.LineNumber, $"ノーツの記号「{symbol}」が読めません。");
                    score.Events.Add(new SusEvent(measures.BeatAt(line.Measure, index, count), channel, lane, width, type, id, line.Measure));
                }
            }

            // 同じ拍なら書かれた順を保つ
            var sorted = score.Events.OrderBy(e => e.Beat).ToList();
            score.Events.Clear();
            score.Events.AddRange(sorted);
            return score;
        }

        // #TITLE "曲名"、#BPM01: 120 など
        private static void ReadHeaderLine(string row, int lineNumber, SusScore score, Dictionary<string, double> bpmDefinitions)
        {
            if (row.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && row.Contains(':'))
            {
                int colon = row.IndexOf(':');
                string id = row.Substring(4, colon - 4).Trim();
                string value = row.Substring(colon + 1).Trim();
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm) || bpm <= 0)
                    throw Error(lineNumber, $"BPM の値「{value}」が読めません。");
                bpmDefinitions[id] = bpm;
                return;
            }

            int space = row.IndexOfAny(new[] { ' ', '\t' });
            if (space < 0) return;
            string key = row.Substring(1, space - 1).ToUpperInvariant();
            string text = row.Substring(space + 1).Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"') text = text.Substring(1, text.Length - 2);
            score.Header[key] = text;
        }

        // #mmmCL: 記号 …
        private static void ReadDataLine(string row, int lineNumber, List<RawLine> lines)
        {
            int colon = row.IndexOf(':');
            if (colon < 0 || colon < 4) return;
            string head = row.Substring(1, colon - 1).Trim();
            if (head.Length < 5) return;
            if (!int.TryParse(head.Substring(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int measure)) return;

            string channel = head.Substring(3).ToLowerInvariant();
            string data = new string(row.Substring(colon + 1).Where(c => !char.IsWhiteSpace(c)).ToArray());
            lines.Add(new RawLine(measure, channel, data, lineNumber));
        }

        // 1 行の記号を 2 文字ずつ区切る。00 は空きなので返さない
        private static IEnumerable<(int Index, int Count, string Symbol)> Symbols(RawLine line)
        {
            if (line.Data.Length % 2 != 0)
                throw Error(line.LineNumber, "記号の文字数が奇数です（2 文字ずつ並ぶはず）。");
            int count = line.Data.Length / 2;
            for (int i = 0; i < count; i++)
            {
                string symbol = line.Data.Substring(i * 2, 2);
                if (symbol == "00") continue;
                yield return (i, count, symbol);
            }
        }

        private static bool TryChannel(char c, out SusChannel channel)
        {
            switch (c)
            {
                case '1': channel = SusChannel.Tap; return true;
                case '2': channel = SusChannel.Hold; return true;
                case '3': channel = SusChannel.Slide; return true;
                case '4': channel = SusChannel.AirHold; return true;
                case '5': channel = SusChannel.Air; return true;
                default: channel = default; return false;
            }
        }

        /// <summary>36 進 1 文字（0〜9, a〜z）を数にする。読めなければ -1。</summary>
        public static int Base36(char c)
        {
            c = char.ToLowerInvariant(c);
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'z') return c - 'a' + 10;
            return -1;
        }

        private static ChartFormatException Error(int lineNumber, string message) => new ChartFormatException($"{lineNumber} 行目: {message}");

        /// <summary>小節番号と小節内の位置から、曲の頭からの拍を求める。</summary>
        private sealed class MeasureMap
        {
            private readonly SortedDictionary<int, double> _lengths;

            public MeasureMap(SortedDictionary<int, double> lengths) => _lengths = lengths;

            private double LengthOf(int measure)
            {
                double length = DefaultMeasureBeats;
                foreach (var pair in _lengths)
                {
                    if (pair.Key > measure) break;
                    length = pair.Value;
                }
                return length;
            }

            public double BeatAt(int measure, int index, int count)
            {
                double start = 0;
                for (int m = 0; m < measure; m++) start += LengthOf(m);
                return start + LengthOf(measure) * index / count;
            }
        }
    }
}
