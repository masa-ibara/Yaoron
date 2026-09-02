using Yaoron.Inputs;
using UnityEngine;
using UnityEngine.XR;

namespace Yaoron.XR
{
    /// <summary>
    /// VR コントローラ入力。built-in の XR モジュール (InputDevices) だけで読むので、
    /// OpenXR / XRI のバージョン差やパッケージ未導入でコンパイルが壊れない。
    /// 左スティック = スムーズ移動、右スティック = 45° スナップターン (設計書 §10)。
    /// </summary>
    public class XRInput : MonoBehaviour, IInputSource
    {
        [SerializeField] float _snapDeadzone = 0.7f;

        Vector2 _move;
        bool _jump, _ptt, _muteToggle;
        float _snapTurn;
        bool _snapLatched;

        public Vector2 Move => _move;
        public Vector2 Look => Vector2.zero;      // 視点は HMD が決める
        public float SnapTurn => _snapTurn;
        public bool JumpPressed => _jump;
        public bool Sprint => false;
        public bool PushToTalkHeld => _ptt;
        public bool MuteTogglePressed => _muteToggle;
        public bool ViewTogglePressed => false;   // VR は常に一人称

        public void Tick()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            _move = left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis)
                ? Vector2.ClampMagnitude(axis, 1f)
                : Vector2.zero;

            _jump = left.isValid && left.TryGetFeatureValue(CommonUsages.primaryButton, out var jump) && jump;

            // PTT は左トリガー、ミュート切替は右の A/X ボタン。
            _ptt = left.isValid && left.TryGetFeatureValue(CommonUsages.triggerButton, out var trigger) && trigger;

            bool muteNow = right.isValid && right.TryGetFeatureValue(CommonUsages.primaryButton, out var mute) && mute;
            _muteToggle = muteNow && !_mutePrevious;
            _mutePrevious = muteNow;

            UpdateSnapTurn(right);
        }

        bool _mutePrevious;

        /// <summary>スティックを倒し切ったときに 1 回だけ発火させ、中央に戻るまで再発火させない。</summary>
        void UpdateSnapTurn(InputDevice right)
        {
            _snapTurn = 0f;
            if (!right.isValid || !right.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis)) return;

            if (Mathf.Abs(axis.x) < _snapDeadzone * 0.5f) _snapLatched = false;
            else if (!_snapLatched && Mathf.Abs(axis.x) >= _snapDeadzone)
            {
                _snapLatched = true;
                _snapTurn = Mathf.Sign(axis.x);
            }
        }
    }
}
