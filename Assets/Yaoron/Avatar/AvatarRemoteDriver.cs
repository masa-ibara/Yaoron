using Yaoron.Core;
using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// リモートアバター側。位置・頭・手は RealtimeTransform の Smoothing に任せ、
    /// ここでは moveDir の補間 (0.1 s) と距離 LOD の判定だけを行う (設計書 §6, §12)。
    /// </summary>
    public class AvatarRemoteDriver : MonoBehaviour
    {
        [SerializeField] YaAvatar _avatar;
        [SerializeField] float _moveDirSmoothing = 0.1f;

        [Header("LOD (設計書 §6)")]
        [SerializeField] float _ikCutoffDistance = 15f;
        [SerializeField] float _animatorThrottleDistance = 30f;
        [SerializeField] float _throttledAnimatorHz = 10f;

        Vector2 _smoothedMoveDir;
        Vector2 _moveDirVelocity;
        Transform _viewer;
        float _nextAnimatorTick;

        void Awake()
        {
            if (_avatar == null) _avatar = GetComponent<YaAvatar>();
        }

        void Update()
        {
            if (_avatar == null) return;

            _smoothedMoveDir = Vector2.SmoothDamp(_smoothedMoveDir, _avatar.State.MoveDir,
                ref _moveDirVelocity, _moveDirSmoothing);

            var solver = _avatar.PoseSolver;
            if (solver == null) return;

            var distance = DistanceToViewer();
            solver.SetIkEnabled(distance <= _ikCutoffDistance);

            if (distance > _animatorThrottleDistance)
            {
                // 遠景は Animator の更新自体を間引く。
                if (Time.unscaledTime < _nextAnimatorTick) { solver.SetAnimatorEnabled(false); return; }
                _nextAnimatorTick = Time.unscaledTime + 1f / Mathf.Max(1f, _throttledAnimatorHz);
            }
            solver.SetAnimatorEnabled(true);
            solver.ApplyLocomotion(_avatar.State.Locomotion, _smoothedMoveDir);
        }

        float DistanceToViewer()
        {
            if (_viewer == null)
            {
                var cam = Camera.main;
                if (cam == null) return 0f;
                _viewer = cam.transform;
            }
            var a = _viewer.position;
            var b = _avatar.Root.position;
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
