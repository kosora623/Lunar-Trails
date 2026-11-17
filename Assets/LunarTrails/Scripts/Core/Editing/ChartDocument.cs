using System;
using System.Collections.Generic;
using System.Linq;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Editing
{
    /// <summary>
    /// 譜面エディタで編集中の譜面。中身は保存形式と同じ ChartData で持つ（保存・読み込みがそのままできる）。
    /// </summary>
    /// <remarks>
    /// 変更は必ずこのクラスのメソッドを通す。変更の前に中身を丸ごと控えておき、元に戻す・やり直すに使う。
    /// 譜面 1 つ分はノーツ数千個でも小さいので、差分ではなく丸ごと控える（そのほうが壊れにくい）。
    /// </remarks>
    public sealed class ChartDocument
    {
        private const int MaxUndo = 200;

        private readonly List<ChartData> _undo = new List<ChartData>();
        private readonly List<ChartData> _redo = new List<ChartData>();

        public ChartData Data { get; private set; }

        /// <summary>最後に保存（または読み込み）してから変更したか</summary>
        public bool IsDirty { get; private set; }

        public ChartDocument() : this(NewChart()) { }

        public ChartDocument(ChartData data)
        {
            Data = Normalize(ChartDataCopy.Clone(data));
        }

        /// <summary>何もない新しい譜面（BPM 120）</summary>
        public static ChartData NewChart() => new ChartData
        {
            version = ChartBuilder.SupportedVersion,
            song = "",
            difficulty = "stl",
            level = 1,
            offsetMs = 0,
            bpm = new[] { new BpmChangeData { beat = 0, value = 120 } },
            notes = Array.Empty<NoteData>(),
        };

        public IReadOnlyList<NoteData> Notes => Data.notes;

        // ===== 変更 =====

        /// <summary>
        /// 中身を変える。change の中で Data を書き換える。変更の前の状態が元に戻す用に控えられる。
        /// </summary>
        public void Edit(Action<ChartData> change)
        {
            var before = ChartDataCopy.Clone(Data);
            change(Data);
            Data = Normalize(Data);
            _undo.Add(before);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
            _redo.Clear();
            IsDirty = true;
        }

        public void AddNote(NoteData note) => Edit(d => d.notes = d.notes.Append(note).ToArray());

        /// <summary>index 番目のノーツを消す（index は Notes での位置）</summary>
        public void RemoveNote(int index) => Edit(d => d.notes = d.notes.Where((_, i) => i != index).ToArray());

        /// <summary>index 番目のノーツを書き換える。change にはそのノーツが渡される。</summary>
        public void ChangeNote(int index, Action<NoteData> change) => Edit(d => change(d.notes[index]));

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Undo()
        {
            if (!CanUndo) return;
            _redo.Add(Data);
            Data = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            IsDirty = true;
        }

        public void Redo()
        {
            if (!CanRedo) return;
            _undo.Add(Data);
            Data = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            IsDirty = true;
        }

        /// <summary>保存したときに呼ぶ</summary>
        public void MarkSaved() => IsDirty = false;

        /// <summary>試しプレイ用に Chart にする。譜面がおかしければ ChartFormatException。</summary>
        public Chart Build() => ChartBuilder.Build(ChartDataCopy.Clone(Data));

        // ノーツを拍の順に並べ、空の配列を埋める（表示や当たり判定を簡単にするため）
        private static ChartData Normalize(ChartData d)
        {
            d.version = ChartBuilder.SupportedVersion;
            d.bpm = (d.bpm ?? Array.Empty<BpmChangeData>()).OrderBy(b => b.beat).ToArray();
            d.notes = (d.notes ?? Array.Empty<NoteData>()).OrderBy(n => n.beat).ToArray();
            foreach (var n in d.notes)
            {
                if (n.type == "trail" && n.points != null)
                    n.points = n.points.OrderBy(p => p.beat).ToArray();
            }
            return d;
        }
    }

    /// <summary>ChartData を丸ごとコピーする（元に戻す用の控えに使う）</summary>
    public static class ChartDataCopy
    {
        public static ChartData Clone(ChartData d) => new ChartData
        {
            version = d.version,
            song = d.song,
            difficulty = d.difficulty,
            level = d.level,
            offsetMs = d.offsetMs,
            bpm = d.bpm?.Select(b => new BpmChangeData { beat = b.beat, value = b.value }).ToArray(),
            notes = d.notes?.Select(Clone).ToArray(),
        };

        public static NoteData Clone(NoteData n) => new NoteData
        {
            type = n.type,
            beat = n.beat,
            lane = n.lane,
            width = n.width,
            length = n.length,
            x = n.x,
            size = n.size,
            endFlick = n.endFlick,
            points = n.points?.Select(p => new TrailPointData { beat = p.beat, x = p.x, curve = p.curve }).ToArray(),
        };
    }
}
