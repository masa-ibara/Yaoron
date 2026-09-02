using Yaoron.Core;
using Yaoron.Inputs;
using Yaoron.Voice;
using UnityEngine;
using UnityEngine.XR;

namespace Yaoron.XR
{
    /// <summary>
    /// VR のプレイヤー本体 (Quest / PC VR)。XR Origin と同じ構造 —
    /// ルート → カメラオフセット → (カメラ, 左手, 右手) — を自前で持ち、
    /// HMD とコントローラの姿勢を InputDevices から直接当てる。
    /// これで XRI のバージョン差に引きずられず、YaAvatar には頭と両手をそのまま渡せる (設計書 §10)。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class XRPlayerRig : MonoBehaviour, IRigSource
    {
        [Header("構造")]
        [SerializeField] Transform _cameraOffset;
        [SerializeField] Camera _camera;
        [SerializeField] Transform _leftHand;
        [SerializeField] Transform _rightHand;

        [Header("移動")]
        [SerializeField] float _moveSpeed = 2.2f;
        [SerializeField] float _gravity = -18f;
        [SerializeField] float _snapTurnDegrees = 45f;

        [Header("身体")]
        [Tooltip("床から見た標準の目線高さ。トラッキング原点が Floor でない場合の補正に使う。")]
        [SerializeField] float _fallbackEyeHeight = 1.6f;

        CharacterController _controller;
        XRInput _input;
        float _verticalVelocity;
        bool _trackingOriginConfigured;

        public Transform Root => transform;
        public Transform Head => _camera != null ? _camera.transform : transform;
        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;
        public bool HasHands => true;
        public Vector2 MoveInput { get; private set; }
        public bool IsRunning => false;
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponentInChildren<XRInput>(true);
            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            YaServices.Register<IRigSource>(this);
        }

        void OnDestroy()
        {
            if (YaServices.Get<IRigSource>() == (IRigSource)this) YaServices.Unregister<IRigSource>();
        }

        void Start() => ConfigureTrackingOrigin();

        /// <summary>床基準のトラッキングにできれば、身長差がそのまま反映される。</summary>
        void ConfigureTrackingOrigin()
        {
            var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var subsystem in subsystems)
            {
                if (subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                {
                    _trackingOriginConfigured = true;
                    subsystem.TryRecenter();
                }
            }
            if (!_trackingOriginConfigured && _cameraOffset != null)
                _cameraOffset.localPosition = new Vector3(0f, _fallbackEyeHeight, 0f);
        }

        void Update()
        {
            if (_input == null) return;
            _input.Tick();

            ApplyTrackedPoses();
            ApplySnapTurn(_input.SnapTurn);
            ApplyMove(_input.Move);
            ApplyVoiceInput();
            SyncCapsuleToHead();
        }

        void ApplyTrackedPoses()
        {
            ApplyNode(XRNode.Head, Head);
            ApplyNode(XRNode.LeftHand, _leftHand);
            ApplyNode(XRNode.RightHand, _rightHand);
        }

        void ApplyNode(XRNode node, Transform target)
        {
            if (target == null) return;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return;
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out var position))
                target.localPosition = position;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
                target.localRotation = rotation;
        }

        void ApplyMove(Vector2 move)
        {
            MoveInput = move;

            // 進行方向は HMD の向き基準。頭のピッチは無視して水平成分だけ使う。
            var head = Head;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            var world = (right * move.x + forward * move.y) * _moveSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += _gravity * Time.deltaTime;

            _controller.Move((world + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        /// <summary>回転は頭の位置を軸にする。ルートを回すと視界が横滑りして酔う。</summary>
        void ApplySnapTurn(float direction)
        {
            if (Mathf.Approximately(direction, 0f)) return;
            var pivot = Head.position;
            transform.RotateAround(pivot, Vector3.up, direction * _snapTurnDegrees);
        }

        /// <summary>当たり判定のカプセルを頭の真下に置き、実際に立っている場所と合わせる。</summary>
        void SyncCapsuleToHead()
        {
            var local = transform.InverseTransformPoint(Head.position);
            float height = Mathf.Clamp(local.y, 1.0f, 2.2f);
            _controller.height = height;
            _controller.center = new Vector3(local.x, height * 0.5f, local.z);
        }

        void ApplyVoiceInput()
        {
            var voice = YaServices.Get<NormcoreVoiceService>();
            if (voice == null) return;
            voice.PushToTalkHeld = _input.PushToTalkHeld;
            if (_input.MuteTogglePressed) voice.IsMuted = !voice.IsMuted;
        }

        public void Teleport(Vector3 position, float yawDegrees)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            _controller.enabled = true;
            _verticalVelocity = 0f;
        }
    }
}
