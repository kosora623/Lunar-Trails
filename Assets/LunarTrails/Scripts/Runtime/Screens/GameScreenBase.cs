using LunarTrails.Runtime.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarTrails.Runtime.Screens
{
    /// <summary>
    /// 画面の部品の土台。GameDirector を見つけて Director に入れる。
    /// GameDirector が、その画面のときだけこの部品（か、その GameObject）を有効にする。
    /// </summary>
    /// <remarks>
    /// 本番の画面（uGUI など）も、これを継承すると仮の画面と同じように作れる。
    /// 操作は Update で、表示は OnGUI か Canvas で。画面を移るときは Director のメソッドを呼ぶ。
    /// </remarks>
    public abstract class GameScreenBase : MonoBehaviour
    {
        protected GameDirector Director { get; private set; }

        protected virtual void Awake()
        {
            Director = GetComponentInParent<GameDirector>();
            if (Director == null) Director = FindFirstObjectByType<GameDirector>();
            if (Director == null) Debug.LogError($"{GetType().Name}: シーンに GameDirector がありません");
        }

        /// <summary>このフレームの操作を読んでよいならキーボードを返す（画面が切り替わったフレームは null）。</summary>
        protected Keyboard ReadableKeyboard()
        {
            if (Director == null || !Director.AcceptsInput) return null;
            return Keyboard.current;
        }

        protected static bool EnterPressed(Keyboard keyboard)
            => keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }
}
