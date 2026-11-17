using System;
using System.Collections.Generic;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Flow;
using LunarTrails.Core.Records;
using LunarTrails.Core.Scoring;
using LunarTrails.Runtime.Charts;
using LunarTrails.Runtime.Gameplay;
using LunarTrails.Runtime.Records;
using LunarTrails.Runtime.Screens;
using LunarTrails.Runtime.Songs;
using UnityEngine;

namespace LunarTrails.Runtime.Flow
{
    /// <summary>
    /// ゲーム全体の進行役：タイトル → 選曲 → プレイ → リザルト（と設定）の画面を切り替え、
    /// 曲の一覧と自己ベストを持つ。プレイ画面そのものは PrototypePlayer が受け持つ。
    /// </summary>
    /// <remarks>
    /// シーンの組み立て方：
    /// 1. 空の GameObject に GameDirector を付ける（PrototypePlayer と、プレイに要る部品は自動で付く）。
    /// 2. Create → Lunar Trails → Song で曲のアセットを作り、Song Library に並べて、Library に入れる。
    ///    Library が空のときは、PrototypePlayer の Songs を代わりに使う（試作シーンからの引き継ぎ用）。
    ///
    /// 画面の差し替え方（デザインが決まったら）：
    /// 各画面は「今その画面のときだけ有効になる部品」になっている。Title Screen などの欄が空なら、
    /// 仮の画面（OnGUI の文字だけ）を自動で付ける。本番の画面を作ったら、その部品をこの欄に入れる。
    /// - 欄に入れた部品が GameDirector と同じ GameObject にあれば、部品の enabled を切り替える。
    /// - 別の GameObject（Canvas など）にあれば、その GameObject ごと表示・非表示にする。
    /// 本番の画面は GameScreenBase を継承すると、Director（このクラス）を通して曲の一覧を読み、
    /// StartSong / OpenSettings / Back などを呼ぶだけで仮の画面と同じように動く。
    /// </remarks>
    [RequireComponent(typeof(PrototypePlayer))]
    public sealed class GameDirector : MonoBehaviour
    {
        /// <summary>読み込み済みの 1 曲分（SongData の中身と、読み込んだ譜面）</summary>
        public sealed class LoadedSong
        {
            public string Id;
            public string Title;
            public string Artist;
            public string BpmText;
            public Texture2D Jacket;
            public AudioClip Audio;
            public float PreviewStart;
            public float PreviewLength;
            /// <summary>難易度の順（stl → inv）に並べた譜面</summary>
            public readonly List<Chart> Charts = new List<Chart>();
            /// <summary>読み込めなかった譜面のエラー</summary>
            public readonly List<string> Errors = new List<string>();
        }

        /// <summary>最後に遊んだ結果（リザルト画面で使う）</summary>
        public sealed class LastPlay
        {
            public LoadedSong Song;
            public Chart Chart;
            public PlayResult Result;
            /// <summary>記録の更新内容。オートプレイのときは記録しないので null。</summary>
            public RecordUpdate? Update;
            public bool WasAutoPlay;
        }

        /// <summary>難易度の並び順（決定事項：0〜4 = stl / ast / lnr / nov / inv）</summary>
        public static readonly string[] DifficultyOrder = { "stl", "ast", "lnr", "nov", "inv" };

        [Tooltip("選曲画面に並べる曲。空なら PrototypePlayer の Songs を使う")]
        [SerializeField] private SongLibrary _library;

        [Header("画面（空なら仮の画面を自動で付ける）")]
        [SerializeField] private Behaviour _titleScreen;
        [SerializeField] private Behaviour _songSelectScreen;
        [SerializeField] private Behaviour _settingsScreen;
        [SerializeField] private Behaviour _resultScreen;

        private PrototypePlayer _player;
        private readonly List<LoadedSong> _songs = new List<LoadedSong>();
        private LoadedSong _playingSong;
        private int _changedFrame;

        public GameFlow Flow { get; } = new GameFlow();
        public GameScreen Current => Flow.Current;
        public PrototypePlayer Player => _player;
        public IReadOnlyList<LoadedSong> Songs => _songs;
        public RecordBook Records { get; private set; }
        public LastPlay Last { get; private set; }

        /// <summary>選曲画面で選んでいる曲と譜面（画面を行き来しても覚えておく）</summary>
        public int SelectedSong { get; set; }
        public int SelectedChart { get; set; }

        public bool AutoPlay
        {
            get => _player.AutoPlayEnabled;
            set => _player.AutoPlayEnabled = value;
        }

        /// <summary>
        /// 画面が切り替わったフレームでは false。
        /// 前の画面で押した Enter などを、新しい画面が同じフレームで拾わないようにする。
        /// </summary>
        public bool AcceptsInput => Time.frameCount > _changedFrame;

        private void Awake()
        {
            _player = GetComponent<PrototypePlayer>();
            _player.ShowResultOnHud = false;
            _player.Finished += OnPlayFinished;
            _player.ReturnedToMenu += OnPlayQuit;

            // 試作の選曲画面は使わない
            var menu = GetComponent<PrototypeMenu>();
            if (menu != null) menu.enabled = false;

            if (_titleScreen == null) _titleScreen = gameObject.AddComponent<TitleScreen>();
            if (_songSelectScreen == null) _songSelectScreen = gameObject.AddComponent<SongSelectScreen>();
            if (_settingsScreen == null) _settingsScreen = gameObject.AddComponent<SettingsScreen>();
            if (_resultScreen == null) _resultScreen = gameObject.AddComponent<ResultScreen>();

            Records = RecordStore.Load();
            LoadSongs();

            Flow.Changed += (from, to) =>
            {
                _changedFrame = Time.frameCount;
                ShowScreenFor(to);
            };
            ShowScreenFor(Flow.Current);
        }

        // ===== 画面から呼ぶ操作 =====

        /// <summary>選曲画面へ（タイトルの「はじめる」、リザルトの「次へ」）</summary>
        public void GoToSongSelect() => Flow.Go(GameScreen.SongSelect);

        /// <summary>設定画面を開く（閉じると開く前の画面に戻る）</summary>
        public void OpenSettings() => Flow.Go(GameScreen.Settings);

        /// <summary>一つ前の画面へ戻る。プレイ中なら、やめて選曲画面へ。</summary>
        public void Back()
        {
            if (Current == GameScreen.Play)
            {
                _player.ReturnToMenu(); // OnPlayQuit で選曲画面へ移る
                return;
            }
            Flow.Back();
        }

        /// <summary>選んだ曲と譜面で遊び始める。</summary>
        public void StartSong(int songIndex, int chartIndex)
        {
            if (songIndex < 0 || songIndex >= _songs.Count) return;
            var song = _songs[songIndex];
            if (chartIndex < 0 || chartIndex >= song.Charts.Count) return;

            SelectedSong = songIndex;
            SelectedChart = chartIndex;
            _playingSong = song;
            Flow.Go(GameScreen.Play);
            _player.Play(song.Charts[chartIndex], song.Audio);
        }

        /// <summary>リザルト画面から、同じ譜面をもう一度遊ぶ。</summary>
        public void Retry()
        {
            if (Last == null) return;
            _playingSong = Last.Song;
            Flow.Go(GameScreen.Play);
            _player.Play(Last.Chart, Last.Song.Audio);
        }

        /// <summary>その譜面の自己ベスト。まだ遊んでいなければ null。</summary>
        public PlayRecord FindRecord(LoadedSong song, Chart chart)
        {
            return Records.Find(RecordBook.Key(song.Id, chart.Difficulty));
        }

        // ===== プレイの終わり =====

        private void OnPlayFinished(PlayResult result)
        {
            var chart = _player.CurrentChart;
            var last = new LastPlay { Song = _playingSong, Chart = chart, Result = result, WasAutoPlay = AutoPlay };
            if (!AutoPlay)
            {
                last.Update = Records.Submit(RecordBook.Key(_playingSong.Id, chart.Difficulty), result);
                RecordStore.Save(Records);
            }
            Last = last;

            _player.Stop();
            Flow.Go(GameScreen.Result);
        }

        // プレイ中に Esc でやめたとき
        private void OnPlayQuit()
        {
            if (Current == GameScreen.Play) Flow.Go(GameScreen.SongSelect);
        }

        // ===== 画面の切り替え =====

        private void ShowScreenFor(GameScreen screen)
        {
            SetVisible(_titleScreen, screen == GameScreen.Title);
            SetVisible(_songSelectScreen, screen == GameScreen.SongSelect);
            SetVisible(_settingsScreen, screen == GameScreen.Settings);
            SetVisible(_resultScreen, screen == GameScreen.Result);
            // プレイ画面は PrototypePlayer が自分で表示する（IsPlaying の間だけ）
        }

        private void SetVisible(Behaviour screen, bool visible)
        {
            if (screen == null) return;
            if (screen.gameObject == gameObject) screen.enabled = visible;
            else screen.gameObject.SetActive(visible);
        }

        // ===== 曲の読み込み =====

        private void LoadSongs()
        {
            _songs.Clear();
            if (_library != null && _library.songs != null && _library.songs.Length > 0)
            {
                foreach (var data in _library.songs)
                {
                    if (data == null) continue;
                    _songs.Add(Load(new LoadedSong
                    {
                        Id = data.SongId,
                        Title = string.IsNullOrEmpty(data.title) ? data.name : data.title,
                        Artist = data.artist,
                        BpmText = data.bpmText,
                        Jacket = data.jacket,
                        Audio = data.audio,
                        PreviewStart = data.previewStart,
                        PreviewLength = data.previewLength,
                    }, data.charts));
                }
            }
            else
            {
                // 試作シーンからの引き継ぎ：PrototypePlayer の Songs を使う（ID は曲名）
                foreach (var entry in _player.Songs)
                {
                    string title = string.IsNullOrEmpty(entry.title) ? "（曲名なし）" : entry.title;
                    _songs.Add(Load(new LoadedSong { Id = title, Title = title, Audio = entry.audio, PreviewLength = 15f }, entry.charts));
                }
            }
        }

        private static LoadedSong Load(LoadedSong song, TextAsset[] charts)
        {
            foreach (var asset in charts ?? new TextAsset[0])
            {
                if (asset == null) continue;
                try
                {
                    song.Charts.Add(ChartLoader.FromTextAsset(asset));
                }
                catch (ChartFormatException e)
                {
                    song.Errors.Add($"{asset.name}: {e.Message}");
                    Debug.LogError($"譜面を読み込めませんでした（{asset.name}）: {e.Message}");
                }
            }
            song.Charts.Sort((a, b) => DifficultyRank(a.Difficulty).CompareTo(DifficultyRank(b.Difficulty)));
            return song;
        }

        /// <summary>難易度の並び順。知らない難易度は最後。</summary>
        public static int DifficultyRank(string difficulty)
        {
            int index = Array.IndexOf(DifficultyOrder, difficulty);
            return index < 0 ? DifficultyOrder.Length : index;
        }
    }
}
