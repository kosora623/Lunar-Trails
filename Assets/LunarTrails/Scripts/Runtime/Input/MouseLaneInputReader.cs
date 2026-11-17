using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace LunarTrails.Runtime.Input
{
    /// <summary>
    /// マウス（トラックパッドを含む）の入力を、マウスレーン内のカーソル位置・速さと、クリックの時刻にする。
    /// </summary>
    /// <remarks>
    /// 画面上のカーソル位置をそのまま使うと、レーンの外に出たり、人によって感度が違ったりする。
    /// そのため OS のカーソルは画面中央に固定して隠し、移動量だけを読んでレーン内の位置を自分で計算する。
    /// Esc キーで固定を外し、ゲーム画面をクリックするとまた固定する。
    ///
    /// クリックの時刻は、キー入力と同じく Input System のイベントの時刻を使う（左右のボタンは区別しない）。
    /// 位置と速さは毎フレームの移動量から計算する。
    /// </remarks>
    [DefaultExecutionOrder(-100)] // ゲーム進行より先に、このフレームのカーソル位置を計算しておく
    public sealed class MouseLaneInputReader : MonoBehaviour
    {
        [Tooltip("カーソルがマウスレーンの端から端まで動くのに必要なマウスの移動量（ピクセル）。小さいほど感度が高い")]
        [SerializeField] private float _pixelsPerLane = 500f;

        private readonly List<double> _clicks = new List<double>();

        /// <summary>カーソルのマウスレーン内の位置（0 = 左端、1 = 右端）</summary>
        public float CursorX { get; private set; } = 0.5f;

        /// <summary>カーソルの速さ（1 秒にマウスレーン何本分か）。端で止まっていても、マウスを動かした分で計算する。</summary>
        public float Speed { get; private set; }

        public float PixelsPerLane => _pixelsPerLane;

        /// <summary>マウスのボタン（左右どちらか）を押しているか。トレイルの判定に使う。</summary>
        public bool IsButtonHeld
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
            }
        }

        private void OnEnable()
        {
            InputSystem.onEvent += OnInputEvent;
            LockCursor(true);
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
            LockCursor(false);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

            // delta は、このフレームの間にマウスが動いた量（ピクセル）
            float moved = mouse.delta.ReadValue().x / _pixelsPerLane;
            CursorX = Mathf.Clamp01(CursorX + moved);
            float dt = Time.unscaledDeltaTime;
            Speed = dt > 0 ? Mathf.Abs(moved) / dt : 0f;
        }

        /// <summary>前回呼んでからクリックされた時刻（Time.realtimeSinceStartupAsDouble と同じ基準）を返す。</summary>
        public List<double> DrainClicks()
        {
            var result = new List<double>(_clicks);
            _clicks.Clear();
            return result;
        }

        public void Clear()
        {
            _clicks.Clear();
            CursorX = 0.5f;
            Speed = 0f;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!(device is Mouse mouse)) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            if (WasPressedInEvent(mouse.leftButton, eventPtr) || WasPressedInEvent(mouse.rightButton, eventPtr))
                _clicks.Add(eventPtr.time);
        }

        // onEvent はイベントが反映される前に呼ばれるので、ReadValue() が直前の状態になる
        private static bool WasPressedInEvent(ButtonControl button, InputEventPtr eventPtr)
        {
            if (!button.ReadValueFromEvent(eventPtr, out float next)) return false;
            return !button.IsValueConsideredPressed(button.ReadValue()) && button.IsValueConsideredPressed(next);
        }
    }
}
