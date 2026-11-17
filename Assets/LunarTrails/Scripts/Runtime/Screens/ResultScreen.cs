using LunarTrails.Core.Scoring;

namespace LunarTrails.Runtime.Screens
{
    /// <summary>仮のリザルト画面。Enter か Esc で選曲へ、R でもう一度。</summary>
    public sealed class ResultScreen : GameScreenBase
    {
        private void Update()
        {
            var keyboard = ReadableKeyboard();
            if (keyboard == null) return;

            if (keyboard.rKey.wasPressedThisFrame) Director.Retry();
            else if (EnterPressed(keyboard) || keyboard.escapeKey.wasPressedThisFrame) Director.GoToSongSelect();
        }

        private void OnGUI()
        {
            var last = Director.Last;
            if (last == null) return;
            var r = last.Result;

            string record;
            if (last.WasAutoPlay) record = "オートプレイのため記録しません";
            else if (last.Update.Value.IsFirstPlay) record = "<b>NEW RECORD</b>（初プレイ）";
            else
            {
                var u = last.Update.Value;
                string rate = u.IsNewBestRate ? $"<b>NEW RECORD</b>　自己ベスト {u.PreviousBestRate:0.0000}% → {r.AchievementRate:0.0000}%" : $"自己ベスト {u.PreviousBestRate:0.0000}%";
                record = u.IsNewBestLamp ? $"{rate}　<b>ランプ更新</b>" : rate;
            }

            string average = r.AverageOffset.HasValue ? $"{r.AverageOffset.Value * 1000:+0.0;-0.0;0.0}ms" : "-";
            PlainGui.Text(
                "<size=32>リザルト</size>\n\n" +
                $"{last.Song.Title}　{last.Chart.Difficulty} Lv.{last.Chart.Level}\n\n" +
                $"<size=40>{r.AchievementRate:0.0000}%</size>\n" +
                $"{PlayResult.LampLabel(r.Lamp)}\n" +
                $"{record}\n\n" +
                $"PERFECT+ {r.PerfectPlus}\n" +
                $"PERFECT  {r.Perfect}\n" +
                $"HIT      {r.Hit}\n" +
                $"MISS     {r.Miss}\n\n" +
                $"最大コンボ {r.MaxCombo} / {r.Total}\n" +
                $"FAST {r.FastCount}　SLOW {r.SlowCount}　平均のずれ {average}\n\n" +
                "Enter: 選曲へ　R: もう一度");
        }
    }
}
