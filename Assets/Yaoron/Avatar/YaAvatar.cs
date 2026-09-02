using Yaoron.Core;
using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// アバター 1 体の器。Normcore の RealtimeAvatar 相当をプロジェクト側に持ち直したもので、
    /// ルート / 頭 / 両手のトランスフォーム、同期状態、VRM の実体をまとめて抱える (設計書 §5, §6)。
    /// ネットワーク依存はすべて AvatarSync 側にあるため、このクラスは Normcore 無しでも動く。
    /// </summary>
    public class YaAvatar : MonoBehaviour
    {
        [Header("トラッキング対象 (RealtimeTransform を付けておく)")]
        [SerializeField] Transform _root;
        [SerializeField] Transform _head;
        [SerializeField] Transform _leftHand;
        [SerializeField] Transform _rightHand;

        [Header("見た目")]
        [Tooltip("VRM をぶら下げる親。ルートのローカル原点に置くこと。")]
        [SerializeField] Transform _modelParent;
        [SerializeField] AvatarPlaceholder _placeholder;

        [Header("ドライバ")]
        [SerializeField] AvatarLocalDriver _localDriver;
        [SerializeField] AvatarRemoteDriver _remoteDriver;
        [SerializeField] AvatarPoseSolver _poseSolver;

        public AvatarState State { get; } = new AvatarState();

        public Transform Root => _root != null ? _root : transform;
        public Transform Head => _head;
        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;
        public Transform ModelParent => _modelParent != null ? _modelParent : Root;
        public AvatarPoseSolver PoseSolver => _poseSolver;
        public AvatarPlaceholder Placeholder => _placeholder;

        /// <summary>自分が操作するアバターか。</summary>
        public bool IsLocal { get; private set; }

        /// <summary>Normcore の clientID。オフライン時は 0。</summary>
        public int OwnerId { get; private set; }

        /// <summary>読み込み済み VRM のルート (未ロード時は null)。</summary>
        public GameObject Model { get; private set; }

        bool _initialized;

        void Awake() => ConfigureAudio();

        /// <summary>
        /// Web はブラウザが音声を直接鳴らすため距離減衰が効かない。3D 設定を残しても意味がないので
        /// 2D に落とし、距離制御は VoiceRangeCuller に一本化する (設計書 ADR-4)。
        /// </summary>
        void ConfigureAudio()
        {
            var source = GetComponentInChildren<AudioSource>(true);
            if (source == null) return;
            bool flat = PlatformProfile.Voice == VoiceSpatialization.Flat2D;
            source.spatialBlend = flat ? 0f : 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 20f;
        }

        void OnEnable() => YaAvatarManager.Register(this);
        void OnDisable() => YaAvatarManager.Unregister(this);

        /// <summary>
        /// 所有者が確定した時点で 1 度だけ呼ばれる。ローカルなら入力ドライバ、
        /// リモートなら補間 + ポーズ復元ドライバだけを生かす。
        /// </summary>
        public void Initialize(bool isLocal, int ownerId)
        {
            IsLocal = isLocal;
            OwnerId = ownerId;

            if (_localDriver != null) _localDriver.enabled = isLocal;
            if (_remoteDriver != null) _remoteDriver.enabled = !isLocal;

            if (!_initialized)
            {
                _initialized = true;
                State.AvatarIdChanged += OnAvatarIdChanged;
            }

            name = isLocal ? "Avatar (Local)" : $"Avatar ({ownerId})";
            YaAvatarManager.NotifyInitialized(this);
            if (!string.IsNullOrEmpty(State.AvatarId)) OnAvatarIdChanged(State.AvatarId);
        }

        void OnDestroy()
        {
            State.AvatarIdChanged -= OnAvatarIdChanged;
        }

        /// <summary>avatarId は全クライアントで監視され、各自が VRM を取りに行く (設計書 §8)。</summary>
        void OnAvatarIdChanged(string avatarId)
        {
            if (string.IsNullOrEmpty(avatarId)) return;
            var loader = YaServices.Get<AvatarLoader>();
            if (loader == null)
            {
                YaLog.Warn("AvatarLoader が未登録のため VRM を読み込めません。");
                return;
            }
            loader.LoadIntoAsync(this, avatarId).Forget();
        }

        /// <summary>AvatarLoader から呼ばれる。旧モデルを捨てて差し替え、ポーズ解決を繋ぎ直す。</summary>
        public void SetModel(GameObject model)
        {
            if (Model != null && Model != model) Destroy(Model);
            Model = model;
            if (model != null)
            {
                var t = model.transform;
                t.SetParent(ModelParent, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }
            if (_placeholder != null) _placeholder.SetVisible(model == null);
            if (_poseSolver != null) _poseSolver.Bind(this, model);
        }

        /// <summary>VR の自視点では自分の頭を消す (設計書 §10)。</summary>
        public void SetHeadVisible(bool visible)
        {
            if (_poseSolver != null) _poseSolver.SetHeadVisible(visible);
        }
    }
}
