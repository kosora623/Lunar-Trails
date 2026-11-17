using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class KeyLaneJudgeTests
    {
        // 60BPM・offset 0 なので「拍 = 秒」になり、時刻を読みやすい
        private static Chart ChartOf(params NoteData[] notes) => ChartBuilder.Build(new ChartData
        {
            version = 1,
            bpm = new[] { new BpmChangeData { beat = 0, value = 60 } },
            notes = notes,
        });

        private static NoteData Tap(double beat, int lane, int width = 1) =>
            new NoteData { type = "tap", beat = beat, lane = lane, width = width };

        [TestCase(0.000, Judgement.PerfectPlus)]
        [TestCase(-0.040, Judgement.PerfectPlus)]
        [TestCase(0.050, Judgement.Perfect)]
        [TestCase(-0.060, Judgement.Perfect)]
        [TestCase(0.080, Judgement.Hit)]
        [TestCase(-0.090, Judgement.Hit)]
        public void ずれの大きさで判定が決まる(double offset, Judgement expected)
        {
            var judge = new KeyLaneJudge(ChartOf(Tap(1, 0)));

            var result = judge.OnKeyDown(0, 1 + offset);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(expected, result.Value.Judgement);
        }

        [Test]
        public void 判定幅より早く押しても何も取らない()
        {
            var judge = new KeyLaneJudge(ChartOf(Tap(1, 0)));

            Assert.IsNull(judge.OnKeyDown(0, 0.8));
            Assert.AreEqual(1, judge.PendingCount);
        }

        [Test]
        public void 別のレーンを押しても取らない()
        {
            var judge = new KeyLaneJudge(ChartOf(Tap(1, 0)));

            Assert.IsNull(judge.OnKeyDown(1, 1.0));
        }

        [Test]
        public void 幅のあるノーツはかかっているどのレーンでも取れる()
        {
            var judge = new KeyLaneJudge(ChartOf(Tap(1, 3, width: 3)));

            Assert.IsTrue(judge.OnKeyDown(5, 1.0).HasValue);
        }

        [Test]
        public void 同じレーンに続くノーツは早い方から取る()
        {
            var judge = new KeyLaneJudge(ChartOf(Tap(1.00, 0), Tap(1.05, 0)));

            var first = judge.OnKeyDown(0, 1.04);

            Assert.AreEqual(0, first.Value.Note.Index);
        }

        [Test]
        public void 判定幅を過ぎた見逃しはMISSになる()
        {
            var judge = new KeyLaneJudge(ChartOf(Tap(1, 0), Tap(2, 1)));

            Assert.AreEqual(0, judge.CollectMisses(1.09).Count);

            var misses = judge.CollectMisses(1.2);
            Assert.AreEqual(1, misses.Count);
            Assert.AreEqual(Judgement.Miss, misses[0].Judgement);
            Assert.AreEqual(1, judge.PendingCount);
        }

        [Test]
        public void ガイドノーツは判定しない()
        {
            var judge = new KeyLaneJudge(ChartOf(new NoteData { type = "guide", beat = 1, lane = 0 }));

            Assert.AreEqual(0, judge.PendingCount);
        }
    }
}
