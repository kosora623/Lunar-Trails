using LunarTrails.Core.Charts;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class ChartBuilderTests
    {
        private static ChartData Data(params NoteData[] notes) => new ChartData
        {
            version = 1,
            song = "test",
            difficulty = "lnr",
            offsetMs = 500,
            bpm = new[] { new BpmChangeData { beat = 0, value = 120 } },
            notes = notes,
        };

        [Test]
        public void 拍の位置にoffsetを足した秒になる()
        {
            var chart = ChartBuilder.Build(Data(new NoteData { type = "tap", beat = 2, lane = 0 }));

            // 120BPM で 2 拍 = 1 秒、offset 0.5 秒
            Assert.AreEqual(1.5, chart.Notes[0].Time, 1e-9);
        }

        [Test]
        public void ノーツは時刻順に並び替えられる()
        {
            var chart = ChartBuilder.Build(Data(
                new NoteData { type = "tap", beat = 4, lane = 0 },
                new NoteData { type = "tap", beat = 1, lane = 1 }));

            Assert.AreEqual(1, chart.Notes[0].Index);
            Assert.AreEqual(0, chart.Notes[1].Index);
        }

        [Test]
        public void widthを省略すると1レーン幅になる()
        {
            var chart = ChartBuilder.Build(Data(new NoteData { type = "tap", beat = 0, lane = 5 }));

            Assert.AreEqual(1, chart.Notes[0].Width);
        }

        [Test]
        public void ロングの終わりの時刻が入る()
        {
            var chart = ChartBuilder.Build(Data(new NoteData { type = "long", beat = 2, lane = 0, length = 2 }));

            Assert.AreEqual(2.5, chart.Notes[0].EndTime, 1e-9);
        }

        [Test]
        public void トレイルの通過点は始点からの拍で数える()
        {
            var chart = ChartBuilder.Build(Data(new NoteData
            {
                type = "trail",
                beat = 4,
                size = 0.2f,
                points = new[]
                {
                    new TrailPointData { beat = 0, x = 0.3f },
                    new TrailPointData { beat = 2, x = 0.7f, curve = "smooth" },
                },
            }));

            var trail = chart.Notes[0];
            Assert.AreEqual(2.5, trail.TrailPoints[0].Time, 1e-9);
            Assert.AreEqual(3.5, trail.TrailPoints[1].Time, 1e-9);
            Assert.AreEqual(CurveType.Smooth, trail.TrailPoints[1].Curve);
            Assert.AreEqual(3.5, trail.EndTime, 1e-9);
        }

        [Test]
        public void キーレーンからはみ出すとエラーになる()
        {
            Assert.Throws<ChartFormatException>(() =>
                ChartBuilder.Build(Data(new NoteData { type = "tap", beat = 0, lane = 5, width = 2 })));
        }

        [Test]
        public void 知らないノーツの種類はエラーになる()
        {
            Assert.Throws<ChartFormatException>(() =>
                ChartBuilder.Build(Data(new NoteData { type = "scratch", beat = 0 })));
        }
    }
}
