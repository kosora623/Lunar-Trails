using System;

namespace LunarTrails.Core.Editing
{
    /// <summary>
    /// 譜面エディタで、ノーツの位置を拍や横位置の目盛りにそろえる計算。
    /// </summary>
    public static class EditorGrid
    {
        /// <summary>選べる拍の細かさ（1 拍を何等分するか）。4 なら 16 分音符、3 なら 3 連符（8 分）。</summary>
        public static readonly int[] Divisions = { 1, 2, 3, 4, 6, 8, 12, 16 };

        /// <summary>マウスレーンの横位置の目盛りの数（Ched と同じ 16 等分）</summary>
        public const int MouseColumns = 16;

        /// <summary>beat を、1 拍を division 等分した目盛りのいちばん近いところにそろえる。</summary>
        public static double SnapBeat(double beat, int division)
        {
            if (division <= 0) return beat;
            return Math.Round(beat * division) / division;
        }

        /// <summary>beat 以下で、いちばん近い目盛り（スクロールの区切りなどに使う）。</summary>
        public static double FloorBeat(double beat, int division)
        {
            if (division <= 0) return beat;
            return Math.Floor(beat * division + 1e-9) / division;
        }

        /// <summary>マウスレーン内の位置（0〜1）を 1/16 の目盛りにそろえる。</summary>
        public static float SnapX(float x)
        {
            float snapped = (float)Math.Round(x * MouseColumns) / MouseColumns;
            return Math.Max(0f, Math.Min(1f, snapped));
        }

        /// <summary>ノーツの中心が目盛りに乗るようにそろえる（幅 size のノーツが端からはみ出さないようにもする）。</summary>
        public static float SnapCenter(float x, float size)
        {
            float half = size / 2f;
            float left = SnapX(x - half);
            left = Math.Max(0f, Math.Min(1f - size, left));
            return left + half;
        }
    }
}
