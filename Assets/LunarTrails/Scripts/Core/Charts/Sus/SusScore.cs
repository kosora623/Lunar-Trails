using System.Collections.Generic;

namespace LunarTrails.Core.Charts.Sus
{
    /// <summary>
    /// SUS ファイルを読んだだけの中身。拍の位置への変換までは済ませてあるが、
    /// Lunar Trails のノーツへの置き換えはまだしていない（それは SusConverter が行う）。
    /// </summary>
    public sealed class SusScore
    {
        /// <summary>#TITLE や #PLAYLEVEL などの見出し。キーは大文字（"TITLE"）、値は前後の " を外したもの。</summary>
        public readonly Dictionary<string, string> Header = new Dictionary<string, string>();

        /// <summary>BPM の変化点（拍, BPM）。拍の順に並ぶ。</summary>
        public readonly List<(double Beat, double Bpm)> BpmChanges = new List<(double, double)>();

        /// <summary>ノーツの記号すべて。拍の順に並ぶ。</summary>
        public readonly List<SusEvent> Events = new List<SusEvent>();

        public string GetHeader(string key) => Header.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>SUS のノーツの種類（行の番号の 4 文字目、チャンネル）。</summary>
    public enum SusChannel
    {
        Tap = 1,      // #mmm1L   … TAP / ExTAP / フリック / ダメージ など
        Hold = 2,     // #mmm2LX  … HOLD（X は同じ HOLD をつなぐための記号）
        Slide = 3,    // #mmm3LX  … SLIDE
        AirHold = 4,  // #mmm4LX  … AIR ACTION
        Air = 5,      // #mmm5L   … AIR（上下の矢印）
    }

    /// <summary>SUS の 1 記号分（1 個のノーツ、または HOLD / SLIDE の点 1 つ）。</summary>
    public readonly struct SusEvent
    {
        public readonly double Beat;
        public readonly SusChannel Channel;
        /// <summary>左端のレーン（0〜15）</summary>
        public readonly int Lane;
        /// <summary>幅（1〜16）</summary>
        public readonly int Width;
        /// <summary>記号の 1 文字目の数字。意味はチャンネルごとに違う（SusConverter を参照）。</summary>
        public readonly int Type;
        /// <summary>HOLD / SLIDE をつなぐ記号。TAP と AIR では空。</summary>
        public readonly char Id;
        /// <summary>エラー表示用の小節番号</summary>
        public readonly int Measure;

        public SusEvent(double beat, SusChannel channel, int lane, int width, int type, char id, int measure)
        {
            Beat = beat;
            Channel = channel;
            Lane = lane;
            Width = width;
            Type = type;
            Id = id;
            Measure = measure;
        }
    }
}
