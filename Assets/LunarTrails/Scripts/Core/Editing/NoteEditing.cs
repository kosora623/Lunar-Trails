using System;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Editing
{
    /// <summary>譜面エディタで、ノーツを作る・動かすときの計算。</summary>
    public static class NoteEditing
    {
        /// <summary>
        /// ノーツの頭を、拍 beat・列 column に動かす。ノーツの種類に合わない列（キーのノーツをマウスレーンへ、など）なら動かさない。
        /// トレイルは全体を平行に動かす。
        /// </summary>
        /// <param name="grabOffsetX">マウスのノーツで、つかんだ位置とノーツの中心との差（つかんだ場所がずれないように）</param>
        /// <returns>動かせたら true</returns>
        public static bool MoveTo(NoteData n, double beat, NotePicker.Column column, float grabOffsetX = 0f)
        {
            beat = Math.Max(0, beat);
            switch (n.type)
            {
                case "tap":
                case "long":
                case "guide":
                {
                    if (column.IsMouse) return false;
                    int width = n.width <= 0 ? 1 : n.width;
                    n.lane = Math.Max(0, Math.Min(Chart.KeyLaneCount - width, column.KeyLane));
                    n.beat = beat;
                    return true;
                }
                case "click":
                    if (!column.IsMouse) return false;
                    n.beat = beat;
                    return true;
                case "track":
                case "flick":
                    if (!column.IsMouse) return false;
                    n.x = EditorGrid.SnapCenter(column.MouseX - grabOffsetX, n.size);
                    n.beat = beat;
                    return true;
                case "trail":
                {
                    if (!column.IsMouse || n.points == null || n.points.Length == 0) return false;
                    // 始点の位置を動かし、ほかの点も同じだけずらす（レーンからはみ出さないようにずらす量を抑える）
                    float target = EditorGrid.SnapX(column.MouseX - grabOffsetX);
                    float delta = target - n.points[0].x;
                    float min = float.MaxValue, max = float.MinValue;
                    foreach (var p in n.points)
                    {
                        min = Math.Min(min, p.x);
                        max = Math.Max(max, p.x);
                    }
                    delta = Math.Max(-min, Math.Min(1f - max, delta));
                    foreach (var p in n.points) p.x += delta;
                    n.beat = beat;
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>ノーツの横位置（マウスのノーツの中心。トレイルは始点）。キーのノーツは 0。</summary>
        public static float HeadX(NoteData n)
        {
            if (n.type == "trail") return n.points != null && n.points.Length > 0 ? n.points[0].x : 0f;
            return n.x;
        }

        /// <summary>
        /// トレイルに通過点を足す（始点からの拍 rel）。同じ拍の点があれば置き換える。
        /// </summary>
        public static void AddTrailPoint(NoteData trail, double rel, float x, bool smooth)
        {
            var list = new System.Collections.Generic.List<TrailPointData>(trail.points ?? Array.Empty<TrailPointData>());
            list.RemoveAll(p => Math.Abs(p.beat - rel) < 1e-6);
            list.Add(new TrailPointData { beat = rel, x = EditorGrid.SnapX(x), curve = smooth ? "smooth" : "linear" });
            list.Sort((a, b) => a.beat.CompareTo(b.beat));
            trail.points = list.ToArray();
        }

        /// <summary>
        /// ノーツの幅を変える（キーのノーツは 1〜6 レーン、マウスのノーツは 1/16〜1）。
        /// </summary>
        public static void Resize(NoteData n, int step)
        {
            switch (n.type)
            {
                case "tap":
                case "long":
                case "guide":
                {
                    int width = n.width <= 0 ? 1 : n.width;
                    width = Math.Max(1, Math.Min(Chart.KeyLaneCount - n.lane, width + step));
                    n.width = width;
                    break;
                }
                case "track":
                case "flick":
                case "trail":
                {
                    float unit = 1f / EditorGrid.MouseColumns;
                    n.size = Math.Max(unit, Math.Min(1f, (float)Math.Round(n.size / unit + step) * unit));
                    if (n.type != "trail") n.x = EditorGrid.SnapCenter(n.x, n.size);
                    break;
                }
            }
        }
    }
}
