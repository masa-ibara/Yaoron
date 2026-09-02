using System;
using System.Collections.Generic;
using Yaoron.Core;
using Yaoron.Net;
using UnityEngine;
#if YA_NORMCORE
using Normal.Realtime;
#endif

namespace Yaoron.Avatar
{
    /// <summary>
    /// アバターの生成と台帳。Normcore 同梱の RealtimeAvatarManager は UPM パッケージ内で編集できないため、
    /// 同等の役割をプロジェクト側に置いた (設計書 §5)。Realtime.Instantiate の所有権オプションと
    /// ローカルリグ (XR Origin / Desktop) の割り当てはここで完結させる。
    /// </summary>
    public class YaAvatarManager : MonoBehaviour, IOccupancyCounter
    {
        [SerializeField] SessionController _session;
        [SerializeField] NormcoreConfig _config;
        [Tooltip("Normcore 未導入時 / オフライン確認用に直接 Instantiate するプレハブ")]
        [SerializeField] GameObject _offlineAvatarPrefab;
        [SerializeField] Transform _spawnArea;
        [SerializeField] float _spawnRadius = 3f;

        static readonly List<YaAvatar> Avatars = new List<YaAvatar>();

        public static YaAvatarManager Instance { get; private set; }
        public static IReadOnlyList<YaAvatar> All => Avatars;

        public YaAvatar LocalAvatar { get; private set; }
        public int Occupants => Avatars.Count;

        public event Action<YaAvatar> AvatarAdded;
        public event Action<YaAvatar> AvatarRemoved;

        IRigSource _rig;
        GameObject _localInstance;

        void Awake()
        {
            Instance = this;
            YaServices.Register<IOccupancyCounter>(this);
            YaServices.Register(this);
            if (_session == null) _session = FindFirstObjectByType<SessionController>();
            if (_session != null)
            {
                _session.JoinedRoom += OnJoinedRoom;
                _session.LeftRoom += OnLeftRoom;
            }
        }

        void OnDestroy()
        {
            if (_session != null)
            {
                _session.JoinedRoom -= OnJoinedRoom;
                _session.LeftRoom -= OnLeftRoom;
            }
            if (Instance == this) Instance = null;
            YaServices.Unregister<IOccupancyCounter>();
            YaServices.Unregister<YaAvatarManager>();
        }

        // ------------------------------------------------------------ 台帳

        internal static void Register(YaAvatar avatar)
        {
            if (Avatars.Contains(avatar)) return;
            Avatars.Add(avatar);
            if (Instance != null) Instance.AvatarAdded?.Invoke(avatar);
        }

        internal static void Unregister(YaAvatar avatar)
        {
            if (!Avatars.Remove(avatar)) return;
            if (Instance == null) return;
            if (Instance.LocalAvatar == avatar) Instance.LocalAvatar = null;
            Instance.AvatarRemoved?.Invoke(avatar);
        }

        internal static void NotifyInitialized(YaAvatar avatar)
        {
            if (Instance == null) return;
            if (avatar.IsLocal) Instance.BindLocal(avatar);
        }

        // ------------------------------------------------------------ 生成

        void OnJoinedRoom(SessionController session)
        {
            _rig = YaServices.Get<IRigSource>();
            if (_rig == null) YaLog.Warn("IRigSource が見つかりません。リグをシーンに置いてください。");
            SpawnLocalAvatar();
        }

        void OnLeftRoom(SessionController session)
        {
            // Normcore 側は destroyWhenOwnerLeaves で消えるが、オフライン時は自前で片付ける。
            if (_localInstance != null && !IsNetworked) Destroy(_localInstance);
            _localInstance = null;
            LocalAvatar = null;
        }

        bool IsNetworked
        {
#if YA_NORMCORE
            get => _session != null && _session.Realtime != null;
#else
            get => false;
#endif
        }

        void SpawnLocalAvatar()
        {
            if (LocalAvatar != null) return;
            var pose = PickSpawnPose();

#if YA_NORMCORE
            if (_session != null && _session.Realtime != null)
            {
                // 所有権は本人固定・奪取禁止・退出で破棄 (設計書 §9)。
                _localInstance = Realtime.Instantiate(
                    _config.avatarPrefabName, pose.position, pose.rotation,
                    new Realtime.InstantiateOptions
                    {
                        ownedByClient               = true,
                        preventOwnershipTakeover    = true,
                        destroyWhenOwnerLeaves      = true,
                        destroyWhenLastClientLeaves = true,
                        useInstance                 = _session.Realtime,
                    });
            }
#endif
            if (_localInstance == null)
            {
                if (_offlineAvatarPrefab == null)
                {
                    YaLog.Warn("オフライン用アバタープレハブが未設定です。");
                    return;
                }
                _localInstance = Instantiate(_offlineAvatarPrefab, pose.position, pose.rotation);
                var mine = _localInstance.GetComponent<YaAvatar>();
                if (mine != null)
                {
                    mine.State.AvatarId = SettingsStore.AvatarId;
                    mine.State.DisplayName = SettingsStore.DisplayName;
                    mine.State.IsVR = PlatformProfile.IsVR;
                    mine.Initialize(true, 0);
                }
            }
        }

        /// <summary>ローカル所有が確定したアバターにリグを噛ませる。</summary>
        void BindLocal(YaAvatar avatar)
        {
            LocalAvatar = avatar;
            if (_rig == null) _rig = YaServices.Get<IRigSource>();
            var driver = avatar.GetComponentInChildren<AvatarLocalDriver>(true);
            if (driver != null) driver.Bind(avatar, _rig);
            avatar.SetHeadVisible(!PlatformProfile.IsVR);
            YaLog.Info($"ローカルアバターを生成しました (clientID {avatar.OwnerId})");
        }

        (Vector3 position, Quaternion rotation) PickSpawnPose()
        {
            var origin = _spawnArea != null ? _spawnArea.position : Vector3.zero;
            var circle = UnityEngine.Random.insideUnitCircle * _spawnRadius;
            var pos = origin + new Vector3(circle.x, 0f, circle.y);
            var yaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            return (pos, yaw);
        }
    }
}
