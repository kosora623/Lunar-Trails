using System;
using System.Collections.Generic;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// マウスレーンの判定：クリック、トラック、フリック。企画書「3. ノーツ仕様」と決定事項 1〜4 に対応。
    /// </summary>
    /// <remarks>
    /// - クリック：タイミングよくクリック。カーソルの位置と、左右どちらのボタンかは問わない。判定はタップと同じ 4 段階。
    /// - トラック：ノーツの時刻の前後 TrackWindow のうちに、カーソルが重なっていれば PERFECT+、なければ MISS。
    /// - フリック：判定幅（HIT）の中で、カーソルが FlickSpeedThreshold 以上の速さで動いた最初のフレームで判定。
    ///   方向と位置は問わない。ずれはそのフレームの時刻で測るので、フレーム単位（60fps で最大約 17ms）の誤差がある。
    /// - トレイル：未実装（判定しない）。
    ///
    /// トラックとフリックは毎フレーム Update を呼んで、そのフレームのカーソルの状態で判定する。
    /// </remarks>
    public sealed class MouseLaneJudge
    {
        private readonly JudgeWindows _windows;
        private readonly MouseJudgeSettings _settings;
        private readonly List<Note> _clicks = new List<Note>();     // 時刻順
        private readonly List<Note> _positional = new List<Note>(); // トラックとフリック（時刻順）

        public MouseLaneJudge(Chart chart, JudgeWindows windows = null, MouseJudgeSettings settings = null)
        {
            _windows = windows ?? JudgeWindows.Default;
            _settings = settings ?? MouseJudgeSettings.Default;
            foreach (var note in chart.Notes)
            {
                if (note.Type == NoteType.Click) _clicks.Add(note);
                else if (note.Type == NoteType.Track || note.Type == NoteType.Flick) _positional.Add(note);
            }
        }

        public int PendingCount => _clicks.Count + _positional.Count;

        /// <summary>クリックされたときに呼ぶ。判定したクリックノーツがなければ null（空クリック）。</summary>
        /// <param name="time">クリックした瞬間のゲームの時刻（入力イベントの時刻から求めた値）</param>
        public JudgeResult? OnClick(double time)
        {
            for (int i = 0; i < _clicks.Count; i++)
            {
                double offset = time - _clicks[i].Time;
                if (_windows.IsTooEarly(offset)) break;
                if (_windows.IsTooLate(offset)) continue;

                var note = _clicks[i];
                _clicks.RemoveAt(i);
                return new JudgeResult(note, _windows.Classify(offset), offset);
            }
            return null;
        }

        /// <summary>
        /// 毎フレーム呼ぶ。トラックとフリックの判定と、全種類の見逃し（MISS）を返す。
        /// </summary>
        /// <param name="gameTime">このフレームのゲームの時刻</param>
        /// <param name="cursorX">カーソルのマウスレーン内の位置（0 = 左端、1 = 右端）</param>
        /// <param name="cursorSpeed">カーソルの速さ（1 秒にマウスレーン何本分か）</param>
        public List<JudgeResult> Update(double gameTime, float cursorX, float cursorSpeed)
        {
            var results = new List<JudgeResult>();
            bool flickUsed = false; // 1 回の払いで 2 つのフリックを取らないようにする

            for (int i = 0; i < _positional.Count; i++)
            {
                var note = _positional[i];
                double offset = gameTime - note.Time;
                JudgeResult? result = null;

                if (note.Type == NoteType.Track)
                {
                    if (offset < -_settings.TrackWindow) break; // ここから先はまだ早い
                    if (offset > _settings.TrackWindow)
                        result = new JudgeResult(note, Judgement.Miss, null);
                    else if (Overlaps(note, cursorX))
                        result = new JudgeResult(note, Judgement.PerfectPlus, null);
                }
                else // Flick
                {
                    if (_windows.IsTooEarly(offset)) break;
                    if (_windows.IsTooLate(offset))
                        result = new JudgeResult(note, Judgement.Miss, null);
                    else if (!flickUsed && cursorSpeed >= _settings.FlickSpeedThreshold)
                    {
                        result = new JudgeResult(note, _windows.Classify(offset), offset);
                        flickUsed = true;
                    }
                }

                if (result.HasValue)
                {
                    results.Add(result.Value);
                    _positional.RemoveAt(i);
                    i--;
                }
            }

            while (_clicks.Count > 0 && _windows.IsTooLate(gameTime - _clicks[0].Time))
            {
                results.Add(new JudgeResult(_clicks[0], Judgement.Miss, null));
                _clicks.RemoveAt(0);
            }

            return results;
        }

        private bool Overlaps(Note note, float cursorX) =>
            Math.Abs(cursorX - note.X) <= note.Size / 2f + _settings.TrackMargin;
    }
}
