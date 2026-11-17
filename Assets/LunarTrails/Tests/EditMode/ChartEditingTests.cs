using LunarTrails.Core.Charts;
using LunarTrails.Core.Editing;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class ChartEditingTests
    {
        private static NoteData Tap(double beat, int lane) => new NoteData { type = "tap", beat = beat, lane = lane, width = 1 };

        [Test]
        public void AddAndRemove_KeepsNotesSortedByBeat()
        {
            var doc = new ChartDocument();
            doc.AddNote(Tap(2, 0));
            doc.AddNote(Tap(1, 1));
            Assert.AreEqual(1.0, doc.Notes[0].beat);
            doc.RemoveNote(0);
            Assert.AreEqual(1, doc.Notes.Count);
            Assert.AreEqual(2.0, doc.Notes[0].beat);
            Assert.IsTrue(doc.IsDirty);
        }

        [Test]
        public void UndoRedo_RestoresState()
        {
            var doc = new ChartDocument();
            doc.AddNote(Tap(1, 0));
            doc.ChangeNote(0, n => n.lane = 3);
            doc.Undo();
            Assert.AreEqual(0, doc.Notes[0].lane);
            doc.Undo();
            Assert.AreEqual(0, doc.Notes.Count);
            doc.Redo();
            doc.Redo();
            Assert.AreEqual(3, doc.Notes[0].lane);
        }

        [Test]
        public void NewEdit_ClearsRedo()
        {
            var doc = new ChartDocument();
            doc.AddNote(Tap(1, 0));
            doc.Undo();
            doc.AddNote(Tap(2, 0));
            Assert.IsFalse(doc.CanRedo);
        }

        [Test]
        public void Document_DoesNotShareDataWithSource()
        {
            var data = ChartDocument.NewChart();
            var doc = new ChartDocument(data);
            doc.AddNote(Tap(1, 0));
            Assert.AreEqual(0, data.notes.Length);
        }

        [Test]
        public void Build_MakesPlayableChart()
        {
            var doc = new ChartDocument();
            doc.AddNote(Tap(2, 0)); // BPM 120 なので 1 秒
            Assert.AreEqual(1.0, doc.Build().Notes[0].Time, 1e-9);
        }

        [TestCase(1.13, 4, 1.25)]
        [TestCase(1.10, 4, 1.0)]
        [TestCase(1.30, 3, 1.3333333333)]
        public void SnapBeat_RoundsToDivision(double beat, int division, double expected)
        {
            Assert.AreEqual(expected, EditorGrid.SnapBeat(beat, division), 1e-6);
        }

        [Test]
        public void SnapCenter_KeepsNoteInsideLane()
        {
            Assert.AreEqual(0.125f, EditorGrid.SnapCenter(0.01f, 0.25f), 1e-6);
            Assert.AreEqual(0.875f, EditorGrid.SnapCenter(0.99f, 0.25f), 1e-6);
        }

        [Test]
        public void Picker_FindsKeyAndMouseNotes()
        {
            var doc = new ChartDocument();
            doc.AddNote(Tap(1, 2));
            doc.AddNote(new NoteData { type = "flick", beat = 1, x = 0.5f, size = 0.25f });
            doc.AddNote(new NoteData { type = "long", beat = 4, lane = 0, width = 1, length = 2 });

            Assert.AreEqual("tap", doc.Notes[NotePicker.Find(doc, 1.05, NotePicker.Column.Key(2), 0.1)].type);
            Assert.AreEqual("flick", doc.Notes[NotePicker.Find(doc, 1.0, NotePicker.Column.Mouse(0.55f), 0.1)].type);
            Assert.AreEqual(-1, NotePicker.Find(doc, 1.0, NotePicker.Column.Mouse(0.9f), 0.1));
            Assert.AreEqual("long", doc.Notes[NotePicker.Find(doc, 5.0, NotePicker.Column.Key(0), 0.1)].type); // 帯の上
        }

        [Test]
        public void Picker_FindsTrailAlongItsPath()
        {
            var doc = new ChartDocument();
            doc.AddNote(new NoteData
            {
                type = "trail", beat = 0, size = 0.125f,
                points = new[] { new TrailPointData { beat = 0, x = 0 }, new TrailPointData { beat = 4, x = 1 } },
            });
            Assert.AreEqual(0, NotePicker.Find(doc, 2.0, NotePicker.Column.Mouse(0.5f), 0.1));
            Assert.AreEqual(-1, NotePicker.Find(doc, 2.0, NotePicker.Column.Mouse(0.9f), 0.1));
        }

        [Test]
        public void MoveTo_KeyNoteStaysInKeyLanes()
        {
            var n = new NoteData { type = "tap", beat = 1, lane = 0, width = 2 };
            Assert.IsTrue(NoteEditing.MoveTo(n, 3, NotePicker.Column.Key(5)));
            Assert.AreEqual(4, n.lane); // 幅 2 なのでレーン 4 まで
            Assert.AreEqual(3.0, n.beat);
            Assert.IsFalse(NoteEditing.MoveTo(n, 3, NotePicker.Column.Mouse(0.5f)));
        }

        [Test]
        public void MoveTo_TrailShiftsAllPointsWithinLane()
        {
            var n = new NoteData
            {
                type = "trail", beat = 0, size = 0.125f,
                points = new[] { new TrailPointData { beat = 0, x = 0.25f }, new TrailPointData { beat = 2, x = 0.75f } },
            };
            NoteEditing.MoveTo(n, 1, NotePicker.Column.Mouse(0.75f));
            // 終点が 1 を超えないよう、ずらす量は 0.25 まで
            Assert.AreEqual(0.5f, n.points[0].x, 1e-6);
            Assert.AreEqual(1.0f, n.points[1].x, 1e-6);
            Assert.AreEqual(1.0, n.beat);
        }

        [Test]
        public void AddTrailPoint_KeepsOrderAndReplacesSameBeat()
        {
            var n = new NoteData { type = "trail", points = new[] { new TrailPointData { beat = 0, x = 0.5f } } };
            NoteEditing.AddTrailPoint(n, 2, 0.25f, smooth: true);
            NoteEditing.AddTrailPoint(n, 1, 0.75f, smooth: false);
            NoteEditing.AddTrailPoint(n, 2, 0.5f, smooth: false);
            Assert.AreEqual(3, n.points.Length);
            Assert.AreEqual(1.0, n.points[1].beat);
            Assert.AreEqual(0.5f, n.points[2].x);
            Assert.AreEqual("linear", n.points[2].curve);
        }

        [Test]
        public void Resize_ClampsWidthAndSize()
        {
            var tap = new NoteData { type = "tap", lane = 4, width = 1 };
            NoteEditing.Resize(tap, +5);
            Assert.AreEqual(2, tap.width);
            var flick = new NoteData { type = "flick", x = 0.5f, size = 0.0625f };
            NoteEditing.Resize(flick, -3);
            Assert.AreEqual(0.0625f, flick.size, 1e-6);
            NoteEditing.Resize(flick, +3);
            Assert.AreEqual(0.25f, flick.size, 1e-6);
        }
    }
}
