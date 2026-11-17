using System;
using System.Collections.Generic;
using LunarTrails.Core.Timing;

namespace LunarTrails.Core.Settings
{
    /// <summary>
    /// プレイヤーが設定画面で変える値。実装計画の「設定（速さ、判定調整、感度、キー配置）」に対応。
    /// </summary>
    /// <remarks>
    /// 保存と読み込みは Runtime の SettingsStore が JSON で行う。フィールド名がそのまま JSON のキーになる。
    /// 保存ファイルを手で書き換えられてもおかしな値にならないよう、読み込んだら必ず Sanitize() を呼ぶ。
    /// </remarks>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinScrollSpeed = 4f;
        public const float MaxScrollSpeed = 40f;
        public const float MinPixelsPerLane = 100f;
        public const float MaxPixelsPerLane = 3000f;

        /// <summary>キーの初期配置（企画書の決定事項 5：S D F と J K L）。Input System の Key の名前で持つ。</summary>
        public static readonly string[] DefaultLaneKeys = { "S", "D", "F", "J", "K", "L" };

        /// <summary>音ズレ調整（ミリ秒）。+ にすると、遅れて叩いてもちょうどになる。</summary>
        public int offsetMs;

        /// <summary>ノーツの流れる速さ（1 秒に進む距離）</summary>
        public float scrollSpeed = 16f;

        /// <summary>マウス感度：カーソルがマウスレーンの端から端まで動くのに必要なマウスの移動量（ピクセル）。小さいほど敏感。</summary>
        public float pixelsPerLane = 500f;

        /// <summary>6 つのキーレーンのキー（左から）</summary>
        public string[] laneKeys = (string[])DefaultLaneKeys.Clone();

        /// <summary>値を正しい範囲に直す。キー配置がおかしい（数が違う、空、重複）ときは初期配置に戻す。</summary>
        public void Sanitize()
        {
            offsetMs = Math.Max(PlayerOffset.MinMs, Math.Min(PlayerOffset.MaxMs, offsetMs));
            scrollSpeed = Clamp(scrollSpeed, MinScrollSpeed, MaxScrollSpeed, 16f);
            pixelsPerLane = Clamp(pixelsPerLane, MinPixelsPerLane, MaxPixelsPerLane, 500f);

            if (!IsValidKeys(laneKeys)) laneKeys = (string[])DefaultLaneKeys.Clone();
        }

        /// <summary>
        /// レーン lane のキーを key に変える。別のレーンが同じキーを使っていたら、そのレーンと入れ替える（重複を作らない）。
        /// </summary>
        public void SetLaneKey(int lane, string key)
        {
            if (lane < 0 || lane >= laneKeys.Length || string.IsNullOrEmpty(key)) return;
            int other = Array.IndexOf(laneKeys, key);
            if (other >= 0 && other != lane) laneKeys[other] = laneKeys[lane];
            laneKeys[lane] = key;
        }

        public GameSettings Clone()
        {
            var copy = (GameSettings)MemberwiseClone();
            copy.laneKeys = (string[])laneKeys.Clone();
            return copy;
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        private static bool IsValidKeys(string[] keys)
        {
            if (keys == null || keys.Length != DefaultLaneKeys.Length) return false;
            var seen = new HashSet<string>();
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k) || !seen.Add(k)) return false;
            }
            return true;
        }
    }
}
