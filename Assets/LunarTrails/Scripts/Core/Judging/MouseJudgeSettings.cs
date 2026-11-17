namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// マウスレーンの判定の設定値。どれも仮の値で、試遊でマウスとトラックパッドの両方を使って決める。
    /// 位置と幅は「マウスレーンの幅 = 1」とした単位。
    /// </summary>
    public sealed class MouseJudgeSettings
    {
        public static readonly MouseJudgeSettings Default = new MouseJudgeSettings();

        /// <summary>トラック：ノーツの時刻の前後この秒数のうちに、カーソルが重なっていれば成功</summary>
        public double TrackWindow = 0.05;

        /// <summary>トラック：ノーツの幅の外側に、さらにこれだけの余裕を持たせる（片側）</summary>
        public float TrackMargin = 0.05f;

        /// <summary>フリック：成功とみなすカーソルの速さ（1 秒にマウスレーン何本分動いたか）</summary>
        public float FlickSpeedThreshold = 4f;
    }
}
