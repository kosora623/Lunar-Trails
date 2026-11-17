using System.Collections.Generic;
using LunarTrails.Core.Charts;
using LunarTrails.Runtime.Charts;
using LunarTrails.Runtime.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarTrails.Runtime.Gameplay
{
    /// <summary>
    /// 試作用の選曲画面と設定画面。見た目は仮（OnGUI の文字だけ）で、キーボードで操作する。
    /// PrototypePlayer が自動で付ける。
    /// </summary>
    /// <remarks>
    /// 選曲：↑↓ で曲、←→ で難易度、Enter で開始、A でオートプレイの切り替え、Tab で設定へ。
    /// 設定：↑↓ で項目、←→ で値（Shift で大きく）、キー配置は Enter を押してから割り当てたいキーを押す。
    ///       Esc か Tab で選曲に戻る。値は変えるたびに保存する。
    /// 設定画面の中身は SettingsPanel（本番の流れの SettingsScreen と共通）。
    /// GameDirector（本番の流れ）を付けたシーンでは使わない（GameDirector が止める）。
    /// </remarks>
    [RequireComponent(typeof(PrototypePlayer))]
    public sealed class PrototypeMenu : MonoBehaviour
    {
        private enum Screen { Select, Settings }

        /// <summary>1 曲分の、読み込み済みの譜面</summary>
        private sealed class SongItem
        {
            public PrototypePlayer.SongEntry Entry;
            public readonly List<Chart> Charts = new List<Chart>();
            public readonly List<string> Errors = new List<string>();
        }

        private PrototypePlayer _player;
        private readonly List<SongItem> _songs = new List<SongItem>();
        private Screen _screen = Screen.Select;
        private int _songIndex;
        private int _chartIndex;
        private SettingsPanel _settingsPanel;
        private int _ignoreInputUntilFrame;   // 画面が切り替わったフレームの Enter などを拾わないため

        private GUIStyle _style;

        private void Awake()
        {
            _player = GetComponent<PrototypePlayer>();
            _player.ReturnedToMenu += () => _ignoreInputUntilFrame = Time.frameCount + 1;
            _settingsPanel = new SettingsPanel(() => _player.CurrentSettings, _player.ApplySettings);
        }

        private void Start()
        {
            foreach (var entry in _player.Songs)
                _songs.Add(LoadSong(entry));
        }

        private static SongItem LoadSong(PrototypePlayer.SongEntry entry)
        {
            var item = new SongItem { Entry = entry };
            foreach (var asset in entry.charts ?? new TextAsset[0])
            {
                if (asset == null) continue;
                try
                {
                    item.Charts.Add(ChartLoader.FromTextAsset(asset));
                }
                catch (ChartFormatException e)
                {
                    item.Errors.Add($"{asset.name}: {e.Message}");
                    Debug.LogError($"譜面を読み込めませんでした（{asset.name}）: {e.Message}");
                }
            }
            return item;
        }

        // ===== 操作 =====

        private void Update()
        {
            if (_player.IsPlaying) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || Time.frameCount <= _ignoreInputUntilFrame) return;

            if (_screen == Screen.Select) UpdateSelect(keyboard);
            else UpdateSettings(keyboard);
        }

        private void UpdateSelect(Keyboard keyboard)
        {
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                _screen = Screen.Settings;
                _settingsPanel.ResetSelection();
                return;
            }
            if (keyboard.aKey.wasPressedThisFrame) _player.AutoPlayEnabled = !_player.AutoPlayEnabled;
            if (_songs.Count == 0) return;

            if (keyboard.upArrowKey.wasPressedThisFrame) MoveSong(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame) MoveSong(+1);

            var song = _songs[_songIndex];
            if (song.Charts.Count == 0) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame) _chartIndex = Mathf.Max(0, _chartIndex - 1);
            if (keyboard.rightArrowKey.wasPressedThisFrame) _chartIndex = Mathf.Min(song.Charts.Count - 1, _chartIndex + 1);

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                _player.Play(song.Charts[_chartIndex], song.Entry.audio);
        }

        private void MoveSong(int delta)
        {
            _songIndex = (_songIndex + delta + _songs.Count) % _songs.Count;
            _chartIndex = Mathf.Clamp(_chartIndex, 0, Mathf.Max(0, _songs[_songIndex].Charts.Count - 1));
        }

        private void UpdateSettings(Keyboard keyboard)
        {
            if (_settingsPanel.HandleInput(keyboard)) _screen = Screen.Select;
        }

        // ===== 表示 =====

        private void OnGUI()
        {
            if (_player.IsPlaying) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };

            string text = _screen == Screen.Select ? SelectText() : SettingsText();
            GUI.Label(new Rect(40, 40, UnityEngine.Screen.width - 80, UnityEngine.Screen.height - 80), text, _style);
        }

        private string SelectText()
        {
            var lines = new List<string>
            {
                "<size=32>選曲</size>",
                "",
                "↑↓: 曲　←→: 難易度　Enter: 開始　A: オートプレイ　Tab: 設定",
                $"オートプレイ: {(_player.AutoPlayEnabled ? "ON" : "OFF")}",
                "",
            };

            if (_songs.Count == 0)
            {
                lines.Add("曲がありません。PrototypePlayer の Songs に曲を登録してください。");
                return string.Join("\n", lines);
            }

            for (int i = 0; i < _songs.Count; i++)
            {
                var song = _songs[i];
                bool selected = i == _songIndex;
                string title = string.IsNullOrEmpty(song.Entry.title) ? "（曲名なし）" : song.Entry.title;
                var difficulties = new List<string>();
                for (int c = 0; c < song.Charts.Count; c++)
                {
                    string label = $"{song.Charts[c].Difficulty} Lv.{song.Charts[c].Level}";
                    difficulties.Add(selected && c == _chartIndex ? $"<b>[{label}]</b>" : label);
                }
                string charts = difficulties.Count > 0 ? string.Join("  ", difficulties) : "（譜面なし）";
                string audio = song.Entry.audio == null ? "  ※音声なし" : "";
                lines.Add($"{(selected ? "▶" : "　")} {title}　{charts}{audio}");

                if (selected)
                {
                    foreach (var error in song.Errors) lines.Add($"　　<color=#ff6060>読み込めない譜面: {error}</color>");
                }
            }
            return string.Join("\n", lines);
        }

        private string SettingsText()
        {
            return "<size=32>設定</size>\n\n" + SettingsPanel.HelpText + "\n"
                + $"保存先: {SettingsStore.FilePath}\n\n" + _settingsPanel.Text();
        }
    }
}
