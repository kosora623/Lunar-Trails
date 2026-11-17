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
        public double TrackWindow = 0.08;

        /// <summary>トラック：ノーツの幅の外側に、さらにこれだけの余裕を持たせる（片側）</summary>
        public float TrackMargin = 0.08f;

        /// <summary>フリック：成功とみなすカーソルの速さ（1 秒にマウスレーン何本分動いたか）</summary>
        public float FlickSpeedThreshold = 3f;

        /// <summary>トレイル：途中のチェックを何拍ごとに置くか</summary>
        public double TrailTickBeats = 0.5;

        /// <summary>トレイル：チェックの時刻の前後この秒数のうちに、なぞれていれば成功</summary>
        public double TrailTickWindow = 0.08;

        /// <summary>トレイル：帯の幅の外側に持たせる余裕（片側）</summary>
        public float TrailMargin = 0.12f;

        /// <summary>
        /// トレイル：ボタンを離しても、この秒数以内なら押したままとみなす。
        /// トラックパッドで指が一瞬浮いたときなどに切れないための、ごく短い猶予。
        /// トレイル上のクリックは「押さえていない方のボタンで叩く」のが正しい遊び方なので、
        /// 同じボタンで押し直せるほど長く（0.1 秒以上）はしない。
        /// </summary>
        public double TrailReleaseGrace = 0.05;
    }
}
