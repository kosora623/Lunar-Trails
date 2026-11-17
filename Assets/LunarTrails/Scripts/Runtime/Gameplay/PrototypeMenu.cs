using System.Collections.Generic;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Settings;
using LunarTrails.Runtime.Charts;
using LunarTrails.Runtime.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
    /// UI 案が決まったら、本番の画面に置き換える。
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

        // 設定画面の項目（並び順）
        private const int ItemOffset = 0;
        private const int ItemScrollSpeed = 1;
        private const int ItemSensitivity = 2;
        private const int ItemFirstKey = 3;
        private const int ItemReset = ItemFirstKey + 6;
        private const int ItemCount = ItemReset + 1;

        private PrototypePlayer _player;
        private readonly List<SongItem> _songs = new List<SongItem>();
        private Screen _screen = Screen.Select;
        private int _songIndex;
        private int _chartIndex;
        private int _settingIndex;
        private int _rebindingLane = -1;      // キーの割り当て待ちのレーン（-1 なら待っていない）
        private int _ignoreInputUntilFrame;   // 画面が切り替わったフレームの Enter などを拾わないため

        private GUIStyle _style;

        private void Awake()
        {
            _player = GetComponent<PrototypePlayer>();
            _player.ReturnedToMenu += () => _ignoreInputUntilFrame = Time.frameCount + 1;
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
            var settings = _player.CurrentSettings;

            if (_rebindingLane >= 0)
            {
                // 割り当て待ち：Esc で取り消し、それ以外のキーを押したらそのキーにする
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _rebindingLane = -1;
                    return;
                }
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key == null || !key.wasPressedThisFrame) continue;
                    settings.SetLaneKey(_rebindingLane, key.keyCode.ToString());
                    _rebindingLane = -1;
                    Commit(settings);
                    return;
                }
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                _screen = Screen.Select;
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame) _settingIndex = (_settingIndex - 1 + ItemCount) % ItemCount;
            if (keyboard.downArrowKey.wasPressedThisFrame) _settingIndex = (_settingIndex + 1) % ItemCount;

            int direction = 0;
            if (keyboard.leftArrowKey.wasPressedThisFrame) direction = -1;
            if (keyboard.rightArrowKey.wasPressedThisFrame) direction = +1;
            bool big = keyboard.shiftKey.isPressed;
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;

            if (direction != 0)
            {
                switch (_settingIndex)
                {
                    case ItemOffset: settings.offsetMs += direction * (big ? 10 : 1); break;
                    case ItemScrollSpeed: settings.scrollSpeed += direction * (big ? 5f : 1f); break;
                    case ItemSensitivity: settings.pixelsPerLane += direction * (big ? 100f : 25f); break;
                }
                Commit(settings);
            }

            if (enter)
            {
                if (_settingIndex >= ItemFirstKey && _settingIndex < ItemFirstKey + 6)
                {
                    _rebindingLane = _settingIndex - ItemFirstKey;
                    _ignoreInputUntilFrame = Time.frameCount; // この Enter 自体を割り当てないように、次のフレームから受け付ける
                }
                else if (_settingIndex == ItemReset)
                {
                    Commit(new GameSettings());
                }
            }
        }

        // 値を正しい範囲に直してから、反映して保存する
        private void Commit(GameSettings settings)
        {
            settings.Sanitize();
            _player.ApplySettings(settings);
            SettingsStore.Save(settings);
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
            var s = _player.CurrentSettings;
            string Row(int index, string label, string value)
            {
                string mark = index == _settingIndex ? "▶" : "　";
                return $"{mark} {label}: {value}";
            }

            var lines = new List<string>
            {
                "<size=32>設定</size>",
                "",
                "↑↓: 項目　←→: 値を変える（Shift で大きく）　Enter: キーの割り当て　Esc / Tab: 戻る",
                $"保存先: {SettingsStore.FilePath}",
                "",
                Row(ItemOffset, "オフセット", $"{s.offsetMs:+0;-0;0}ms（+ にすると、遅れて叩いてもちょうどになる）"),
                Row(ItemScrollSpeed, "ノーツの速さ", $"{s.scrollSpeed:0}"),
                Row(ItemSensitivity, "マウス感度", $"{s.pixelsPerLane:0}（レーンの端から端までのマウス移動量。小さいほど敏感）"),
            };
            for (int lane = 0; lane < 6; lane++)
            {
                string value = _rebindingLane == lane ? "<b>割り当てたいキーを押してください（Esc で取り消し）</b>" : s.laneKeys[lane];
                lines.Add(Row(ItemFirstKey + lane, $"レーン {lane + 1} のキー", value));
            }
            lines.Add(Row(ItemReset, "初期設定に戻す", "Enter"));
            return string.Join("\n", lines);
        }
    }
}
