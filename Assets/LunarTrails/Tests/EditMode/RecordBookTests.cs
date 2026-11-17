using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using LunarTrails.Core.Records;
using LunarTrails.Core.Scoring;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class RecordBookTests
    {
        private static readonly Note AnyNote = new Note();

        private static PlayResult ResultOf(params Judgement[] judgements)
        {
            var stats = new JudgeStats();
            foreach (var j in judgements)
                stats.Add(new JudgeResult(AnyNote, j, j == Judgement.Miss ? (double?)null : 0.0));
            return new PlayResult(stats);
        }

        [Test]
        public void キーは曲のIDと難易度()
        {
            Assert.AreEqual("moon/inv", RecordBook.Key("moon", "inv"));
        }

        [Test]
        public void 初めての記録はすべて新記録()
        {
            var book = new RecordBook();
            var update = book.Submit("moon/stl", ResultOf(Judgement.Perfect, Judgement.Miss));

            Assert.IsTrue(update.IsFirstPlay);
            Assert.IsTrue(update.IsNewBestRate);
            Assert.IsTrue(update.IsNewBestLamp);
            Assert.IsNull(update.PreviousBestRate);

            var record = book.Find("moon/stl");
            Assert.AreEqual(50.0, record.bestRate, 1e-9);
            Assert.AreEqual(Lamp.Clear, record.bestLamp);
            Assert.AreEqual(1, record.playCount);
        }

        [Test]
        public void 下回った結果では自己ベストは変わらず回数だけ増える()
        {
            var book = new RecordBook();
            book.Submit("moon/stl", ResultOf(Judgement.Perfect, Judgement.Perfect));   // 100%
            var update = book.Submit("moon/stl", ResultOf(Judgement.Perfect, Judgement.Hit)); // 75%

            Assert.IsFalse(update.IsFirstPlay);
            Assert.IsFalse(update.IsNewBestRate);
            Assert.IsFalse(update.IsNewBestLamp);
            Assert.AreEqual(100.0, update.PreviousBestRate.Value, 1e-9);

            var record = book.Find("moon/stl");
            Assert.AreEqual(100.0, record.bestRate, 1e-9);
            Assert.AreEqual(Lamp.PerfectTrack, record.bestLamp);
            Assert.AreEqual(2, record.playCount);
        }

        [Test]
        public void 達成率とランプは別々に一番良いものを残す()
        {
            var book = new RecordBook();
            // 4 判定で 1 つ MISS：(101*3 + 0) / 4 = 75.75%、CLEAR
            book.Submit("k", ResultOf(Judgement.PerfectPlus, Judgement.PerfectPlus, Judgement.PerfectPlus, Judgement.Miss));
            // MISS なしだが達成率は低い：(50*4) / 4 = 50%、FT
            var update = book.Submit("k", ResultOf(Judgement.Hit, Judgement.Hit, Judgement.Hit, Judgement.Hit));

            Assert.IsFalse(update.IsNewBestRate);
            Assert.IsTrue(update.IsNewBestLamp);
            var record = book.Find("k");
            Assert.AreEqual(75.75, record.bestRate, 1e-9);
            Assert.AreEqual(Lamp.FullTrack, record.bestLamp);
        }

        [Test]
        public void 譜面ごとに別の記録()
        {
            var book = new RecordBook();
            book.Submit("moon/stl", ResultOf(Judgement.Perfect));
            Assert.IsNull(book.Find("moon/inv"));
        }

        [Test]
        public void Sanitizeで壊れた記録を直す()
        {
            var book = new RecordBook();
            book.records.Add(null);
            book.records.Add(new PlayRecord { key = "" });
            book.records.Add(new PlayRecord { key = "a", bestRate = 500, bestLamp = (Lamp)99, playCount = -3 });

            book.Sanitize();

            Assert.AreEqual(1, book.records.Count);
            var r = book.records[0];
            Assert.AreEqual(PlayResult.PerfectPlusRate, r.bestRate);
            Assert.AreEqual(Lamp.Clear, r.bestLamp);
            Assert.AreEqual(0, r.playCount);
        }
    }
}
