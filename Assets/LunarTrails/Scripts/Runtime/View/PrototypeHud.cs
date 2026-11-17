using LunarTrails.Core.Judging;
using LunarTrails.Core.Scoring;
using UnityEngine;

namespace LunarTrails.Runtime.View
{
    /// <summary>
    /// 試作用の仮の文字表示。Unity の OnGUI で、判定や設定値をそのまま文字で出す。
    /// UI 案が決まったら、本番の UI に置き換える。
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        private const float JudgementShowSeconds = 0.5f;

        private JudgeResult? _lastResult;
        private float _lastResultAt;
        private GUIStyle _bigStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _resultStyle;

        // PrototypePlayer が毎フレーム入れる値
        public JudgeStats Stats;
        public string Title = "";
        public bool AutoPlay;
        public int OffsetMs;
        /// <summary>曲が終わったら入る。入っている間はリザルトを表示する。</summary>
        public PlayResult Result;
        public float CursorSpeed;
        public float FlickSpeedThreshold;

        // カーソルの速さは毎フレーム大きく揺れるので、直近の最大値を少しずつ下げながら表示する
        private float _shownSpeed;

        public void ShowJudgement(JudgeResult result)
        {
            _lastResult = result;
            _lastResultAt = Time.unscaledTime;
        }

        public void ClearJudgement() => _lastResult = null;

        private void OnGUI()
        {
            if (_bigStyle == null)
            {
                _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, richText = true };
                _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.UpperLeft };
            }

            // 判定の文字（画面の中央少し下）
            if (_lastResult.HasValue && Time.unscaledTime - _lastResultAt < JudgementShowSeconds)
            {
                var r = _lastResult.Value;
                string text = r.Judgement switch
                {
                    Judgement.PerfectPlus => "PERFECT+",
                    Judgement.Perfect => "PERFECT",
                    Judgement.Hit => "HIT",
                    _ => "MISS",
                };
                if (r.OffsetSeconds.HasValue)
                    text += $"\n<size=20>{r.OffsetSeconds.Value * 1000:+0;-0;0}ms</size>";
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 120), text, _bigStyle);
            }

            if (Result != null)
            {
                DrawResult(Result);
                return;
            }

            if (Stats == null) return;

            // OnGUI は 1 フレームに複数回呼ばれるので、描画のときだけ更新する
            if (Event.current.type == EventType.Repaint)
                _shownSpeed = Mathf.Max(CursorSpeed, _shownSpeed - 20f * Time.unscaledDeltaTime);

            // 左上の情報
            string average = Stats.RecentAverageOffset.HasValue
                ? $"{Stats.RecentAverageOffset.Value * 1000:+0;-0;0}ms"
                : "-";
            string info =
                $"{Title}\n" +
                $"オートプレイ: {(AutoPlay ? "ON" : "OFF")}\n" +
                $"オフセット: {OffsetMs:+0;-0;0}ms（↑↓で1ms、Shift+↑↓で10ms）\n" +
                $"最近のずれの平均: {average}（+ は遅れ気味）\n" +
                $"\n" +
                $"PERFECT+ {Stats.Count(Judgement.PerfectPlus)}  PERFECT {Stats.Count(Judgement.Perfect)}  " +
                $"HIT {Stats.Count(Judgement.Hit)}  MISS {Stats.Count(Judgement.Miss)}\n" +
                $"コンボ {Stats.Combo}（最大 {Stats.MaxCombo}）\n" +
                $"カーソルの速さ {Mathf.Min(_shownSpeed, 99f):0.0}（フリック成功は {FlickSpeedThreshold:0.0} 以上）\n" +
                $"\n" +
                $"R: 最初からやり直す　Esc: 選曲に戻る\n";
            GUI.Label(new Rect(16, 16, 600, 400), info, _smallStyle);
        }

        // リザルト：画面中央に黒い箱を出し、その上に文字だけで並べる
        private void DrawResult(PlayResult r)
        {
            float width = 520, height = 420;
            var box = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            GUI.Box(box, GUIContent.none);

            string average = r.AverageOffset.HasValue ? $"{r.AverageOffset.Value * 1000:+0;-0;0}ms" : "-";
            string text =
                $"{Title}\n" +
                (AutoPlay ? "（オートプレイ）\n" : "\n") +
                $"\n" +
                $"<size=40>{r.AchievementRate:0.0000}%</size>\n" +
                $"{PlayResult.LampLabel(r.Lamp)}\n" +
                $"\n" +
                $"PERFECT+  {r.PerfectPlus}\n" +
                $"PERFECT   {r.Perfect}\n" +
                $"HIT       {r.Hit}\n" +
                $"MISS      {r.Miss}\n" +
                $"\n" +
                $"最大コンボ {r.MaxCombo} / {r.Total}\n" +
                $"FAST {r.FastCount}  SLOW {r.SlowCount}  ずれの平均 {average}\n" +
                $"\n" +
                $"R: もう一度　Enter: 選曲に戻る";
            _resultStyle.richText = true;
            GUI.Label(new Rect(box.x + 32, box.y + 24, width - 64, height - 48), text, _resultStyle);
        }
    }
}
