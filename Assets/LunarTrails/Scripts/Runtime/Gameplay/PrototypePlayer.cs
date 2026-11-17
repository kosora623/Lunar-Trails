using System.Collections.Generic;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Judging;
using LunarTrails.Core.Scoring;
using LunarTrails.Core.Settings;
using LunarTrails.Core.Timing;
using LunarTrails.Runtime.Audio;
using LunarTrails.Runtime.Charts;
using LunarTrails.Runtime.Input;
using LunarTrails.Runtime.Settings;
using LunarTrails.Runtime.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarTrails.Runtime.Gameplay
{
    /// <summary>
    /// 試作用のプレイ画面：譜面を読み込んで曲を鳴らし、ノーツを流して判定する。
    /// 見た目は仮（Unity 標準の立方体と文字だけ）。選曲と設定は PrototypeMenu が受け持つ。
    /// </summary>
    /// <remarks>
    /// 試作シーンの組み立て方：
    /// 1. シーンに空の GameObject を置き、PrototypePlayer を付ける（必要な部品は自動で付く）。
    /// 2. Songs に曲を登録する。1 曲ごとに、曲名・音声（空でもよい）・譜面（難易度の数だけ）を入れる。
    ///    Songs が空のときは、Chart Json に入れた譜面 1 つと、AudioSource の AudioClip を 1 曲として使う。
    /// 3. 再生すると選曲画面が出る。曲と難易度を選んで Enter で開始。
    ///    プレイ中は S D F J K L で叩き、マウスでカーソルを動かす・クリックする・払う。Esc で選曲に戻る。
    ///
    /// サンプル譜面は BPM 120・オフセット 0 なので、曲を入れるなら頭から一定の BPM 120 の曲を使う。
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
        /// <summary>選曲画面に並べる 1 曲分。</summary>
        [System.Serializable]
        public sealed class SongEntry
        {
            public string title;
            [Tooltip("曲の音声。空でも遊べる（叩く音だけで確認できる）")]
            public AudioClip audio;
            [Tooltip("この曲の譜面（難易度ごとに 1 つ）")]
            public TextAsset[] charts;
        }

        [Tooltip("選曲画面に並べる曲")]
        [SerializeField] private SongEntry[] _songs = new SongEntry[0];

        [Tooltip("Songs が空のときだけ使う譜面（以前の試作シーンとの互換用）")]
        [SerializeField] private TextAsset _chartJson;

        [Tooltip("ON にすると、全ノーツをちょうどの時刻に自動で叩く（譜面と曲のずれ確認用）")]
        [SerializeField] private bool _autoPlay;

        [Tooltip("再生を始めてから曲が始まるまでの秒数")]
        [SerializeField] private double _leadInSeconds = 1.5;

        [Tooltip("最後のノーツから終了までの秒数")]
        [SerializeField] private double _endPaddingSeconds = 2.0;

        [Tooltip("判定の厳しさ（仮の値。試遊で決める）")]
        [SerializeField] private JudgeTuning _judgeTuning = new JudgeTuning();

        /// <summary>
        /// 試作中に Inspector から判定の厳しさを変えるための値。初期値は Core 側の初期値と同じ。
        /// </summary>
        [System.Serializable]
        private sealed class JudgeTuning
        {
            [Header("判定幅（ミリ秒）")]
            public float perfectPlusMs = (float)(JudgeWindows.Default.PerfectPlus * 1000);
            public float perfectMs = (float)(JudgeWindows.Default.Perfect * 1000);
            public float hitMs = (float)(JudgeWindows.Default.Hit * 1000);

            [Header("トラック")]
            [Tooltip("ノーツの時刻の前後この秒数のうちに重なれば成功")]
            public float trackWindow = (float)MouseJudgeSettings.Default.TrackWindow;
            [Tooltip("ノーツの幅の外側に持たせる余裕（マウスレーンの幅 = 1）")]
            public float trackMargin = MouseJudgeSettings.Default.TrackMargin;

            [Header("フリック")]
            [Tooltip("成功とみなす速さ（1 秒にマウスレーン何本分動いたか）")]
            public float flickSpeedThreshold = MouseJudgeSettings.Default.FlickSpeedThreshold;

            [Header("トレイル")]
            [Tooltip("途中のチェックの、前後の猶予（秒）")]
            public float trailTickWindow = (float)MouseJudgeSettings.Default.TrailTickWindow;
            [Tooltip("帯の幅の外側に持たせる余裕（マウスレーンの幅 = 1）")]
            public float trailMargin = MouseJudgeSettings.Default.TrailMargin;
            [Tooltip("ボタンを離しても、この秒数以内なら押したままとみなす（指が一瞬浮いたとき用。長くしすぎると同じボタンで押し直せてしまう）")]
            public float trailReleaseGrace = (float)MouseJudgeSettings.Default.TrailReleaseGrace;

            public JudgeWindows ToWindows() => new JudgeWindows(perfectPlusMs / 1000.0, perfectMs / 1000.0, hitMs / 1000.0);

            public MouseJudgeSettings ToMouseSettings() => new MouseJudgeSettings
            {
                TrackWindow = trackWindow,
                TrackMargin = trackMargin,
                FlickSpeedThreshold = flickSpeedThreshold,
                TrailTickWindow = trailTickWindow,
                TrailMargin = trailMargin,
                TrailReleaseGrace = trailReleaseGrace,
            };
        }

        // オートプレイの叩く音を、どれだけ先まで予約しておくか（秒）
        private const double HitSoundLookAhead = 0.2;

        private AudioSource _music;
        private AudioSourceSongClock _clock;
        private KeyLaneInputReader _input;
        private MouseLaneInputReader _mouse;
        private HitSoundPlayer _hitSound;
        private PlayfieldView _view;
        private PrototypeHud _hud;

        private Chart _chart;
        private PlayerOffset _offset = new PlayerOffset();
        private GameSettings _settings;

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

        /// <summary>今プレイ中か（リザルト表示中も含む）。false のときは選曲・設定画面。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>プレイが終わってメニューに戻るときに呼ばれる。</summary>
        public event System.Action ReturnedToMenu;

        public bool AutoPlayEnabled
        {
            get => _autoPlay;
            set => _autoPlay = value;
        }

        /// <summary>選曲画面に並べる曲。Songs が空なら、Chart Json と AudioSource の音声を 1 曲として返す。</summary>
        public IReadOnlyList<SongEntry> Songs
        {
            get
            {
                if (_songs != null && _songs.Length > 0) return _songs;
                if (_chartJson == null) return new SongEntry[0];
                var music = GetComponent<AudioSource>();
                return new[] { new SongEntry { title = _chartJson.name, audio = music != null ? music.clip : null, charts = new[] { _chartJson } } };
            }
        }

        private void Awake()
        {
            _music = GetComponent<AudioSource>();
            _clock = GetComponent<AudioSourceSongClock>();
            _input = GetComponent<KeyLaneInputReader>();
            // RequireComponent は、すでにシーンに置いてある PrototypePlayer には後から部品を足さないので、ここで補う
            _mouse = GetComponent<MouseLaneInputReader>();
            if (_mouse == null) _mouse = gameObject.AddComponent<MouseLaneInputReader>();
            _hitSound = GetComponent<HitSoundPlayer>();
            _view = GetComponent<PlayfieldView>();
            _hud = GetComponent<PrototypeHud>();
            if (GetComponent<PrototypeMenu>() == null) gameObject.AddComponent<PrototypeMenu>();

            ApplySettings(SettingsStore.Load());
        }

        /// <summary>設定を反映する（設定画面で値を変えたときにも呼ばれる）。保存はしない。</summary>
        public void ApplySettings(GameSettings settings)
        {
            _settings = settings;
            _offset.Milliseconds = settings.offsetMs;
            _view.ScrollSpeed = settings.scrollSpeed;
            _mouse.PixelsPerLane = settings.pixelsPerLane;
            _input.SetLaneKeys(settings.laneKeys);
        }

        public GameSettings CurrentSettings => _settings;

        private void Start()
        {
            _view.BuildStage(Camera.main);
            _mouse.CaptureCursor = false;
        }

        /// <summary>選曲画面から呼ぶ：指定した譜面と曲で遊び始める。</summary>
        public void Play(Chart chart, AudioClip audio)
        {
            _chart = chart;
            _music.clip = audio;
            _hitSoundTimes = CollectHitSoundTimes(_chart);
            _endTime = 0;
            foreach (var note in _chart.Notes) _endTime = System.Math.Max(_endTime, note.EndTime);

            Debug.Log($"開始: {_chart.Song} [{_chart.Difficulty}] ノーツ {_chart.Notes.Count} 個");
            IsPlaying = true;
            _mouse.CaptureCursor = true;
            StartPlay();
        }

        /// <summary>プレイをやめて選曲画面に戻る。</summary>
        public void ReturnToMenu()
        {
            _clock.Stop();
            _hitSound.StopAll();
            _view.ClearNotes();
            _hud.Stats = null;
            _hud.Result = null;
            _hud.ClearJudgement();
            _mouse.CaptureCursor = false;
            IsPlaying = false;
            ReturnedToMenu?.Invoke();
        }

        /// <summary>最初から遊ぶ（R キーでも呼ばれる）。</summary>
        private void StartPlay()
        {
            _clock.Stop();
            _hitSound.StopAll();
            _input.Clear();
            _mouse.Clear();

            var windows = _judgeTuning.ToWindows();
            _judge = new KeyLaneJudge(_chart, windows);
            _mouseJudge = new MouseLaneJudge(_chart, windows, _judgeTuning.ToMouseSettings());
            _previousGameTime = double.NegativeInfinity;
            _autoPlayer = new AutoPlay(_chart);
            _stats = new JudgeStats();
            _judgedNotes.Clear();
            _nextHitSound = 0;
            _hud.ClearJudgement();
            _hud.Result = null;
            _view.BuildNotes(_chart);

            _clock.Play(_leadInSeconds);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            HandleDebugKeys();
            if (!IsPlaying) return; // Esc でメニューに戻った

            double songTime = _clock.SongTime;
            double gameTime = _offset.ToGameTime(songTime);
            float cursorX, cursorSpeed;
            bool buttonHeld;

            if (_autoPlay)
            {
                ScheduleAutoPlayHitSounds(songTime);
                foreach (var press in _autoPlayer.Collect(gameTime))
                    Apply(_judge.OnKeyDown(press.Lane, press.Time), playSound: false);
                foreach (var clickTime in _autoPlayer.CollectClicks(gameTime))
                    Apply(_mouseJudge.OnClick(clickTime), playSound: false);
                cursorX = _autoPlayer.CursorX(gameTime);
                cursorSpeed = _autoPlayer.CursorSpeed(_previousGameTime, gameTime);
                buttonHeld = _autoPlayer.IsHolding(gameTime);

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
                buttonHeld = _mouse.IsButtonHeld;
            }

            // トラック、フリック、トレイルの途中は、このフレームのカーソルの状態で判定する
            foreach (var result in _mouseJudge.Update(gameTime, cursorX, cursorSpeed, buttonHeld))
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
            // 重ねるだけのトラックと、トレイルの途中のチェック（ずれを持たない判定）では音を鳴らさない
            bool isTimingJudgement = r.OffsetSeconds.HasValue;
            if (playSound && r.Judgement != Judgement.Miss && isTimingJudgement) _hitSound.PlayNow();
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
                    || note.Type == NoteType.Click || note.Type == NoteType.Flick || note.Type == NoteType.Trail;
                if (!sounds) continue;
                times.Add(note.Time);
                if (note.Type == NoteType.Trail && note.EndFlick) times.Add(note.EndTime); // 終点のフリック
            }

            // 同じ時刻の音は 1 回だけ鳴らす
            times.Sort();
            var unique = new List<double>();
            foreach (var t in times)
            {
                if (unique.Count == 0 || t - unique[unique.Count - 1] >= 0.001) unique.Add(t);
            }
            return unique;
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

            // Esc はプレイ中ならやめて戻る。リザルト表示中は Enter でも戻れる
            bool showingResult = _hud.Result != null;
            if (keyboard.escapeKey.wasPressedThisFrame || (showingResult && keyboard.enterKey.wasPressedThisFrame))
                ReturnToMenu();
        }

        // プレイ中のオフセット変更も、設定として保存する
        private void ChangeOffset(int deltaMs)
        {
            _offset.Milliseconds += deltaMs;
            _settings.offsetMs = _offset.Milliseconds;
            SettingsStore.Save(_settings);
        }

        private void UpdateHud(double gameTime, float cursorSpeed)
        {
            _hud.CursorSpeed = cursorSpeed;
            _hud.FlickSpeedThreshold = _judgeTuning.flickSpeedThreshold;
            _hud.Stats = _stats;
            _hud.Title = $"{_chart.Song} [{_chart.Difficulty}] Lv.{_chart.Level}";
            _hud.AutoPlay = _autoPlay;
            _hud.OffsetMs = _offset.Milliseconds;
            // 最後のノーツから少し待ったら、リザルトを出す（1 回だけ計算する）
            if (_hud.Result == null && gameTime > _endTime + _endPaddingSeconds)
            {
                var result = new PlayResult(_stats);
                _hud.Result = result;
                Debug.Log($"リザルト: 達成率 {result.AchievementRate:0.0000}% {PlayResult.LampLabel(result.Lamp)} 最大コンボ {result.MaxCombo}");
            }
        }
    }
}
