using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using LunarTrails.Core.Timing;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class JudgeStatsTests
    {
        private static readonly Note AnyNote = new Note();

        private static JudgeResult Hit(double offset) =>
            new JudgeResult(AnyNote, JudgeWindows.Default.Classify(offset), offset);

        private static JudgeResult Miss() => new JudgeResult(AnyNote, Judgement.Miss, null);

        [Test]
        public void MISSでコンボが切れて最大コンボは残る()
        {
            var stats = new JudgeStats();
            stats.Add(Hit(0));
            stats.Add(Hit(0.05));
            stats.Add(Miss());
            stats.Add(Hit(0));

            Assert.AreEqual(1, stats.Combo);
            Assert.AreEqual(2, stats.MaxCombo);
            Assert.AreEqual(1, stats.Count(Judgement.Miss));
            Assert.AreEqual(4, stats.Total);
        }

        [Test]
        public void ずれの平均は見逃しを含めず最近の分だけで出す()
        {
            var stats = new JudgeStats();
            Assert.IsNull(stats.RecentAverageOffset);

            stats.Add(Hit(0.010));
            stats.Add(Miss());
            stats.Add(Hit(0.030));
            Assert.AreEqual(0.020, stats.RecentAverageOffset.Value, 1e-9);

            for (int i = 0; i < JudgeStats.RecentOffsetCount; i++) stats.Add(Hit(-0.005));
            Assert.AreEqual(-0.005, stats.RecentAverageOffset.Value, 1e-9);
        }

        [Test]
        public void オフセットはゲームの時刻を遅らせて範囲内に収まる()
        {
            var offset = new PlayerOffset(20);
            Assert.AreEqual(0.98, offset.ToGameTime(1.0), 1e-9);

            offset.Milliseconds = 1000;
            Assert.AreEqual(PlayerOffset.MaxMs, offset.Milliseconds);
            offset.Milliseconds = -1000;
            Assert.AreEqual(PlayerOffset.MinMs, offset.Milliseconds);
        }
    }
}
