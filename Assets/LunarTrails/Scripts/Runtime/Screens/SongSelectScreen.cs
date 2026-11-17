using System.Collections.Generic;
using LunarTrails.Core.Scoring;
using LunarTrails.Runtime.Flow;
using UnityEngine;

namespace LunarTrails.Runtime.Screens
{
    /// <summary>
    /// 仮の選曲画面。↑↓ で曲、←→ で難易度、Enter で開始、A でオートプレイ、Tab で設定、Esc でタイトルへ。
    /// 選んでいる曲の試聴を流し、譜面ごとの自己ベストを出す。
    /// </summary>
    public sealed class SongSelectScreen : GameScreenBase
    {
        private AudioSource _preview;
        private GameDirector.LoadedSong _previewing;

        protected override void Awake()
        {
            base.Awake();
            // 曲の再生用の AudioSource（PrototypePlayer が使う）とは別に、試聴用のものを子に作る
            var child = new GameObject("Preview");
            child.transform.SetParent(transform, false);
            _preview = child.AddComponent<AudioSource>();
            _preview.playOnAwake = false;
            _preview.loop = true;
        }

        private void OnEnable()
        {
            _previewing = null; // 戻ってきたら試聴を流し直す
        }

        private void OnDisable()
        {
            if (_preview != null) _preview.Stop();
            _previewing = null;
        }

        private void Update()
        {
            var songs = Director.Songs;
            UpdatePreview(songs.Count > 0 ? songs[Director.SelectedSong] : null);

            var keyboard = ReadableKeyboard();
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) { Director.Back(); return; }
            if (keyboard.tabKey.wasPressedThisFrame) { Director.OpenSettings(); return; }
            if (keyboard.aKey.wasPressedThisFrame) Director.AutoPlay = !Director.AutoPlay;
            if (songs.Count == 0) return;

            if (keyboard.upArrowKey.wasPressedThisFrame) MoveSong(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame) MoveSong(+1);

            var song = songs[Director.SelectedSong];
            if (song.Charts.Count == 0) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame) Director.SelectedChart = Mathf.Max(0, Director.SelectedChart - 1);
            if (keyboard.rightArrowKey.wasPressedThisFrame) Director.SelectedChart = Mathf.Min(song.Charts.Count - 1, Director.SelectedChart + 1);

            if (EnterPressed(keyboard)) Director.StartSong(Director.SelectedSong, Director.SelectedChart);
        }

        // 曲を移っても、できるだけ同じ難易度を選んだままにする
        private void MoveSong(int delta)
        {
            var songs = Director.Songs;
            var current = songs[Director.SelectedSong];
            string difficulty = Director.SelectedChart < current.Charts.Count ? current.Charts[Director.SelectedChart].Difficulty : null;

            Director.SelectedSong = (Director.SelectedSong + delta + songs.Count) % songs.Count;
            var next = songs[Director.SelectedSong];
            int same = next.Charts.FindIndex(c => c.Difficulty == difficulty);
            Director.SelectedChart = same >= 0 ? same : Mathf.Clamp(Director.SelectedChart, 0, Mathf.Max(0, next.Charts.Count - 1));
        }

        private void UpdatePreview(GameDirector.LoadedSong song)
        {
            if (song != _previewing)
            {
                _previewing = song;
                _preview.Stop();
                _preview.clip = song?.Audio;
                if (_preview.clip != null)
                {
                    _preview.time = Mathf.Clamp(song.PreviewStart, 0f, Mathf.Max(0f, _preview.clip.length - 0.1f));
                    _preview.Play();
                }
            }

            // 試聴の長さを過ぎたら、開始位置に戻す
            if (song != null && _preview.isPlaying && song.PreviewLength > 0f && _preview.time > song.PreviewStart + song.PreviewLength)
                _preview.time = song.PreviewStart;
        }

        private void OnGUI()
        {
            var songs = Director.Songs;
            var lines = new List<string>
            {
                "<size=32>選曲</size>",
                "",
                "↑↓: 曲　←→: 難易度　Enter: 開始　A: オートプレイ　Tab: 設定　Esc: タイトルへ",
                $"オートプレイ: {(Director.AutoPlay ? "ON（記録しません）" : "OFF")}",
                "",
            };

            if (songs.Count == 0)
            {
                lines.Add("曲がありません。GameDirector の Library に曲を登録してください。");
                PlainGui.Text(string.Join("\n", lines));
                return;
            }

            for (int i = 0; i < songs.Count; i++)
            {
                bool selected = i == Director.SelectedSong;
                lines.Add($"{(selected ? "▶" : "　")} {songs[i].Title}");
                if (selected) AddDetail(lines, songs[i]);
            }
            PlainGui.Text(string.Join("\n", lines));

            // ジャケットは右上に、そのままの画像で出す
            var jacket = songs[Director.SelectedSong].Jacket;
            if (jacket != null) GUI.DrawTexture(new Rect(Screen.width - 40 - 256, 40, 256, 256), jacket, ScaleMode.ScaleToFit);
        }

        private void AddDetail(List<string> lines, GameDirector.LoadedSong song)
        {
            var info = new List<string>();
            if (!string.IsNullOrEmpty(song.Artist)) info.Add(song.Artist);
            if (!string.IsNullOrEmpty(song.BpmText)) info.Add($"BPM {song.BpmText}");
            if (song.Audio == null) info.Add("※音声なし");
            if (info.Count > 0) lines.Add("　　" + string.Join("　", info));

            if (song.Charts.Count == 0) lines.Add("　　（譜面なし）");
            for (int c = 0; c < song.Charts.Count; c++)
            {
                var chart = song.Charts[c];
                var record = Director.FindRecord(song, chart);
                string best = record == null
                    ? "未プレイ"
                    : $"{record.bestRate:0.0000}%　{PlayResult.LampLabel(record.bestLamp)}　{record.playCount} 回";
                string label = $"{chart.Difficulty} Lv.{chart.Level}　{best}";
                lines.Add(c == Director.SelectedChart ? $"　　<b>[{label}]</b>" : $"　　 {label}");
            }
            foreach (var error in song.Errors) lines.Add($"　　<color=#ff6060>読み込めない譜面: {error}</color>");
            lines.Add("");
        }
    }
}
