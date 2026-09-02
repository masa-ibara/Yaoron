using UnityEngine;
#if YA_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif

namespace Yaoron.Inputs
{
    /// <summary>
    /// キーボード + マウス (Windows / macOS アプリ、ブラウザ)。
    /// 視点は右ドラッグ中のみ動かす。ブラウザではポインタロックがユーザー操作を要求するため、
    /// 「押している間だけ回る」方式のほうが事故が少ない (設計書 §10)。
    /// Input System パッケージがあればそちらを、無ければ従来の Input Manager を使う。
    /// </summary>
    public class DesktopInput : MonoBehaviour, IInputSource
    {
        [SerializeField] float _mouseSensitivity = 0.12f;
        [SerializeField] bool _requireRightDragToLook = true;

        Vector2 _move, _look;
        bool _jump, _sprint, _ptt, _muteToggle, _viewToggle;

        public Vector2 Move => _move;
        public Vector2 Look => _look;
        public float SnapTurn => 0f;
        public bool JumpPressed => _jump;
        public bool Sprint => _sprint;
        public bool PushToTalkHeld => _ptt;
        public bool MuteTogglePressed => _muteToggle;
        public bool ViewTogglePressed => _viewToggle;

        public void Tick()
        {
#if YA_INPUTSYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            float x = 0f, y = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

                _sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                _jump = keyboard.spaceKey.wasPressedThisFrame;
                _ptt = keyboard.tKey.isPressed;
                _muteToggle = keyboard.mKey.wasPressedThisFrame;
                _viewToggle = keyboard.vKey.wasPressedThisFrame;
            }
            _move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

            bool looking = !_requireRightDragToLook || (mouse != null && mouse.rightButton.isPressed);
            var delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            _look = looking ? delta * _mouseSensitivity : Vector2.zero;
#else
            _move = Vector2.ClampMagnitude(
                new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical")), 1f);

            _sprint = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            _jump = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            _ptt = UnityEngine.Input.GetKey(KeyCode.T);
            _muteToggle = UnityEngine.Input.GetKeyDown(KeyCode.M);
            _viewToggle = UnityEngine.Input.GetKeyDown(KeyCode.V);

            bool looking = !_requireRightDragToLook || UnityEngine.Input.GetMouseButton(1);
            var delta = new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"), UnityEngine.Input.GetAxisRaw("Mouse Y"));
            // Input Manager の Mouse X/Y は既に感度が掛かっているので、係数を合わせる。
            _look = looking ? delta * (_mouseSensitivity * 12f) : Vector2.zero;
#endif
        }
    }
}
