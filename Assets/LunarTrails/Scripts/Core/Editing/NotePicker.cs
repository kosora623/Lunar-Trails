using System;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Editing
{
    /// <summary>
    /// 譜面エディタで、マウスの下にあるノーツを探す。位置は「拍」と「列」で渡す。
    /// </summary>
    public static class NotePicker
    {
        /// <summary>エディタの画面上の列</summary>
        public readonly struct Column
        {
            /// <summary>キーレーン（0〜5）。マウスレーンなら -1。</summary>
            public readonly int KeyLane;
            /// <summary>マウスレーン内の位置（0〜1）。キーレーンなら使わない。</summary>
            public readonly float MouseX;

            private Column(int keyLane, float mouseX)
            {
                KeyLane = keyLane;
                MouseX = mouseX;
            }

            public bool IsMouse => KeyLane < 0;
            public static Column Key(int lane) => new Column(lane, 0);
            public static Column Mouse(float x) => new Column(-1, x);
        }

        /// <summary>
        /// 拍 beat・列 column にあるノーツの番号（ChartDocument.Notes での位置）を返す。なければ -1。
        /// </summary>
        /// <param name="beatTolerance">頭の前後この拍以内なら当たりとみなす</param>
        public static int Find(ChartDocument doc, double beat, Column column, double beatTolerance)
        {
            var notes = doc.Notes;
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < notes.Count; i++)
            {
                double distance = Distance(notes[i], beat, column, beatTolerance);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        // 当たっていれば頭からの拍の距離、当たっていなければ MaxValue
        private static double Distance(NoteData n, double beat, Column column, double tolerance)
        {
            double fromHead = Math.Abs(beat - n.beat);
            switch (n.type)
            {
                case "tap":
                case "guide":
                case "long":
                {
                    if (column.IsMouse) return double.MaxValue;
                    int width = n.width <= 0 ? 1 : n.width;
                    if (column.KeyLane < n.lane || column.KeyLane >= n.lane + width) return double.MaxValue;
                    if (fromHead <= tolerance) return fromHead;
                    // ロングは帯の上でも当たり
                    if (n.type == "long" && beat >= n.beat && beat <= n.beat + n.length) return tolerance;
                    return double.MaxValue;
                }
                case "click":
                    return column.IsMouse && fromHead <= tolerance ? fromHead : double.MaxValue;
                case "track":
                case "flick":
                    if (!column.IsMouse || fromHead > tolerance) return double.MaxValue;
                    return Math.Abs(column.MouseX - n.x) <= Math.Max(n.size, 1f / EditorGrid.MouseColumns) / 2f ? fromHead : double.MaxValue;
                case "trail":
                {
                    if (!column.IsMouse || n.points == null || n.points.Length < 2) return double.MaxValue;
                    double rel = beat - n.beat;
                    if (rel < -tolerance || rel > n.points[n.points.Length - 1].beat + tolerance) return double.MaxValue;
                    float x = TrailX(n, rel);
                    return Math.Abs(column.MouseX - x) <= Math.Max(n.size, 1f / EditorGrid.MouseColumns) / 2f ? tolerance : double.MaxValue;
                }
                default:
                    return double.MaxValue;
            }
        }

        /// <summary>トレイルの、始点から rel 拍の位置の横位置（当たり判定用に直線でつないだもの）</summary>
        public static float TrailX(NoteData trail, double rel)
        {
            var p = trail.points;
            if (rel <= p[0].beat) return p[0].x;
            for (int i = 1; i < p.Length; i++)
            {
                if (rel <= p[i].beat)
                {
                    double span = p[i].beat - p[i - 1].beat;
                    float t = span <= 0 ? 1f : (float)((rel - p[i - 1].beat) / span);
                    return p[i - 1].x + (p[i].x - p[i - 1].x) * t;
                }
            }
            return p[p.Length - 1].x;
        }
    }
}
