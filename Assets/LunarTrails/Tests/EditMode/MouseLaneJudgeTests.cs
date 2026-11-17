using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class MouseLaneJudgeTests
    {
        // 60BPM・offset 0 なので「拍 = 秒」
        private static Chart ChartOf(params NoteData[] notes) => ChartBuilder.Build(new ChartData
        {
            version = 1,
            bpm = new[] { new BpmChangeData { beat = 0, value = 60 } },
            notes = notes,
        });

        private static NoteData Click(double beat) => new NoteData { type = "click", beat = beat };
        private static NoteData Track(double beat, float x, float size = 0.2f) => new NoteData { type = "track", beat = beat, x = x, size = size };
        private static NoteData Flick(double beat) => new NoteData { type = "flick", beat = beat, x = 0.5f, size = 0.5f };

        // ===== クリック =====

        [Test]
        public void クリックはタップと同じくずれで判定が決まる()
        {
            var judge = new MouseLaneJudge(ChartOf(Click(1)));

            var result = judge.OnClick(1.05);

            Assert.AreEqual(Judgement.Perfect, result.Value.Judgement);
        }

        [Test]
        public void クリックの見逃しはMISSになる()
        {
            var judge = new MouseLaneJudge(ChartOf(Click(1)));

            var results = judge.Update(1.2, 0.5f, 0f);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Judgement.Miss, results[0].Judgement);
        }

        // ===== トラック =====

        [Test]
        public void トラックはカーソルが重なっていればPERFECTプラス()
        {
            var judge = new MouseLaneJudge(ChartOf(Track(1, x: 0.3f)));

            Assert.AreEqual(0, judge.Update(0.9, 0.3f, 0f).Count, "時刻より前は判定しない");

            var results = judge.Update(0.98, 0.35f, 0f);
            Assert.AreEqual(Judgement.PerfectPlus, results[0].Judgement);
            Assert.IsNull(results[0].OffsetSeconds, "タイミングを問わないノーツなので、ずれは記録しない");
        }

        [Test]
        public void トラックに重ならないまま時刻を過ぎるとMISS()
        {
            var judge = new MouseLaneJudge(ChartOf(Track(1, x: 0.3f)));

            Assert.AreEqual(0, judge.Update(1.0, 0.8f, 0f).Count);
            var results = judge.Update(1.1, 0.8f, 0f);

            Assert.AreEqual(Judgement.Miss, results[0].Judgement);
        }

        [Test]
        public void トラックの幅の外側にも少し余裕がある()
        {
            var settings = new MouseJudgeSettings { TrackMargin = 0.05f };
            var judge = new MouseLaneJudge(ChartOf(Track(1, x: 0.5f, size: 0.2f)), settings: settings);

            // 中心から 0.14 ずれ：幅の半分 0.1 ＋ 余裕 0.05 = 0.15 の内側
            Assert.AreEqual(Judgement.PerfectPlus, judge.Update(1.0, 0.64f, 0f)[0].Judgement);
        }

        // ===== フリック =====

        [Test]
        public void フリックは判定幅の中で速く動いたフレームで判定する()
        {
            var settings = new MouseJudgeSettings { FlickSpeedThreshold = 4f };
            var judge = new MouseLaneJudge(ChartOf(Flick(1)), settings: settings);

            Assert.AreEqual(0, judge.Update(0.99, 0.5f, 1f).Count, "遅い動きでは取れない");

            var results = judge.Update(1.02, 0.5f, 6f);
            Assert.AreEqual(Judgement.PerfectPlus, results[0].Judgement);
            Assert.AreEqual(0.02, results[0].OffsetSeconds.Value, 1e-9);
        }

        [Test]
        public void フリックは判定幅より前に速く動いても取れない()
        {
            var judge = new MouseLaneJudge(ChartOf(Flick(1)));

            Assert.AreEqual(0, judge.Update(0.8, 0.5f, 100f).Count);
            Assert.AreEqual(1, judge.PendingCount);
        }

        [Test]
        public void 一回の払いで二つのフリックは取れない()
        {
            var judge = new MouseLaneJudge(ChartOf(Flick(1.0), Flick(1.05)));

            var results = judge.Update(1.02, 0.5f, 100f);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(0, results[0].Note.Index);
        }

        [Test]
        public void フリックせずに判定幅を過ぎるとMISS()
        {
            var judge = new MouseLaneJudge(ChartOf(Flick(1)));

            var results = judge.Update(1.1, 0.5f, 0f);

            Assert.AreEqual(Judgement.Miss, results[0].Judgement);
        }

        // ===== オートプレイ =====

        [Test]
        public void オートプレイでマウスノーツも全部PERFECTプラスになる()
        {
            var chart = ChartOf(
                Click(1), Track(1.5, x: 0.2f), Flick(2), Track(2.5, x: 0.9f), Click(2.5), Flick(3));
            var auto = new AutoPlay(chart);
            var judge = new MouseLaneJudge(chart);
            var stats = new JudgeStats();

            double previous = double.NegativeInfinity;
            for (double t = 0; t < 4; t += 1.0 / 60)
            {
                foreach (var click in auto.CollectClicks(t))
                    stats.Add(judge.OnClick(click).Value);
                foreach (var r in judge.Update(t, auto.CursorX(t), auto.CursorSpeed(previous, t)))
                    stats.Add(r);
                previous = t;
            }

            Assert.AreEqual(6, stats.Total);
            Assert.AreEqual(6, stats.Count(Judgement.PerfectPlus));
        }

        [Test]
        public void オートプレイのカーソルはノーツの位置を直線でつないで動く()
        {
            var auto = new AutoPlay(ChartOf(Track(1, x: 0.2f), Track(2, x: 0.6f)));

            Assert.AreEqual(0.2f, auto.CursorX(0.0), 1e-6);
            Assert.AreEqual(0.4f, auto.CursorX(1.5), 1e-6);
            Assert.AreEqual(0.6f, auto.CursorX(3.0), 1e-6);
        }
    }
}
