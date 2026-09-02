using System.Collections;
using Yaoron.Core;
using UnityEngine;
#if YA_NORMCORE
using Normal.Realtime;
#endif

namespace Yaoron.Avatar
{
    /// <summary>
    /// ローカル所有アバターだけで動く。リグ (身体) の姿勢をアバターのトランスフォームに写し、
    /// 移動状態を AvatarState に落とす。ネットワークへの書き込み自体は AvatarSync が担当する。
    /// </summary>
    public class AvatarLocalDriver : MonoBehaviour
    {
        [SerializeField] YaAvatar _avatar;
        [Tooltip("走行判定のしきい値 (m/s)")]
        [SerializeField] float _runSpeed = 3.2f;
        [Tooltip("歩行判定のしきい値 (m/s)")]
        [SerializeField] float _walkSpeed = 0.15f;

        IRigSource _rig;
        Vector3 _lastRootPosition;
        float _planarSpeed;
        bool _ready;
        Coroutine _setup;

        public void Bind(YaAvatar avatar, IRigSource rig)
        {
            _avatar = avatar != null ? avatar : _avatar;
            _rig = rig;
            _ready = false;
            if (_rig == null) return;

            _lastRootPosition = _rig.Root.position;

            // Bind は RealtimeView がモデルを各コンポーネントへ配っている最中に呼ばれることがある。
            // その時点では所有権 API がモデル未設定で例外を投げるので、1 フレーム待ってから触る。
            if (_setup != null) StopCoroutine(_setup);
            _setup = StartCoroutine(SetupWhenModelReady());
        }

        IEnumerator SetupWhenModelReady()
        {
            yield return null;
            _setup = null;
            if (_avatar == null || _rig == null) yield break;

            // 非VR では手を同期しない。GameObject ごと落として RealtimeTransform の送信も止める (設計書 §6)。
            var hands = _rig.HasHands;
            if (_avatar.LeftHand != null) _avatar.LeftHand.gameObject.SetActive(hands);
            if (_avatar.RightHand != null) _avatar.RightHand.gameObject.SetActive(hands);

            RequestTransformOwnership();
            _ready = true;
        }

        void RequestTransformOwnership()
        {
#if YA_NORMCORE
            // 所有権はビュー単位で取れているはずだが、Normcore 同梱の RealtimeAvatar と同じく
            // 各 RealtimeTransform でも明示的に要求しておく。
            foreach (var rt in _avatar.GetComponentsInChildren<RealtimeTransform>(true))
            {
                try { rt.RequestOwnership(); }
                catch (System.Exception e) { YaLog.Warn($"RealtimeTransform の所有権を取得できません: {e.Message}"); }
            }
#endif
        }

        void LateUpdate()
        {
            if (!_ready || _avatar == null || _rig == null) return;

            var root = _avatar.Root;
            root.SetPositionAndRotation(_rig.Root.position, _rig.Root.rotation);

            if (_avatar.Head != null && _rig.Head != null)
            {
                _avatar.Head.SetPositionAndRotation(_rig.Head.position, _rig.Head.rotation);
            }

            if (_rig.HasHands)
            {
                if (_avatar.LeftHand != null && _rig.LeftHand != null)
                    _avatar.LeftHand.SetPositionAndRotation(_rig.LeftHand.position, _rig.LeftHand.rotation);
                if (_avatar.RightHand != null && _rig.RightHand != null)
                    _avatar.RightHand.SetPositionAndRotation(_rig.RightHand.position, _rig.RightHand.rotation);
            }

            UpdateLocomotion(root.position);
        }

        void UpdateLocomotion(Vector3 rootPosition)
        {
            var delta = rootPosition - _lastRootPosition;
            delta.y = 0f;
            _lastRootPosition = rootPosition;

            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // 生の速度はネットワーク遅延やテレポートで跳ねるので均す。
            _planarSpeed = Mathf.Lerp(_planarSpeed, delta.magnitude / dt, 0.35f);

            var state = _avatar.State;
            state.MoveDir = _rig.MoveInput;

            Locomotion next;
            if (!_rig.IsGrounded) next = Locomotion.Jump;
            else if (_planarSpeed >= _runSpeed || (_rig.IsRunning && _planarSpeed > _walkSpeed)) next = Locomotion.Run;
            else if (_planarSpeed > _walkSpeed) next = Locomotion.Walk;
            else next = Locomotion.Idle;

            state.Locomotion = next;
        }
    }
}
