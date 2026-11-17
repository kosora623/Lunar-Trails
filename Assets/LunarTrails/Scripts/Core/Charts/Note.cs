using System.Collections.Generic;

namespace LunarTrails.Core.Charts
{
    public enum NoteType
    {
        // キーレーン
        Tap,
        Long,
        Guide,

        // マウスレーン
        Click,
        Track,
        Flick,
        Trail,
    }

    public enum CurveType
    {
        Linear,
        Smooth,
    }

    /// <summary>トレイルの通過点。時刻は秒に変換済み。</summary>
    public readonly struct TrailPoint
    {
        public readonly double Time;
        public readonly float X;
        public readonly CurveType Curve;

        public TrailPoint(double time, float x, CurveType curve)
        {
            Time = time;
            X = x;
            Curve = curve;
        }
    }

    /// <summary>
    /// ゲーム中に使うノーツ1個分。時刻はすべて「曲の再生位置（秒）」に変換済み。
    /// </summary>
    public sealed class Note
    {
        /// <summary>譜面の中での通し番号（読み込み順）。判定結果やデバッグ表示の照合に使う。</summary>
        public int Index;
        public NoteType Type;

        /// <summary>判定ラインに届く時刻（秒）</summary>
        public double Time;
        /// <summary>元の拍の位置。エディタやデバッグ表示用。</summary>
        public double Beat;

        /// <summary>キーレーン番号（左から 0〜5）</summary>
        public int Lane;
        /// <summary>何レーン分の幅か（1 以上）</summary>
        public int Width = 1;
        /// <summary>ロングの終わりの時刻（秒）。ロング以外は Time と同じ。</summary>
        public double EndTime;

        /// <summary>マウスレーン内の横位置（0 = 左端、1 = 右端）</summary>
        public float X;
        /// <summary>マウスレーン内の幅（0〜1）</summary>
        public float Size;

        public bool EndFlick;
        public IReadOnlyList<TrailPoint> TrailPoints = System.Array.Empty<TrailPoint>();

        public bool IsKeyLaneNote => Type == NoteType.Tap || Type == NoteType.Long || Type == NoteType.Guide;
        public bool IsMouseLaneNote => !IsKeyLaneNote;

        /// <summary>このノーツが指定したキーレーンにかかっているか（幅のあるノーツ用）</summary>
        public bool CoversLane(int lane) => IsKeyLaneNote && lane >= Lane && lane < Lane + Width;

        public override string ToString() => $"#{Index} {Type} beat={Beat} t={Time:F3}s";
    }
}
