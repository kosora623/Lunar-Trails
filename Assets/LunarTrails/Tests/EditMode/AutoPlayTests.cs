using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class AutoPlayTests
    {
        // 60BPM・offset 0 なので「拍 = 秒」
        private static Chart ChartOf(params NoteData[] notes) => ChartBuilder.Build(new ChartData
        {
            version = 1,
            bpm = new[] { new BpmChangeData { beat = 0, value = 60 } },
            notes = notes,
        });

        [Test]
        public void 時刻になったノーツだけをちょうどの時刻で押す()
        {
            var auto = new AutoPlay(ChartOf(
                new NoteData { type = "tap", beat = 1, lane = 2 },
                new NoteData { type = "tap", beat = 2, lane = 4 }));

            Assert.AreEqual(0, auto.Collect(0.99).Count);

            var presses = auto.Collect(1.5);
            Assert.AreEqual(1, presses.Count);
            Assert.AreEqual(2, presses[0].Lane);
            Assert.AreEqual(1.0, presses[0].Time, 1e-9);

            Assert.AreEqual(0, auto.Collect(1.6).Count, "一度押したノーツは二度押さない");
        }

        [Test]
        public void オートプレイで全ノーツがPERFECTプラスになる()
        {
            var chart = ChartOf(
                new NoteData { type = "tap", beat = 1, lane = 0 },
                new NoteData { type = "tap", beat = 1, lane = 5 },
                new NoteData { type = "long", beat = 1.25, lane = 3, length = 1 },
                new NoteData { type = "tap", beat = 1.5, lane = 1, width = 2 },
                new NoteData { type = "guide", beat = 2, lane = 0 },
                new NoteData { type = "click", beat = 2 });
            var auto = new AutoPlay(chart);
            var judge = new KeyLaneJudge(chart);
            var stats = new JudgeStats();

            // 60fps でフレームを進めるつもりで呼ぶ
            for (double t = 0; t < 4; t += 1.0 / 60)
            {
                foreach (var press in auto.Collect(t))
                {
                    var result = judge.OnKeyDown(press.Lane, press.Time);
                    Assert.IsTrue(result.HasValue);
                    stats.Add(result.Value);
                }
                foreach (var miss in judge.CollectMisses(t)) stats.Add(miss);
            }

            Assert.AreEqual(4, stats.Total);
            Assert.AreEqual(4, stats.Count(Judgement.PerfectPlus));
            Assert.AreEqual(4, stats.MaxCombo);
        }
    }
}
