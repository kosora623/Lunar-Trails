using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class TrailTests
    {
        // 60BPM・offset 0 なので「拍 = 秒」
        private static Chart ChartOf(params NoteData[] notes) => ChartBuilder.Build(new ChartData
        {
            version = 1,
            bpm = new[] { new BpmChangeData { beat = 0, value = 60 } },
            notes = notes,
        });

        private static NoteData Trail(double beat, bool endFlick, params TrailPointData[] points) =>
            new NoteData { type = "trail", beat = beat, size = 0.2f, endFlick = endFlick, points = points };

        private static TrailPointData P(double beat, float x, string curve = null) =>
            new TrailPointData { beat = beat, x = x, curve = curve };

        // ===== 軌道 =====

        [Test]
        public void 直線の区間は点と点をまっすぐつなぐ()
        {
            var trail = ChartOf(Trail(1, false, P(0, 0.2f), P(2, 0.6f))).Notes[0];

            Assert.AreEqual(0.2f, TrailPath.XAt(trail, 1.0), 1e-6);
            Assert.AreEqual(0.4f, TrailPath.XAt(trail, 2.0), 1e-6);
            Assert.AreEqual(0.6f, TrailPath.XAt(trail, 3.0), 1e-6);
            Assert.AreEqual(0.6f, TrailPath.XAt(trail, 9.0), 1e-6, "終点より後は終点の位置");
        }

        [Test]
        public void なめらかな区間も通過点を必ず通る()
        {
            var trail = ChartOf(Trail(0, false, P(0, 0.3f), P(2, 0.7f, "smooth"), P(4, 0.4f, "smooth"))).Notes[0];

            Assert.AreEqual(0.3f, TrailPath.XAt(trail, 0.0), 1e-6);
            Assert.AreEqual(0.7f, TrailPath.XAt(trail, 2.0), 1e-6);
            Assert.AreEqual(0.4f, TrailPath.XAt(trail, 4.0), 1e-6);
        }

        [Test]
        public void なめらかな区間は直線と違う形になりレーンからはみ出さない()
        {
            var trail = ChartOf(Trail(0, false, P(0, 0.0f), P(1, 1.0f, "smooth"), P(2, 0.0f, "smooth"))).Notes[0];

            // 0.5 秒の位置：直線なら 0.5 だが、曲線なので 1 に向かってふくらむ
            float mid = TrailPath.XAt(trail, 0.5);
            Assert.AreNotEqual(0.5f, mid);

            for (double t = 0; t <= 2; t += 0.05)
            {
                float x = TrailPath.XAt(trail, t);
                Assert.That(x, Is.InRange(0f, 1f));
            }
        }

        // ===== 判定 =====

        [Test]
        public void 始点はクリックとして判定する()
        {
            var judge = new MouseLaneJudge(ChartOf(Trail(1, false, P(0, 0.5f), P(1, 0.5f))));

            var result = judge.OnClick(1.07);

            Assert.AreEqual(Judgement.Hit, result.Value.Judgement);
        }

        [Test]
        public void 途中のチェックは押したまま帯の中にいればPERFECTプラス()
        {
            var settings = new MouseJudgeSettings { TrailTickBeats = 0.5 };
            var judge = new MouseLaneJudge(ChartOf(Trail(1, false, P(0, 0.2f), P(1, 0.6f))), settings: settings);
            judge.OnClick(1.0);

            // 1.5 秒のチェック：帯は 0.4 にある
            var results = judge.Update(1.5, 0.42f, 0f, buttonHeld: true);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Judgement.PerfectPlus, results[0].Judgement);
        }

        [Test]
        public void 途中でボタンを離していたらMISS()
        {
            var settings = new MouseJudgeSettings { TrailTickBeats = 0.5, TrailTickWindow = 0.05 };
            var judge = new MouseLaneJudge(ChartOf(Trail(1, false, P(0, 0.5f), P(1, 0.5f))), settings: settings);
            judge.OnClick(1.0);

            Assert.AreEqual(0, judge.Update(1.5, 0.5f, 0f, buttonHeld: false).Count, "まだ猶予の中");
            var results = judge.Update(1.6, 0.5f, 0f, buttonHeld: false);

            Assert.AreEqual(Judgement.Miss, results[0].Judgement);
        }

        [Test]
        public void 帯から外れていたらMISS()
        {
            var settings = new MouseJudgeSettings { TrailTickBeats = 0.5 };
            var judge = new MouseLaneJudge(ChartOf(Trail(1, false, P(0, 0.2f), P(1, 0.2f))), settings: settings);
            judge.OnClick(1.0);

            judge.Update(1.5, 0.9f, 0f, buttonHeld: true);
            var results = judge.Update(1.6, 0.9f, 0f, buttonHeld: true);

            Assert.AreEqual(Judgement.Miss, results[0].Judgement);
        }

        [Test]
        public void チェックは決めた拍ごとと終点に置かれる()
        {
            var settings = new MouseJudgeSettings { TrailTickBeats = 0.5 };
            // 長さ 2 拍：始点（クリック）＋ 0.5, 1.0, 1.5, 2.0 拍目のチェック 4 つ
            var judge = new MouseLaneJudge(ChartOf(Trail(1, false, P(0, 0.5f), P(2, 0.5f))), settings: settings);

            Assert.AreEqual(5, judge.PendingCount);
        }

        [Test]
        public void 終点フリックがあれば別のフリックとして判定する()
        {
            var judge = new MouseLaneJudge(ChartOf(Trail(1, true, P(0, 0.5f), P(1, 0.5f))));
            judge.OnClick(1.0);

            var results = judge.Update(2.01, 0.5f, 100f, buttonHeld: true);

            Assert.That(results.Exists(r => r.Note.Type == NoteType.Flick && r.Judgement == Judgement.PerfectPlus));
        }

        [Test]
        public void オートプレイでトレイルも全部PERFECTプラスになる()
        {
            var chart = ChartOf(
                Trail(1, false, P(0, 0.3f), P(2, 0.7f, "smooth"), P(4, 0.4f, "smooth")),
                new NoteData { type = "click", beat = 6 },
                Trail(7, true, P(0, 0.1f), P(1, 0.9f), P(1.5, 0.9f)));
            var auto = new AutoPlay(chart);
            var judge = new MouseLaneJudge(chart);
            var stats = new JudgeStats();

            double previous = double.NegativeInfinity;
            for (double t = 0; t < 10; t += 1.0 / 60)
            {
                foreach (var click in auto.CollectClicks(t))
                    stats.Add(judge.OnClick(click).Value);
                foreach (var r in judge.Update(t, auto.CursorX(t), auto.CursorSpeed(previous, t), auto.IsHolding(t)))
                    stats.Add(r);
                previous = t;
            }

            Assert.AreEqual(0, judge.PendingCount);
            Assert.AreEqual(stats.Total, stats.Count(Judgement.PerfectPlus));
        }
    }
}
