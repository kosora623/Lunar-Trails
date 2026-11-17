using LunarTrails.Core.Timing;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class TempoMapTests
    {
        [Test]
        public void 一定のBPMなら拍数かける1拍の秒数になる()
        {
            var tempo = new TempoMap(new[] { (0.0, 120.0) });

            Assert.AreEqual(0.0, tempo.BeatToSeconds(0), 1e-9);
            Assert.AreEqual(0.5, tempo.BeatToSeconds(1), 1e-9);
            Assert.AreEqual(4.0, tempo.BeatToSeconds(8), 1e-9);
        }

        [Test]
        public void BPMが途中で変わると変化点から先は新しいBPMで数える()
        {
            // 0〜4拍は 120BPM（2秒）、4拍目から 60BPM
            var tempo = new TempoMap(new[] { (4.0, 60.0), (0.0, 120.0) });

            Assert.AreEqual(2.0, tempo.BeatToSeconds(4), 1e-9);
            Assert.AreEqual(3.0, tempo.BeatToSeconds(5), 1e-9);
            Assert.AreEqual(60.0, tempo.BpmAt(4.5), 1e-9);
        }

        [Test]
        public void 秒から拍に戻すと元の拍になる()
        {
            var tempo = new TempoMap(new[] { (0.0, 180.0), (16.0, 90.0), (20.0, 200.0) });

            foreach (var beat in new[] { 0.0, 3.25, 16.0, 18.5, 20.0, 31.75 })
            {
                Assert.AreEqual(beat, tempo.SecondsToBeat(tempo.BeatToSeconds(beat)), 1e-9);
            }
        }

        [Test]
        public void beat0のBPMがないとエラーになる()
        {
            Assert.Throws<System.ArgumentException>(() => new TempoMap(new[] { (4.0, 120.0) }));
        }
    }
}
