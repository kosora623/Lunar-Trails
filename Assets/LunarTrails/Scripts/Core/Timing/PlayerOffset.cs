using System;

namespace LunarTrails.Core.Timing
{
    /// <summary>
    /// プレイヤーごとの音ズレ調整（オフセット）。
    /// </summary>
    /// <remarks>
    /// 音が鳴ってから耳に届くまでの遅れや、キーを押してから PC に届くまでの遅れは、機器によって違う。
    /// その差を「ゲームの時刻 = 曲の再生位置 − オフセット」として吸収する。
    /// 判定とノーツの表示の両方が、この「ゲームの時刻」で動く。
    ///
    /// 例：いつも 20ms 遅れて叩いてしまう環境なら、オフセットを +20ms にするとちょうどになる。
    /// </remarks>
    public sealed class PlayerOffset
    {
        public const int MinMs = -300;
        public const int MaxMs = 300;

        private int _milliseconds;

        public PlayerOffset(int milliseconds = 0)
        {
            Milliseconds = milliseconds;
        }

        /// <summary>オフセット（ミリ秒）。MinMs〜MaxMs に収める。</summary>
        public int Milliseconds
        {
            get => _milliseconds;
            set => _milliseconds = Math.Max(MinMs, Math.Min(MaxMs, value));
        }

        public double Seconds => _milliseconds / 1000.0;

        /// <summary>曲の再生位置から、ゲームの時刻（判定と表示に使う時刻）を求める。</summary>
        public double ToGameTime(double songTime) => songTime - Seconds;
    }
}
