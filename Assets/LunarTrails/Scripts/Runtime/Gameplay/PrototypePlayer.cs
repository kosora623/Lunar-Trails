using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using LunarTrails.Runtime.Audio;
using LunarTrails.Runtime.Charts;
using LunarTrails.Runtime.Input;
using UnityEngine;

namespace LunarTrails.Runtime.Gameplay
{
    /// <summary>
    /// 試作用：譜面を読み込んで曲を鳴らし、6K のタップを判定して Console に出す。
    /// ノーツの表示はまだない（UI 案が決まってから NoteView を作る）。
    /// </summary>
    /// <remarks>
    /// 使い方は README の「試作シーンの組み立て方」を参照。
    /// 本番のゲーム進行クラスができたら、このクラスは消すか置き換える。
    /// </remarks>
    public sealed class PrototypePlayer : MonoBehaviour
    {
        [SerializeField] private TextAsset _chartJson;
        [SerializeField] private AudioSourceSongClock _clock;
        [SerializeField] private KeyLaneInputReader _input;

        [Tooltip("再生ボタンを押してから曲が始まるまでの秒数")]
        [SerializeField] private double _leadInSeconds = 1.0;

        private Chart _chart;
        private KeyLaneJudge _judge;

        private void Start()
        {
            try
            {
                _chart = ChartLoader.FromTextAsset(_chartJson);
            }
            catch (ChartFormatException e)
            {
                Debug.LogError($"譜面を読み込めませんでした: {e.Message}", this);
                enabled = false;
                return;
            }

            _judge = new KeyLaneJudge(_chart);
            Debug.Log($"譜面を読み込みました: {_chart.Song} [{_chart.Difficulty}] ノーツ {_chart.Notes.Count} 個");

            _clock.Play(_leadInSeconds);
        }

        private void Update()
        {
            double songTime = _clock.SongTime;

            foreach (var press in _input.DrainPresses())
            {
                // 入力イベントの時刻を、曲の再生位置に直す。
                // 「今の再生位置」から「押されてから今までの経過時間」を引く。
                double pressedAt = songTime - (Time.realtimeSinceStartupAsDouble - press.RealTime);

                var result = _judge.OnKeyDown(press.Lane, pressedAt);
                if (result.HasValue) Debug.Log(result.Value.ToString());
            }

            foreach (var miss in _judge.CollectMisses(songTime))
            {
                Debug.Log(miss.ToString());
            }
        }
    }
}
