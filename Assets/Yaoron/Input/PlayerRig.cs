using Yaoron.Core;
using Yaoron.Voice;
using UnityEngine;

namespace Yaoron.Inputs
{
    /// <summary>
    /// 非VR のプレイヤー本体 (Desktop / Mobile 共通)。CharacterController で歩き、
    /// カメラは一人称。アバターはこのトランスフォームを追従するだけで、移動計算はここにしかない。
    /// IRigSource としてアバター層に自分を渡す (設計書 §10)。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerRig : MonoBehaviour, IRigSource
    {
        [Header("参照")]
        [SerializeField] Camera _camera;
        [Tooltip("同期される頭の位置。カメラと同じ場所でよい。")]
        [SerializeField] Transform _head;

        [Header("移動")]
        [SerializeField] float _walkSpeed = 2.0f;
        [SerializeField] float _runSpeed = 4.5f;
        [SerializeField] float _jumpHeight = 1.1f;
        [SerializeField] float _gravity = -18f;
        [SerializeField] float _acceleration = 14f;

        [Header("視点")]
        [SerializeField] float _pitchLimit = 80f;

        [Header("カメラ (設計書 §10: デスクトップは肩越し 2.5 m で自分の全身が見える)")]
        [Tooltip("起動時に三人称にする。V キーで一人称と切り替え。")]
        [SerializeField] bool _thirdPerson = true;
        [SerializeField] float _thirdPersonDistance = 2.5f;
        [SerializeField] Vector2 _shoulderOffset = new Vector2(0.4f, 0.1f);
        [Tooltip("壁にめり込まないよう、この半径で後方を調べる")]
        [SerializeField] float _cameraProbeRadius = 0.2f;

        CharacterController _controller;
        IInputSource _input;
        Vector3 _velocity;
        Vector2 _planarVelocity;
        float _pitch;
        Transform _cameraTransform;
        bool _cameraIsHead;

        public Transform Root => transform;
        public Transform Head => _head != null ? _head : (_camera != null ? _camera.transform : transform);
        public Transform LeftHand => null;
        public Transform RightHand => null;
        public bool HasHands => false;
        public Vector2 MoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            _input = SelectInput();
            SetupCamera();
            YaServices.Register<IRigSource>(this);
        }

        void OnDestroy()
        {
            if (YaServices.Get<IRigSource>() == (IRigSource)this) YaServices.Unregister<IRigSource>();
        }

        /// <summary>プラットフォームに応じて有効な入力実装をひとつだけ生かす (設計書 §10)。</summary>
        IInputSource SelectInput()
        {
            var desktop = GetComponentInChildren<DesktopInput>(true);
            var touch = GetComponentInChildren<TouchInput>(true);

            bool useTouch = PlatformProfile.IsMobile;
            if (desktop != null) desktop.enabled = !useTouch;
            if (touch != null) touch.gameObject.SetActive(useTouch);

            if (useTouch && touch != null) return touch;
            if (desktop != null) return desktop;
            YaLog.Warn("入力コンポーネントが見つかりません。");
            return null;
        }

        /// <summary>
        /// 頭 (同期される位置) とカメラは別のトランスフォームでなければならない。
        /// 同じだと三人称にした瞬間、他人に見える自分の頭ごと後ろへ下がってしまう。
        /// </summary>
        void SetupCamera()
        {
            if (_camera == null) return;
            _cameraTransform = _camera.transform;
            _cameraIsHead = _cameraTransform == Head;
            if (_cameraIsHead && _thirdPerson)
            {
                _thirdPerson = false;
                YaLog.Warn("カメラが Head と同じ GameObject にあるため一人称で起動します。" +
                           "Yaoron ▸ セットアップ/シーンを作成 でシーンを作り直すと三人称が使えます。");
            }
        }

        void Update()
        {
            if (_input == null) return;
            _input.Tick();

            if (_input.ViewTogglePressed && !_cameraIsHead) _thirdPerson = !_thirdPerson;

            ApplyLook(_input.Look);
            ApplyMove(_input.Move, _input.Sprint, _input.JumpPressed);
            ApplyVoiceInput();

            if (_input is TouchInput touch) touch.ConsumeButtons();
        }

        void LateUpdate() => ApplyCameraOffset();

        /// <summary>三人称のときだけカメラを頭の後方へ引く。壁があれば手前で止める。</summary>
        void ApplyCameraOffset()
        {
            if (_cameraTransform == null || _cameraIsHead) return;

            if (!_thirdPerson)
            {
                _cameraTransform.localPosition = Vector3.zero;
                _cameraTransform.localRotation = Quaternion.identity;
                return;
            }

            var head = Head;
            var wanted = new Vector3(_shoulderOffset.x, _shoulderOffset.y, -_thirdPersonDistance);
            var origin = head.position + head.rotation * new Vector3(_shoulderOffset.x, _shoulderOffset.y, 0f);
            var direction = -head.forward;

            // 始点が自分のカプセルの中にあるので、自分自身のコライダーは無視して一番近い壁を探す。
            var hits = Physics.SphereCastAll(origin, _cameraProbeRadius, direction, _thirdPersonDistance,
                                             ~0, QueryTriggerInteraction.Ignore);
            float nearest = _thirdPersonDistance;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance <= 0f) continue;
                nearest = Mathf.Min(nearest, hit.distance - _cameraProbeRadius);
            }
            wanted.z = -Mathf.Max(0.2f, nearest);

            _cameraTransform.localPosition = wanted;
            _cameraTransform.localRotation = Quaternion.identity;
        }

        void ApplyLook(Vector2 look)
        {
            if (look.sqrMagnitude > 0f)
            {
                transform.Rotate(Vector3.up, look.x, Space.World);
                _pitch = Mathf.Clamp(_pitch - look.y, -_pitchLimit, _pitchLimit);
            }
            var head = Head;
            if (head != null) head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void ApplyMove(Vector2 move, bool sprint, bool jump)
        {
            MoveInput = move;
            IsRunning = sprint && move.sqrMagnitude > 0.01f;

            float speed = IsRunning ? _runSpeed : _walkSpeed;
            var wanted = new Vector2(move.x, move.y) * speed;
            // 加速を挟んで、キー入力の立ち上がりで locomotion が跳ねないようにする。
            _planarVelocity = Vector2.MoveTowards(_planarVelocity, wanted, _acceleration * Time.deltaTime);

            var world = transform.right * _planarVelocity.x + transform.forward * _planarVelocity.y;

            if (_controller.isGrounded)
            {
                if (_velocity.y < 0f) _velocity.y = -2f;
                if (jump) _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
            _velocity.y += _gravity * Time.deltaTime;

            _controller.Move((world + Vector3.up * _velocity.y) * Time.deltaTime);
        }

        /// <summary>PTT とミュートトグルは入力層から音声サービスへ直接橋渡しする。</summary>
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
            _velocity = Vector3.zero;
            _planarVelocity = Vector2.zero;
        }
    }
}
