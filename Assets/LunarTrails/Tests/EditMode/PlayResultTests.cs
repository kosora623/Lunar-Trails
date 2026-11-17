using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using LunarTrails.Core.Scoring;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class PlayResultTests
    {
        private static readonly Note AnyNote = new Note();

        private static JudgeStats StatsOf(params Judgement[] judgements)
        {
            var stats = new JudgeStats();
            foreach (var j in judgements)
                stats.Add(new JudgeResult(AnyNote, j, j == Judgement.Miss ? (double?)null : 0.0));
            return stats;
        }

        [Test]
        public void 全部PERFECTプラスなら達成率101パーセントでPTプラス()
        {
            var result = new PlayResult(StatsOf(Judgement.PerfectPlus, Judgement.PerfectPlus));

            Assert.AreEqual(101.0, result.AchievementRate, 1e-9);
            Assert.AreEqual(Lamp.PerfectTrackPlus, result.Lamp);
        }

        [Test]
        public void 達成率は判定ごとの点数の平均()
        {
            // (101 + 100 + 50 + 0) / 4 = 62.75
            var result = new PlayResult(StatsOf(Judgement.PerfectPlus, Judgement.Perfect, Judgement.Hit, Judgement.Miss));

            Assert.AreEqual(62.75, result.AchievementRate, 1e-9);
            Assert.AreEqual(Lamp.Clear, result.Lamp);
        }

        [TestCase(new[] { Judgement.PerfectPlus, Judgement.Perfect }, Lamp.PerfectTrack)]
        [TestCase(new[] { Judgement.PerfectPlus, Judgement.Hit }, Lamp.FullTrack)]
        [TestCase(new[] { Judgement.PerfectPlus, Judgement.Miss }, Lamp.Clear)]
        public void ランプは一番悪い判定で決まる(Judgement[] judgements, Lamp expected)
        {
            Assert.AreEqual(expected, new PlayResult(StatsOf(judgements)).Lamp);
        }

        [Test]
        public void ノーツがなければ達成率0でランプなし()
        {
            var result = new PlayResult(new JudgeStats());

            Assert.AreEqual(0.0, result.AchievementRate);
            Assert.AreEqual(Lamp.Clear, result.Lamp);
        }

        [Test]
        public void FASTとSLOWはPERFECTプラス以外のずれだけ数える()
        {
            var stats = new JudgeStats();
            void Add(double offset) => stats.Add(new JudgeResult(AnyNote, JudgeWindows.Default.Classify(offset), offset));
            Add(-0.010); // PERFECT+ なので数えない
            Add(-0.060); // FAST
            Add(+0.080); // SLOW
            Add(+0.090); // SLOW
            stats.Add(new JudgeResult(AnyNote, Judgement.PerfectPlus, null)); // タイミングを問わない判定は数えない

            var result = new PlayResult(stats);

            Assert.AreEqual(1, result.FastCount);
            Assert.AreEqual(2, result.SlowCount);
            Assert.AreEqual(0.025, result.AverageOffset.Value, 1e-9);
        }
    }
}
