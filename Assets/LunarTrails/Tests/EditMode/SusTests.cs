using System.Linq;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Charts.Sus;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class SusTests
    {
        private const string Header =
            "#TITLE \"テスト曲\"\n" +
            "#SONGID \"test01\"\n" +
            "#DIFFICULTY 2\n" +
            "#PLAYLEVEL 12+\n" +
            "#WAVEOFFSET 0.5\n" +
            "#REQUEST \"ticks_per_beat 480\"\n" +
            "#BPM01: 120\n" +
            "#00002: 4\n" +
            "#00008: 01\n";

        private static Chart Load(string body) => SusConverter.ToChart(Header + body);

        [Test]
        public void Header_IsRead()
        {
            var chart = Load("");
            Assert.AreEqual("test01", chart.Song);
            Assert.AreEqual("lnr", chart.Difficulty);
            Assert.AreEqual(12.5f, chart.Level);
            Assert.AreEqual(0.5, chart.OffsetSeconds, 1e-9);
        }

        [Test]
        public void Tap_PositionInMeasure_BecomesBeat()
        {
            // 小節 1 を 4 等分した 2 番目（= 4 拍 + 1 拍）。レーン 2〜3 はキー 0
            var chart = Load("#00112: 00120000\n");
            var note = chart.Notes.Single();
            Assert.AreEqual(NoteType.Tap, note.Type);
            Assert.AreEqual(5.0, note.Beat, 1e-9);
            Assert.AreEqual(0, note.Lane);
            Assert.AreEqual(1, note.Width);
            // 120 BPM で 5 拍 = 2.5 秒、そこにオフセット 0.5 秒
            Assert.AreEqual(3.0, note.Time, 1e-9);
        }

        [Test]
        public void Tap_WideNote_CoversSeveralKeys()
        {
            // レーン 4 から幅 4 → キー 1 と 2
            var note = Load("#00014: 14\n").Notes.Single();
            Assert.AreEqual(1, note.Lane);
            Assert.AreEqual(2, note.Width);
        }

        [Test]
        public void Tap_LaneHex_IsRead()
        {
            // レーン c（12）、幅 2 → キー 5
            var note = Load("#0001c: 12\n").Notes.Single();
            Assert.AreEqual(5, note.Lane);
        }

        [Test]
        public void Tap_OutsideKeyArea_IsError()
        {
            var e = Assert.Throws<ChartFormatException>(() => Load("#00010: 12\n"));
            StringAssert.Contains("レーン 2〜13", e.Message);
        }

        [Test]
        public void MouseNotes_UseWholeWidth()
        {
            // ExTAP → クリック、フリック → フリック（レーン 0 幅 4 → 中心 2/16）、ダメージ → トラック
            var chart = Load("#00010: 24\n#00110: 34\n#00218: 48\n");
            var notes = chart.Notes.ToList();
            Assert.AreEqual(NoteType.Click, notes[0].Type);
            Assert.AreEqual(NoteType.Flick, notes[1].Type);
            Assert.AreEqual(2f / 16, notes[1].X, 1e-6);
            Assert.AreEqual(4f / 16, notes[1].Size, 1e-6);
            Assert.AreEqual(NoteType.Track, notes[2].Type);
            Assert.AreEqual(0.75f, notes[2].X, 1e-6);
            Assert.AreEqual(0.5f, notes[2].Size, 1e-6);
        }

        [Test]
        public void Hold_BecomesLong_AcrossMeasures()
        {
            var note = Load("#0002ca: 1200\n#0012ca: 0022\n").Notes.Single();
            Assert.AreEqual(NoteType.Long, note.Type);
            Assert.AreEqual(5, note.Lane);
            Assert.AreEqual(0.0, note.Beat, 1e-9);
            Assert.AreEqual(3.0, note.EndTime - 0.5, 1e-9); // 6 拍 = 3 秒
        }

        [Test]
        public void Slide_BecomesTrail_WithAirAsEndFlick()
        {
            // 始点 レーン 0 幅 4、曲がりの点、終点 レーン 8 幅 4、終点に AIR
            var body =
                "#00030a: 14000000\n" +
                "#00034a: 00440000\n" +
                "#00038a: 00002400\n" +
                "#00058: 00001400\n";
            var note = Load(body).Notes.Single();
            Assert.AreEqual(NoteType.Trail, note.Type);
            Assert.IsTrue(note.EndFlick);
            Assert.AreEqual(2, note.TrailPoints.Count); // 曲がりの点は通る点ではない
            Assert.AreEqual(CurveType.Smooth, note.TrailPoints[1].Curve);
            Assert.AreEqual(2f / 16, note.TrailPoints[0].X, 1e-6);
            Assert.AreEqual(10f / 16, note.TrailPoints[1].X, 1e-6);
            Assert.AreEqual(0.25f, note.Size, 1e-6);
        }

        [Test]
        public void Slide_WithoutEnd_IsError()
        {
            Assert.Throws<ChartFormatException>(() => Load("#00030a: 14\n"));
        }

        [Test]
        public void MeasureLength_ChangesBeatsOfLaterMeasures()
        {
            // 小節 0 は 3 拍。小節 1 の頭は 3 拍目
            var chart = SusConverter.ToChart(
                "#BPM01: 120\n#00008: 01\n#00002: 3\n#00104: 4\n#00112: 12\n");
            Assert.AreEqual(3.0, chart.Notes.Single().Beat, 1e-9);
        }

        [Test]
        public void BpmChange_IsApplied()
        {
            // 0 拍目 120、4 拍目から 60。8 拍目のノーツは 2 秒 + 4 秒
            var chart = SusConverter.ToChart(
                "#BPM01: 120\n#BPM02: 60\n#00008: 01\n#00108: 02\n#00212: 12\n");
            Assert.AreEqual(6.0, chart.Notes.Single().Time, 1e-9);
        }

        [Test]
        public void UnusedNotes_AreWarned()
        {
            var warnings = new System.Collections.Generic.List<string>();
            SusConverter.ToChart(Header + "#00010: 54\n#00050: 14\n", warnings);
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void NoBpm_IsError()
        {
            Assert.Throws<ChartFormatException>(() => SusConverter.ToChart("#00012: 12\n"));
        }
    }
}
