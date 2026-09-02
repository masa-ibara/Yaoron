using UnityEngine;

namespace Yaoron.Inputs
{
    /// <summary>
    /// スマートフォン用。左半分は仮想スティック (TouchStickView から値をもらう)、
    /// 右半分のドラッグで視点 (設計書 §10)。UI 上のタッチは視点操作に使わない。
    /// </summary>
    public class TouchInput : MonoBehaviour, IInputSource
    {
        [SerializeField] float _dragSensitivity = 0.08f;
        [Tooltip("左半分は仮想スティック領域として視点ドラッグから除外する")]
        [SerializeField] float _lookAreaMinX = 0.5f;

        Vector2 _move, _look;
        bool _jump, _ptt, _muteToggle, _viewToggle;
        int _lookFingerId = -1;
        Vector2 _lastLookPosition;

        /// <summary>仮想スティック UI から毎フレーム設定される。</summary>
        public Vector2 StickValue { get; set; }

        /// <summary>UI ボタンから叩く。</summary>
        public void PressJump() => _jump = true;
        public void SetPushToTalk(bool held) => _ptt = held;
        public void ToggleMute() => _muteToggle = true;
        /// <summary>UI ボタンから視点を切り替える。</summary>
        public void ToggleView() => _viewToggle = true;

        public Vector2 Move => _move;
        public Vector2 Look => _look;
        public float SnapTurn => 0f;
        public bool JumpPressed => _jump;
        public bool Sprint => StickValue.magnitude > 0.9f;   // スティックを倒し切ったら走る
        public bool PushToTalkHeld => _ptt;
        public bool MuteTogglePressed => _muteToggle;
        public bool ViewTogglePressed => _viewToggle;

        public void Tick()
        {
            _move = Vector2.ClampMagnitude(StickValue, 1f);
            _look = ReadLookDrag();
        }

        /// <summary>押下フラグは 1 フレームで消費する。リグの Update 後に呼ばれる。</summary>
        public void ConsumeButtons()
        {
            _jump = false;
            _muteToggle = false;
            _viewToggle = false;
        }

        // Active Input Handling が "Input System Package (New)" だけの場合、
        // UnityEngine.Input.touches は実行時に例外を投げる。導入済みなら新 API 側で読む。
#if YA_INPUTSYSTEM
        Vector2 ReadLookDrag()
        {
            var screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen == null) return Vector2.zero;

            foreach (var touch in screen.touches)
            {
                int id = touch.touchId.ReadValue();
                var phase = touch.phase.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (_lookFingerId != -1) continue;
                    if (position.x < Screen.width * _lookAreaMinX) continue;
                    _lookFingerId = id;
                    _lastLookPosition = position;
                }
                else if (id == _lookFingerId)
                {
                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                        phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        _lookFingerId = -1;
                        continue;
                    }
                    var delta = position - _lastLookPosition;
                    _lastLookPosition = position;
                    return delta * _dragSensitivity;
                }
            }
            return Vector2.zero;
        }
#else
        Vector2 ReadLookDrag()
        {
            var touches = UnityEngine.Input.touches;
            for (int i = 0; i < touches.Length; i++)
            {
                var touch = touches[i];
                if (touch.phase == TouchPhase.Began)
                {
                    if (_lookFingerId != -1) continue;
                    if (touch.position.x < Screen.width * _lookAreaMinX) continue;
                    _lookFingerId = touch.fingerId;
                    _lastLookPosition = touch.position;
                }
                else if (touch.fingerId == _lookFingerId)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _lookFingerId = -1;
                        continue;
                    }
                    var delta = touch.position - _lastLookPosition;
                    _lastLookPosition = touch.position;
                    return delta * _dragSensitivity;
                }
            }
            return Vector2.zero;
        }
#endif
    }
}
