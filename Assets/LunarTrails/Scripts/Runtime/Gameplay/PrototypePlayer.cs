using System.Collections.Generic;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using LunarTrails.Core.Timing;
using LunarTrails.Runtime.Audio;
using LunarTrails.Runtime.Charts;
using LunarTrails.Runtime.Input;
using LunarTrails.Runtime.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarTrails.Runtime.Gameplay
{
    /// <summary>
    /// 試作用のプレイ画面：譜面を読み込んで曲を鳴らし、ノーツを流して 6K のタップを判定する。
    /// 見た目は仮（Unity 標準の立方体と文字だけ）。
    /// </summary>
    /// <remarks>
    /// 試作シーンの組み立て方：
    /// 1. シーンに空の GameObject を置き、PrototypePlayer を付ける（必要な部品は自動で付く）。
    /// 2. Chart Json に譜面（例：Charts/sample_stl.json）を入れる。
    /// 3. 曲を鳴らすなら AudioSource の AudioClip に曲を入れる。空でも動く（叩く音だけで確認できる）。
    /// 4. 再生する。S D F J K L で叩き、マウスでカーソルを動かす・クリックする・払う。
    ///    Auto Play を ON にすると自動で取る。Esc でマウスの固定を外し、ゲーム画面のクリックで戻す。
    ///
    /// サンプル譜面は BPM 120・オフセット 0 なので、曲を入れるなら頭から一定の BPM 120 の曲を使う。
    /// マウスノーツを試すなら sample_mouse.json を使う。
    ///
    /// 時刻の扱い：
    /// - 曲の再生位置 … AudioSourceSongClock が返す、音声側の正確な時刻
    /// - ゲームの時刻 … 曲の再生位置 − プレイヤーのオフセット。判定と表示はこちらで動く
    /// オートプレイの叩く音は「曲の再生位置」でノーツの時刻ちょうどに鳴らす。
    /// そのため、オートプレイで曲と叩く音がずれて聞こえたら、それは譜面の offsetMs か BPM のずれ。
    ///
    /// 本番のゲーム進行クラスができたら、このクラスは消すか置き換える。
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AudioSourceSongClock))]
    [RequireComponent(typeof(KeyLaneInputReader))]
    [RequireComponent(typeof(MouseLaneInputReader))]
    [RequireComponent(typeof(HitSoundPlayer))]
    [RequireComponent(typeof(PlayfieldView))]
    [RequireComponent(typeof(PrototypeHud))]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private const string OffsetPrefsKey = "LunarTrails.PlayerOffsetMs";

        [SerializeField] private TextAsset _chartJson;

        [Tooltip("ON にすると、全ノーツをちょうどの時刻に自動で叩く（譜面と曲のずれ確認用）")]
        [SerializeField] private bool _autoPlay;

        [Tooltip("再生を始めてから曲が始まるまでの秒数")]
        [SerializeField] private double _leadInSeconds = 1.5;

        [Tooltip("最後のノーツから終了までの秒数")]
        [SerializeField] private double _endPaddingSeconds = 2.0;

        [Header("マウスレーンの判定（仮の値。試遊で決める）")]
        [Tooltip("フリック成功とみなす速さ（1 秒にマウスレーン何本分動いたか）")]
        [SerializeField] private float _flickSpeedThreshold = 4f;

        [Tooltip("トラック：ノーツの幅の外側に持たせる余裕（マウスレーンの幅 = 1）")]
        [SerializeField] private float _trackMargin = 0.05f;

        // オートプレイの叩く音を、どれだけ先まで予約しておくか（秒）
        private const double HitSoundLookAhead = 0.2;

        private AudioSourceSongClock _clock;
        private KeyLaneInputReader _input;
        private MouseLaneInputReader _mouse;
        private HitSoundPlayer _hitSound;
        private PlayfieldView _view;
        private PrototypeHud _hud;

        private Chart _chart;
        private PlayerOffset _offset;

        // 1 回のプレイごとに作り直すもの
        private KeyLaneJudge _judge;
        private MouseLaneJudge _mouseJudge;
        private double _previousGameTime;
        private AutoPlay _autoPlayer;
        private JudgeStats _stats;
        private readonly HashSet<int> _judgedNotes = new HashSet<int>();
        private List<double> _hitSoundTimes;
        private int _nextHitSound;
        private double _endTime;

        private void Awake()
        {
            _clock = GetComponent<AudioSourceSongClock>();
            _input = GetComponent<KeyLaneInputReader>();
            // RequireComponent は、すでにシーンに置いてある PrototypePlayer には後から部品を足さないので、ここで補う
            _mouse = GetComponent<MouseLaneInputReader>();
            if (_mouse == null) _mouse = gameObject.AddComponent<MouseLaneInputReader>();
            _hitSound = GetComponent<HitSoundPlayer>();
            _view = GetComponent<PlayfieldView>();
            _hud = GetComponent<PrototypeHud>();
            _offset = new PlayerOffset(PlayerPrefs.GetInt(OffsetPrefsKey, 0));
        }

        private void Start()
        {
            if (_chartJson == null)
            {
                Debug.LogError("PrototypePlayer の Chart Json に譜面ファイルを入れてください。", this);
                enabled = false;
                return;
            }

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

            Debug.Log($"譜面を読み込みました: {_chart.Song} [{_chart.Difficulty}] ノーツ {_chart.Notes.Count} 個");

            _view.BuildStage(Camera.main);
            _hitSoundTimes = CollectHitSoundTimes(_chart);
            _endTime = 0;
            foreach (var note in _chart.Notes) _endTime = System.Math.Max(_endTime, note.EndTime);

            StartPlay();
        }

        /// <summary>最初から遊ぶ（R キーでも呼ばれる）。</summary>
        private void StartPlay()
        {
            _clock.Stop();
            _hitSound.StopAll();
            _input.Clear();
            _mouse.Clear();

            _judge = new KeyLaneJudge(_chart);
            _mouseJudge = new MouseLaneJudge(_chart, settings: new MouseJudgeSettings
            {
                FlickSpeedThreshold = _flickSpeedThreshold,
                TrackMargin = _trackMargin,
            });
            _previousGameTime = double.NegativeInfinity;
            _autoPlayer = new AutoPlay(_chart);
            _stats = new JudgeStats();
            _judgedNotes.Clear();
            _nextHitSound = 0;
            _hud.ClearJudgement();
            _view.BuildNotes(_chart);

            _clock.Play(_leadInSeconds);
        }

        private void Update()
        {
            HandleDebugKeys();

            double songTime = _clock.SongTime;
            double gameTime = _offset.ToGameTime(songTime);
            float cursorX, cursorSpeed;

            if (_autoPlay)
            {
                ScheduleAutoPlayHitSounds(songTime);
                foreach (var press in _autoPlayer.Collect(gameTime))
                    Apply(_judge.OnKeyDown(press.Lane, press.Time), playSound: false);
                foreach (var clickTime in _autoPlayer.CollectClicks(gameTime))
                    Apply(_mouseJudge.OnClick(clickTime), playSound: false);
                cursorX = _autoPlayer.CursorX(gameTime);
                cursorSpeed = _autoPlayer.CursorSpeed(_previousGameTime, gameTime);

                // オートプレイ中は手の入力を使わない
                _input.Clear();
                _mouse.DrainClicks();
            }
            else
            {
                // 入力イベントの時刻を、ゲームの時刻に直す。
                // 「今のゲームの時刻」から「押されてから今までの経過時間」を引く。
                double now = Time.realtimeSinceStartupAsDouble;
                foreach (var press in _input.DrainPresses())
                    Apply(_judge.OnKeyDown(press.Lane, gameTime - (now - press.RealTime)), playSound: true);
                foreach (var clickAt in _mouse.DrainClicks())
                    Apply(_mouseJudge.OnClick(gameTime - (now - clickAt)), playSound: true);
                cursorX = _mouse.CursorX;
                cursorSpeed = _mouse.Speed;
            }

            // トラックとフリックは、このフレームのカーソルの状態で判定する
            foreach (var result in _mouseJudge.Update(gameTime, cursorX, cursorSpeed))
                Apply(result, playSound: !_autoPlay);

            foreach (var miss in _judge.CollectMisses(gameTime))
                Apply(miss, playSound: false);

            _view.Render(gameTime, note => _judgedNotes.Contains(note.Index), lane => !_autoPlay && _input.IsHeld(lane), cursorX);
            UpdateHud(gameTime, cursorSpeed);
            _previousGameTime = gameTime;
        }

        private void Apply(JudgeResult? result, bool playSound)
        {
            if (!result.HasValue) return;
            var r = result.Value;

            _judgedNotes.Add(r.Note.Index);
            _stats.Add(r);
            _hud.ShowJudgement(r);
            // トラックは重ねるだけのノーツなので音は鳴らさない
            if (playSound && r.Judgement != Judgement.Miss && r.Note.Type != NoteType.Track) _hitSound.PlayNow();
        }

        // ===== オートプレイの叩く音 =====

        // 叩く音を鳴らす時刻の一覧（同じ時刻のノーツは 1 回だけ鳴らす）
        private static List<double> CollectHitSoundTimes(Chart chart)
        {
            var times = new List<double>();
            foreach (var note in chart.Notes)
            {
                // 叩く・クリックする・払うノーツで鳴らす（トラックとガイドは鳴らさない）
                bool sounds = note.Type == NoteType.Tap || note.Type == NoteType.Long
                    || note.Type == NoteType.Click || note.Type == NoteType.Flick;
                if (!sounds) continue;
                if (times.Count > 0 && System.Math.Abs(times[times.Count - 1] - note.Time) < 0.001) continue;
                times.Add(note.Time);
            }
            return times;
        }

        private void ScheduleAutoPlayHitSounds(double songTime)
        {
            while (_nextHitSound < _hitSoundTimes.Count && _hitSoundTimes[_nextHitSound] <= songTime + HitSoundLookAhead)
            {
                double t = _hitSoundTimes[_nextHitSound++];
                if (t < songTime) continue; // もう過ぎた音は鳴らさない
                _hitSound.PlayAt(_clock.SongTimeToDspTime(t));
            }
        }

        // ===== キー操作（試作用） =====

        private void HandleDebugKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            int step = keyboard.shiftKey.isPressed ? 10 : 1;
            if (keyboard.upArrowKey.wasPressedThisFrame) ChangeOffset(+step);
            if (keyboard.downArrowKey.wasPressedThisFrame) ChangeOffset(-step);
            if (keyboard.rKey.wasPressedThisFrame) StartPlay();
        }

        private void ChangeOffset(int deltaMs)
        {
            _offset.Milliseconds += deltaMs;
            PlayerPrefs.SetInt(OffsetPrefsKey, _offset.Milliseconds);
            PlayerPrefs.Save();
        }

        private void UpdateHud(double gameTime, float cursorSpeed)
        {
            _hud.CursorSpeed = cursorSpeed;
            _hud.FlickSpeedThreshold = _flickSpeedThreshold;
            _hud.Stats = _stats;
            _hud.Title = $"{_chart.Song} [{_chart.Difficulty}] Lv.{_chart.Level}";
            _hud.AutoPlay = _autoPlay;
            _hud.OffsetMs = _offset.Milliseconds;
            _hud.Finished = gameTime > _endTime + _endPaddingSeconds;
        }
    }
}
