using System.Collections.Generic;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// キーレーン（6K）のタップ判定。キーが押された時刻とノーツを照らし合わせる。
    /// </summary>
    /// <remarks>
    /// 今の対象はタップと、ロングの始点だけ。
    /// - ロング：企画書では終点の判定なし。押し続けているかの判定は未実装（TODO）。
    /// - ガイド：操作なしの演出用なので判定しない。
    /// マウスレーンのノーツは別のクラス（未作成）で判定する予定。
    /// </remarks>
    public sealed class KeyLaneJudge
    {
        private readonly JudgeWindows _windows;
        private readonly List<Note> _pending; // まだ判定していないノーツ（時刻順）

        public KeyLaneJudge(Chart chart, JudgeWindows windows = null)
        {
            _windows = windows ?? JudgeWindows.Default;
            _pending = new List<Note>();
            foreach (var note in chart.Notes)
            {
                if (note.Type == NoteType.Tap || note.Type == NoteType.Long)
                    _pending.Add(note);
            }
        }

        /// <summary>まだ判定していないノーツの数</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// キーが押されたときに呼ぶ。そのレーンで判定できるノーツがあれば判定結果を返す。
        /// </summary>
        /// <param name="lane">押されたキーのレーン（0〜5）</param>
        /// <param name="songTime">押された瞬間の曲の再生位置（秒）。フレームの時刻ではなく、入力イベントの時刻から求めた値を渡す。</param>
        /// <returns>判定したノーツがなければ null（空押し）。</returns>
        public JudgeResult? OnKeyDown(int lane, double songTime)
        {
            // 判定幅の中にある、いちばん早いノーツを取る。
            // （近い方ではなく早い方を取るのは、密な譜面で手前のノーツを飛ばして奥を取ってしまわないため）
            for (int i = 0; i < _pending.Count; i++)
            {
                var note = _pending[i];
                double offset = songTime - note.Time;

                if (_windows.IsTooEarly(offset)) break; // これ以降はもっと先のノーツなので見なくてよい
                if (_windows.IsTooLate(offset)) continue; // 見逃し済み（CollectMisses で MISS になる）
                if (!note.CoversLane(lane)) continue;

                _pending.RemoveAt(i);
                return new JudgeResult(note, _windows.Classify(offset), offset);
            }
            return null;
        }

        /// <summary>
        /// 判定幅を過ぎても押されなかったノーツを MISS として取り出す。毎フレーム呼ぶ。
        /// </summary>
        public List<JudgeResult> CollectMisses(double songTime)
        {
            // _pending は時刻順なので、先頭から見て過ぎたものだけ取り出せばよい
            var misses = new List<JudgeResult>();
            while (_pending.Count > 0 && _windows.IsTooLate(songTime - _pending[0].Time))
            {
                misses.Add(new JudgeResult(_pending[0], Judgement.Miss, null));
                _pending.RemoveAt(0);
            }
            return misses;
        }
    }
}
