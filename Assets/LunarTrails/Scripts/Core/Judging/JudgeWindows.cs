using System;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// 判定幅（秒）。値は企画書の仮の値（±40 / 60 / 90ms）。試作で叩いて調整する前提なので、
    /// 調整するときはここ（または Default を使っている箇所）だけ直せばよい。
    /// </summary>
    public sealed class JudgeWindows
    {
        public static readonly JudgeWindows Default = new JudgeWindows(0.040, 0.060, 0.090);

        // 浮動小数点の誤差で、ちょうど 40ms のずれが 40.0000000001ms と計算されて
        // 1段下の判定になるのを防ぐための余裕（1マイクロ秒）。
        private const double Tolerance = 1e-6;

        public double PerfectPlus { get; }
        public double Perfect { get; }
        public double Hit { get; }

        public JudgeWindows(double perfectPlus, double perfect, double hit)
        {
            if (!(0 < perfectPlus && perfectPlus <= perfect && perfect <= hit))
                throw new ArgumentException("判定幅は 0 < PERFECT+ <= PERFECT <= HIT にしてください。");
            PerfectPlus = perfectPlus;
            Perfect = perfect;
            Hit = hit;
        }

        /// <summary>
        /// ずれ（入力時刻 − ノーツの時刻、秒）から判定を決める。HIT の幅の外なら Miss。
        /// </summary>
        public Judgement Classify(double offsetSeconds)
        {
            double abs = Math.Abs(offsetSeconds) - Tolerance;
            if (abs <= PerfectPlus) return Judgement.PerfectPlus;
            if (abs <= Perfect) return Judgement.Perfect;
            if (abs <= Hit) return Judgement.Hit;
            return Judgement.Miss;
        }

        /// <summary>HIT の幅より早い（まだ判定の対象にならない）か</summary>
        public bool IsTooEarly(double offsetSeconds) => offsetSeconds + Tolerance < -Hit;

        /// <summary>HIT の幅を過ぎた（もう取れない）か</summary>
        public bool IsTooLate(double offsetSeconds) => offsetSeconds - Tolerance > Hit;
    }
}
