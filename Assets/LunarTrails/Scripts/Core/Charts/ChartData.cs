using System;

namespace LunarTrails.Core.Charts
{
    // 譜面 JSON をそのまま写した「読み込み用の箱」。
    // フィールド名は JSON のキーと一致させること（Unity の JsonUtility がフィールド名で読むため）。
    // ゲーム中はこれを直接使わず、ChartBuilder で Chart（秒に変換済み）にしてから使う。
    //
    // 形式の説明は docs/chart-format.md を参照。

    [Serializable]
    public class ChartData
    {
        public int version;
        public string song;
        public string difficulty;
        public float level;
        public double offsetMs;
        public BpmChangeData[] bpm;
        public NoteData[] notes;
    }

    [Serializable]
    public class BpmChangeData
    {
        public double beat;
        public double value;
    }

    /// <summary>
    /// ノーツ1個分。種類によって使う項目が違うので、使わない項目は 0 や空のままになる。
    /// </summary>
    [Serializable]
    public class NoteData
    {
        /// <summary>tap / long / guide / click / track / flick / trail</summary>
        public string type;
        public double beat;

        // キーレーン用（tap, long, guide）
        public int lane;
        public int width;
        public double length;

        // マウスレーン用（track, flick, trail）
        public float x;
        public float size;

        // トレイル用
        public bool endFlick;
        public TrailPointData[] points;
    }

    [Serializable]
    public class TrailPointData
    {
        /// <summary>トレイルの始点からの拍</summary>
        public double beat;
        public float x;
        /// <summary>前の点からのつなぎ方。linear / smooth。省略時は linear。</summary>
        public string curve;
    }
}
