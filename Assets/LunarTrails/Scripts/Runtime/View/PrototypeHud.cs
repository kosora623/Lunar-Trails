using LunarTrails.Core.Judging;
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

        // PrototypePlayer が毎フレーム入れる値
        public JudgeStats Stats;
        public string Title = "";
        public bool AutoPlay;
        public int OffsetMs;
        public bool Finished;
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
                $"R: 最初からやり直す　Esc: マウスの固定を外す\n" +
                (Finished ? "\n― 終了 ―" : "");
            GUI.Label(new Rect(16, 16, 600, 400), info, _smallStyle);
        }
    }
}
