using System;
using System.Collections.Generic;
using System.Linq;
using LunarTrails.Core.Charts;

namespace LunarTrails.Core.Judging
{
    /// <summary>
    /// マウスレーンの判定：クリック、トラック、フリック、トレイル。企画書「3. ノーツ仕様」と決定事項 1〜4 に対応。
    /// </summary>
    /// <remarks>
    /// - クリック：タイミングよくクリック。カーソルの位置と、左右どちらのボタンかは問わない。判定はタップと同じ 4 段階。
    /// - トラック：ノーツの時刻の前後 TrackWindow のうちに、カーソルが重なっていれば PERFECT+、なければ MISS。
    /// - フリック：判定幅（HIT）の中で、カーソルが FlickSpeedThreshold 以上の速さで動いた最初のフレームで判定。
    ///   方向と位置は問わない。ずれはそのフレームの時刻で測るので、フレーム単位（60fps で最大約 17ms）の誤差がある。
    /// - トレイル：次の 3 つに分けて判定する。
    ///   1. 始点：クリックノーツと同じ（タイミングで 4 段階、位置は問わない）
    ///   2. 途中：TrailTickBeats 拍ごとのチェック。ボタンを押したまま、カーソルが帯の中にあれば PERFECT+、なければ MISS。
    ///      トレイル上のクリックは、押さえていない方のボタンで叩く（左右どちらのボタンでも押さえられ、どちらでもクリックになる）。
    ///      指が一瞬浮いたときのために、離してから TrailReleaseGrace 秒以内は押したままとみなす。
    ///   3. 終点のフリック（endFlick のときだけ）：普通のフリックと同じ
    ///   どれも 1 つの判定として数える（コンボや判定の数に入る）。
    ///
    /// トラック、フリック、トレイルの途中は、毎フレーム Update を呼んで、そのフレームのカーソルの状態で判定する。
    /// </remarks>
    public sealed class MouseLaneJudge
    {
        /// <summary>トレイルの途中のチェック 1 回分</summary>
        private readonly struct TrailTick
        {
            public readonly Note Trail;
            public readonly double Time;

            public TrailTick(Note trail, double time)
            {
                Trail = trail;
                Time = time;
            }
        }

        private readonly JudgeWindows _windows;
        private readonly MouseJudgeSettings _settings;
        private readonly List<Note> _clicks;          // クリックとトレイルの始点（時刻順）
        private readonly List<Note> _positional;      // トラックとフリック、トレイル終点のフリック（時刻順）
        private readonly List<TrailTick> _trailTicks; // トレイルの途中のチェック（時刻順）
        private double _lastHeldTime = double.NegativeInfinity; // 最後にボタンが押されていたゲームの時刻

        public MouseLaneJudge(Chart chart, JudgeWindows windows = null, MouseJudgeSettings settings = null)
        {
            _windows = windows ?? JudgeWindows.Default;
            _settings = settings ?? MouseJudgeSettings.Default;

            var clicks = new List<Note>();
            var positional = new List<Note>();
            var ticks = new List<TrailTick>();

            foreach (var note in chart.Notes)
            {
                switch (note.Type)
                {
                    case NoteType.Click:
                        clicks.Add(note);
                        break;
                    case NoteType.Track:
                    case NoteType.Flick:
                        positional.Add(note);
                        break;
                    case NoteType.Trail:
                        clicks.Add(note);
                        ticks.AddRange(CreateTicks(chart, note));
                        if (note.EndFlick) positional.Add(CreateEndFlick(note));
                        break;
                }
            }

            // 終点のフリックが入ったので並べ直す（同じ時刻なら元の順を保つ）
            _clicks = clicks.OrderBy(n => n.Time).ToList();
            _positional = positional.OrderBy(n => n.Time).ToList();
            _trailTicks = ticks.OrderBy(t => t.Time).ToList();
        }

        public int PendingCount => _clicks.Count + _positional.Count + _trailTicks.Count;

        /// <summary>クリックされたときに呼ぶ。判定したノーツがなければ null（空クリック）。</summary>
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
        /// 毎フレーム呼ぶ。トラック、フリック、トレイルの途中の判定と、全種類の見逃し（MISS）を返す。
        /// </summary>
        /// <param name="gameTime">このフレームのゲームの時刻</param>
        /// <param name="cursorX">カーソルのマウスレーン内の位置（0 = 左端、1 = 右端）</param>
        /// <param name="cursorSpeed">カーソルの速さ（1 秒にマウスレーン何本分か）</param>
        /// <param name="buttonHeld">マウスのボタン（左右どちらか）を押しているか。トレイルの途中の判定に使う</param>
        public List<JudgeResult> Update(double gameTime, float cursorX, float cursorSpeed, bool buttonHeld = false)
        {
            var results = new List<JudgeResult>();
            if (buttonHeld) _lastHeldTime = gameTime;
            bool heldWithGrace = gameTime - _lastHeldTime <= _settings.TrailReleaseGrace;

            JudgePositional(gameTime, cursorX, cursorSpeed, results);
            JudgeTrailTicks(gameTime, cursorX, heldWithGrace, results);

            while (_clicks.Count > 0 && _windows.IsTooLate(gameTime - _clicks[0].Time))
            {
                results.Add(new JudgeResult(_clicks[0], Judgement.Miss, null));
                _clicks.RemoveAt(0);
            }
            return results;
        }

        private void JudgePositional(double gameTime, float cursorX, float cursorSpeed, List<JudgeResult> results)
        {
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
                    else if (Math.Abs(cursorX - note.X) <= note.Size / 2f + _settings.TrackMargin)
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
        }

        private void JudgeTrailTicks(double gameTime, float cursorX, bool buttonHeld, List<JudgeResult> results)
        {
            double window = _settings.TrailTickWindow;
            for (int i = 0; i < _trailTicks.Count; i++)
            {
                var tick = _trailTicks[i];
                double offset = gameTime - tick.Time;
                if (offset < -window) break; // ここから先はまだ早い

                JudgeResult? result = null;
                if (offset > window)
                {
                    result = new JudgeResult(tick.Trail, Judgement.Miss, null);
                }
                else if (buttonHeld)
                {
                    // 帯の位置は、チェックの時刻ではなく「今」の位置で比べる（今のカーソルと見た目の帯を比べるため）
                    float pathX = TrailPath.XAt(tick.Trail, gameTime);
                    if (Math.Abs(cursorX - pathX) <= tick.Trail.Size / 2f + _settings.TrailMargin)
                        result = new JudgeResult(tick.Trail, Judgement.PerfectPlus, null);
                }

                if (result.HasValue)
                {
                    results.Add(result.Value);
                    _trailTicks.RemoveAt(i);
                    i--;
                }
            }
        }

        // 始点から TrailTickBeats 拍ごとに、終点までチェックを置く（始点はクリックで判定するので含めない。終点は含める）
        private IEnumerable<TrailTick> CreateTicks(Chart chart, Note trail)
        {
            double startBeat = trail.Beat;
            double endBeat = chart.Tempo.SecondsToBeat(trail.EndTime - chart.OffsetSeconds);
            double step = _settings.TrailTickBeats;

            for (double beat = startBeat + step; beat < endBeat - 1e-6; beat += step)
                yield return new TrailTick(trail, chart.OffsetSeconds + chart.Tempo.BeatToSeconds(beat));
            yield return new TrailTick(trail, trail.EndTime);
        }

        // 終点のフリックを、普通のフリックノーツとして作る（判定結果の Note はこれになる）
        private static Note CreateEndFlick(Note trail)
        {
            var last = trail.TrailPoints[trail.TrailPoints.Count - 1];
            return new Note
            {
                Index = trail.Index,
                Type = NoteType.Flick,
                Time = trail.EndTime,
                EndTime = trail.EndTime,
                X = last.X,
                Size = trail.Size,
            };
        }
    }
}
