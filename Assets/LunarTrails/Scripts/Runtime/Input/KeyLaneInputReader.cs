using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace LunarTrails.Runtime.Input
{
    /// <summary>キーが押された記録。</summary>
    public readonly struct KeyPress
    {
        public readonly int Lane;
        /// <summary>押された時刻。Time.realtimeSinceStartupAsDouble と同じ基準（秒）。</summary>
        public readonly double RealTime;

        public KeyPress(int lane, double realTime)
        {
            Lane = lane;
            RealTime = realTime;
        }
    }

    /// <summary>
    /// 6K のキー入力を、押された正確な時刻つきで集める。
    /// </summary>
    /// <remarks>
    /// Update で Keyboard.current.xxx.wasPressedThisFrame を見る方法だと、時刻がフレーム単位
    /// （60fps で最大約 17ms の誤差）になってしまう。±40ms の判定には大きいので、
    /// Input System の入力イベントに付いている時刻を使う。
    ///
    /// 使い方：毎フレーム DrainPresses() で溜まった入力を取り出す。
    /// </remarks>
    public sealed class KeyLaneInputReader : MonoBehaviour
    {
        // 企画書の決定事項 5：初期設定は S D F と J K L。設定画面ができたらそこから差し替える。
        [SerializeField]
        private Key[] _laneKeys = { Key.S, Key.D, Key.F, Key.J, Key.K, Key.L };

        private readonly List<KeyPress> _buffer = new List<KeyPress>();

        public IReadOnlyList<Key> LaneKeys => _laneKeys;

        private void OnEnable() => InputSystem.onEvent += OnInputEvent;

        private void OnDisable() => InputSystem.onEvent -= OnInputEvent;

        /// <summary>前回呼んでから押されたキーを、押された順に返す。</summary>
        public List<KeyPress> DrainPresses()
        {
            var result = new List<KeyPress>(_buffer);
            _buffer.Clear();
            return result;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!(device is Keyboard keyboard)) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            for (int lane = 0; lane < _laneKeys.Length; lane++)
            {
                KeyControl key = keyboard[_laneKeys[lane]];

                // onEvent はイベントが反映される前に呼ばれるので、
                // ReadValue() が「直前の状態」、ReadValueFromEvent が「このイベント後の状態」になる。
                if (!key.ReadValueFromEvent(eventPtr, out float next)) continue;
                bool wasUp = !key.IsValueConsideredPressed(key.ReadValue());
                bool isDown = key.IsValueConsideredPressed(next);

                if (wasUp && isDown)
                    _buffer.Add(new KeyPress(lane, eventPtr.time));
            }
        }
    }
}
