using System;
using System.Collections.Generic;
using LunarTrails.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LunarTrails.Runtime.Settings
{
    /// <summary>
    /// 設定画面の中身（項目の選択、値の変更、キーの割り当て）。キーボードで操作し、表示用の文字を返す。
    /// 試作の PrototypeMenu と、本番の流れの SettingsScreen の両方から使う。
    /// </summary>
    /// <remarks>
    /// ↑↓ で項目、←→ で値（Shift で大きく）、キー配置は Enter を押してから割り当てたいキーを押す。
    /// 値は変えるたびに反映して保存する。
    /// </remarks>
    public sealed class SettingsPanel
    {
        // 項目（並び順）
        private const int ItemOffset = 0;
        private const int ItemScrollSpeed = 1;
        private const int ItemSensitivity = 2;
        private const int ItemFirstKey = 3;
        private const int ItemReset = ItemFirstKey + 6;
        private const int ItemCount = ItemReset + 1;

        private readonly Func<GameSettings> _current;
        private readonly Action<GameSettings> _apply;

        private int _index;
        private int _rebindingLane = -1;   // キーの割り当て待ちのレーン（-1 なら待っていない）
        private int _rebindStartFrame;     // 割り当てを始めた Enter 自体を割り当てないため

        /// <param name="current">今の設定を返す</param>
        /// <param name="apply">変えた設定をゲームに反映する（保存はこのクラスがする）</param>
        public SettingsPanel(Func<GameSettings> current, Action<GameSettings> apply)
        {
            _current = current;
            _apply = apply;
        }

        /// <summary>キーの割り当て待ちか（このときの Esc は「閉じる」ではなく「取り消し」）</summary>
        public bool IsRebinding => _rebindingLane >= 0;

        /// <summary>開き直したときに、選択を先頭に戻す。</summary>
        public void ResetSelection()
        {
            _index = 0;
            _rebindingLane = -1;
        }

        /// <summary>1 フレーム分の操作を処理する。閉じる操作（Esc か Tab）をしたら true。</summary>
        public bool HandleInput(Keyboard keyboard)
        {
            var settings = _current();

            if (IsRebinding)
            {
                if (Time.frameCount <= _rebindStartFrame) return false;
                // 割り当て待ち：Esc で取り消し、それ以外のキーを押したらそのキーにする
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _rebindingLane = -1;
                    return false;
                }
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key == null || !key.wasPressedThisFrame) continue;
                    settings.SetLaneKey(_rebindingLane, key.keyCode.ToString());
                    _rebindingLane = -1;
                    Commit(settings);
                    return false;
                }
                return false;
            }

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) return true;

            if (keyboard.upArrowKey.wasPressedThisFrame) _index = (_index - 1 + ItemCount) % ItemCount;
            if (keyboard.downArrowKey.wasPressedThisFrame) _index = (_index + 1) % ItemCount;

            int direction = 0;
            if (keyboard.leftArrowKey.wasPressedThisFrame) direction = -1;
            if (keyboard.rightArrowKey.wasPressedThisFrame) direction = +1;
            bool big = keyboard.shiftKey.isPressed;
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;

            if (direction != 0)
            {
                switch (_index)
                {
                    case ItemOffset: settings.offsetMs += direction * (big ? 10 : 1); break;
                    case ItemScrollSpeed: settings.scrollSpeed += direction * (big ? 5f : 1f); break;
                    case ItemSensitivity: settings.pixelsPerLane += direction * (big ? 100f : 25f); break;
                }
                Commit(settings);
            }

            if (enter)
            {
                if (_index >= ItemFirstKey && _index < ItemFirstKey + 6)
                {
                    _rebindingLane = _index - ItemFirstKey;
                    _rebindStartFrame = Time.frameCount;
                }
                else if (_index == ItemReset)
                {
                    Commit(new GameSettings());
                }
            }
            return false;
        }

        // 値を正しい範囲に直してから、反映して保存する
        private void Commit(GameSettings settings)
        {
            settings.Sanitize();
            _apply(settings);
            SettingsStore.Save(settings);
        }

        /// <summary>表示用の文字（OnGUI のリッチテキスト）。見出しと操作説明は含まない。</summary>
        public string Text()
        {
            var s = _current();
            string Row(int index, string label, string value)
            {
                string mark = index == _index ? "▶" : "　";
                return $"{mark} {label}: {value}";
            }

            var lines = new List<string>
            {
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

        /// <summary>操作説明の 1 行</summary>
        public const string HelpText = "↑↓: 項目　←→: 値を変える（Shift で大きく）　Enter: キーの割り当て　Esc / Tab: 戻る";
    }
}
