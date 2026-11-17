using System;
using System.Collections.Generic;

namespace LunarTrails.Core.Charts
{
    /// <summary>
    /// トレイルの軌道：ある時刻にトレイルがマウスレーンのどこを通っているかを求める。
    /// 判定と表示の両方がこれを使うので、見た目と判定がずれない。
    /// </summary>
    /// <remarks>
    /// 通過点どうしのつなぎ方は、後ろ側の点の curve で決まる。
    /// - linear：直線
    /// - smooth：前後の点も見て、なめらかな曲線（Catmull-Rom 曲線）でつなぐ。必ず通過点を通る。
    /// 曲線がレーンの外にはみ出す場合は、端（0 か 1）で止める。
    /// </remarks>
    public static class TrailPath
    {
        /// <summary>トレイルの、指定した時刻での横位置（0 = 左端、1 = 右端）。始点より前は始点、終点より後は終点の位置。</summary>
        public static float XAt(Note trail, double time) => XAt(trail.TrailPoints, time);

        public static float XAt(IReadOnlyList<TrailPoint> points, double time)
        {
            if (points == null || points.Count == 0) return 0.5f;
            if (time <= points[0].Time) return points[0].X;
            if (time >= points[points.Count - 1].Time) return points[points.Count - 1].X;

            int i = 1;
            while (points[i].Time < time) i++;

            var a = points[i - 1];
            var b = points[i];
            double span = b.Time - a.Time;
            double s = span <= 0 ? 1.0 : (time - a.Time) / span;

            if (b.Curve == CurveType.Linear)
                return (float)(a.X + (b.X - a.X) * s);

            // Hermite 補間。接線は前後の点から求める（端の点は隣の点との直線の傾き）。
            var prev = i >= 2 ? points[i - 2] : a;
            var next = i + 1 < points.Count ? points[i + 1] : b;
            double m0 = Tangent(prev, a, b) * span;
            double m1 = Tangent(a, b, next) * span;

            double s2 = s * s, s3 = s2 * s;
            double x = (2 * s3 - 3 * s2 + 1) * a.X
                     + (s3 - 2 * s2 + s) * m0
                     + (-2 * s3 + 3 * s2) * b.X
                     + (s3 - s2) * m1;
            return (float)Math.Max(0.0, Math.Min(1.0, x));
        }

        // 点 p での傾き（1 秒あたりの x の変化）。前後の点を結んだ直線の傾きを使う。
        private static double Tangent(TrailPoint before, TrailPoint p, TrailPoint after)
        {
            double dt = after.Time - before.Time;
            return dt <= 0 ? 0 : (after.X - before.X) / dt;
        }
    }
}
