using Yaoron.Core;
using UnityEngine;
#if YA_VRM
using UniVRM10;
#endif

namespace Yaoron.Avatar
{
    /// <summary>
    /// 受信した最小の状態から見た目を復元する層 (設計書 §6「ポーズ復元」)。
    ///   VR 送信者   : 頭 + 両手の 3 点から腕を IK で解き、腰と脚はアニメーションに任せる
    ///   非VR 送信者 : Animator のブレンドツリー + 頭の向きだけを反映 (手は非アクティブ)
    /// 距離 LOD (IK 停止 / Animator 間引き) は AvatarRemoteDriver から制御される。
    /// </summary>
    public class AvatarPoseSolver : MonoBehaviour
    {
        [Header("アニメーション")]
        [Tooltip("Humanoid 用のコントローラ。未設定なら簡易の待機ポーズだけ当てる。")]
        [SerializeField] RuntimeAnimatorController _locomotionController;
        [SerializeField] float _headYawLimit = 70f;
        [SerializeField] float _headPitchLimit = 35f;

        [Header("IK")]
        [SerializeField] float _handIkWeight = 1f;
        [SerializeField] float _elbowHintDistance = 0.35f;

        YaAvatar _avatar;
        Animator _animator;
        Transform _head, _neck, _chest;
        Transform _lUpper, _lLower, _lHand, _rUpper, _rLower, _rHand;
        bool _ikEnabled = true;
        bool _headVisible = true;
        Vector3 _headOriginalScale = Vector3.one;
        float _mouthOpen;

#if YA_VRM
        Vrm10Instance _vrm;
#endif

        static readonly int SpeedHash    = Animator.StringToHash("Speed");
        static readonly int MoveXHash    = Animator.StringToHash("MoveX");
        static readonly int MoveYHash    = Animator.StringToHash("MoveY");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int SitHash      = Animator.StringToHash("Sit");

        /// <summary>VRM が差し替わるたびに呼ばれ、ボーン参照を取り直す。</summary>
        public void Bind(YaAvatar avatar, GameObject model)
        {
            _avatar = avatar;
            _animator = null;
            _head = _neck = _chest = null;
            _lUpper = _lLower = _lHand = _rUpper = _rLower = _rHand = null;
#if YA_VRM
            _vrm = null;
#endif
            if (model == null) return;

            _animator = model.GetComponentInChildren<Animator>();
            if (_animator == null || _animator.avatar == null || !_animator.avatar.isHuman)
            {
                YaLog.Warn("Humanoid の Animator が見つかりません。ポーズ復元は無効です。");
                return;
            }

            if (_locomotionController != null && _animator.runtimeAnimatorController == null)
                _animator.runtimeAnimatorController = _locomotionController;

            _head   = _animator.GetBoneTransform(HumanBodyBones.Head);
            _neck   = _animator.GetBoneTransform(HumanBodyBones.Neck);
            _chest  = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _lUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _lLower = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _lHand  = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rLower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rHand  = _animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (_head != null) _headOriginalScale = _head.localScale;
            ApplyHeadVisibility();

#if YA_VRM
            _vrm = model.GetComponent<Vrm10Instance>();
#endif
            if (_avatar != null)
            {
                _avatar.State.ExpressionChanged -= ApplyExpression;
                _avatar.State.ExpressionChanged += ApplyExpression;
                ApplyExpression(_avatar.State.Expression);
            }
        }

        void OnDestroy()
        {
            if (_avatar != null) _avatar.State.ExpressionChanged -= ApplyExpression;
        }

        public void SetIkEnabled(bool value) => _ikEnabled = value;

        public void SetAnimatorEnabled(bool value)
        {
            if (_animator != null && _animator.enabled != value) _animator.enabled = value;
        }

        /// <summary>VR の自視点では自分の頭が視界を塞ぐので潰す (設計書 §10)。</summary>
        public void SetHeadVisible(bool visible)
        {
            _headVisible = visible;
            ApplyHeadVisibility();
        }

        void ApplyHeadVisibility()
        {
            if (_head == null) return;
            _head.localScale = _headVisible ? _headOriginalScale : _headOriginalScale * 0.001f;
        }

        public void ApplyLocomotion(Locomotion locomotion, Vector2 moveDir)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            float speed = locomotion == Locomotion.Run ? 1f : locomotion == Locomotion.Walk ? 0.5f : 0f;
            _animator.SetFloat(SpeedHash, speed);
            _animator.SetFloat(MoveXHash, moveDir.x);
            _animator.SetFloat(MoveYHash, moveDir.y);
            _animator.SetBool(GroundedHash, locomotion != Locomotion.Jump);
            _animator.SetBool(SitHash, locomotion == Locomotion.Sit);
        }

        public void ApplyExpression(ExpressionPreset preset)
        {
#if YA_VRM
            if (_vrm == null || _vrm.Runtime == null) return;
            var expr = _vrm.Runtime.Expression;
            expr.SetWeight(ExpressionKey.Happy,     preset == ExpressionPreset.Happy ? 1f : 0f);
            expr.SetWeight(ExpressionKey.Angry,     preset == ExpressionPreset.Angry ? 1f : 0f);
            expr.SetWeight(ExpressionKey.Sad,       preset == ExpressionPreset.Sad ? 1f : 0f);
            expr.SetWeight(ExpressionKey.Relaxed,   preset == ExpressionPreset.Relaxed ? 1f : 0f);
            expr.SetWeight(ExpressionKey.Surprised, preset == ExpressionPreset.Surprised ? 1f : 0f);
#endif
        }

        /// <summary>発話量から口を開く (設計書 §7)。voiceVolume は同期せず各自ローカルで算出する。</summary>
        public void SetMouthOpen(float amount)
        {
            _mouthOpen = Mathf.Clamp01(amount);
#if YA_VRM
            if (_vrm != null && _vrm.Runtime != null)
                _vrm.Runtime.Expression.SetWeight(ExpressionKey.Aa, _mouthOpen);
#endif
        }

        void LateUpdate()
        {
            if (_avatar == null || _animator == null) return;
            if (_animator.runtimeAnimatorController == null) ApplyFallbackPose();
            ApplyHeadAim();
            if (_ikEnabled && _avatar.State.IsVR) ApplyHandIk();
        }

        /// <summary>アニメーションクリップが無い状態でも T ポーズのまま突っ立たせない最低限の補正。</summary>
        void ApplyFallbackPose()
        {
            if (_lUpper != null) _lUpper.localRotation = Quaternion.Euler(0f, 0f, 65f);
            if (_rUpper != null) _rUpper.localRotation = Quaternion.Euler(0f, 0f, -65f);
        }

        /// <summary>同期された頭トランスフォームの向きを、首と頭に配分して当てる。</summary>
        void ApplyHeadAim()
        {
            var target = _avatar.Head;
            if (target == null || _head == null) return;

            var local = Quaternion.Inverse(_avatar.Root.rotation) * target.rotation;
            var euler = local.eulerAngles;
            float yaw   = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.y), -_headYawLimit, _headYawLimit);
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -_headPitchLimit, _headPitchLimit);

            var aim = _avatar.Root.rotation * Quaternion.Euler(pitch, yaw, 0f);
            if (_neck != null) _neck.rotation = Quaternion.Slerp(_neck.rotation, aim, 0.4f);
            _head.rotation = aim;
        }

        void ApplyHandIk()
        {
            SolveArm(_lUpper, _lLower, _lHand, _avatar.LeftHand, -1f);
            SolveArm(_rUpper, _rLower, _rHand, _avatar.RightHand, 1f);
        }

        void SolveArm(Transform upper, Transform lower, Transform hand, Transform target, float side)
        {
            if (upper == null || lower == null || hand == null || target == null) return;
            if (!target.gameObject.activeInHierarchy) return;

            // 肘は身体の外側やや下・後ろへ逃がす。VR での自然な肘の落ち方に近い。
            var chest = _chest != null ? _chest.position : upper.position;
            var hint = chest
                       + _avatar.Root.right * (side * _elbowHintDistance)
                       - _avatar.Root.up * _elbowHintDistance
                       - _avatar.Root.forward * (_elbowHintDistance * 0.5f);

            TwoBoneIk.Solve(upper, lower, hand, target.position, hint, _handIkWeight);
            hand.rotation = target.rotation;
        }
    }
}
