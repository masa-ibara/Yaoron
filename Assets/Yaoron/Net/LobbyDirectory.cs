#if YA_NORMCORE
using System.Collections;
using System.Collections.Generic;
using Yaoron.Core;
using Normal.Realtime;
using UnityEngine;

namespace Yaoron.Net
{
    /// <summary>
    /// ロビー (第2段階)。ワールド用とは別の Realtime インスタンスで固定ルームに接続し、
    /// 自分がいるインスタンスの人数を 5 秒ごとに書き込む / 全インスタンスの人数を読む。
    /// MVP では空き番号を順に試すだけで成立するので、既定では無効 (_enabled = false)。
    /// </summary>
    [RequireComponent(typeof(Realtime))]
    public class LobbyDirectory : MonoBehaviour
    {
        [SerializeField] bool _enabled;
        [SerializeField] NormcoreConfig _config;
        [SerializeField] SessionController _session;
        [SerializeField] float _publishInterval = 5f;

        Realtime _realtime;
        RoomDirectoryComponent _directory;
        RoomDirectoryModel _model;

        public bool Ready => _model != null;

        void Awake()
        {
            _realtime = GetComponent<Realtime>();
            _realtime.didConnectToRoom += OnConnected;
        }

        void OnDestroy()
        {
            if (_realtime != null) _realtime.didConnectToRoom -= OnConnected;
        }

        void Start()
        {
            if (!_enabled) { enabled = false; return; }
            _realtime.Connect(_config.lobbyRoomName);
        }

        void OnConnected(Realtime realtime)
        {
            // ロビーの辞書は最初に来たクライアントが作り、以降は Datastore から復元される。
            var view = Realtime.Instantiate("RoomDirectory", new Realtime.InstantiateOptions
            {
                ownedByClient               = false,
                preventOwnershipTakeover    = false,
                destroyWhenOwnerLeaves      = false,
                destroyWhenLastClientLeaves = false,
                useInstance                 = _realtime,
            });
            _directory = view != null ? view.GetComponent<RoomDirectoryComponent>() : null;
            _model = _directory != null ? _directory.Model : null;
            StartCoroutine(PublishLoop());
        }

        IEnumerator PublishLoop()
        {
            var wait = new WaitForSeconds(_publishInterval);
            while (true)
            {
                yield return wait;
                if (_model == null || _session == null || !_session.IsConnected) continue;
                Publish(_session.RoomName, YaServices.TryGet<IOccupancyCounter>(out var c) ? c.Occupants : 0);
            }
        }

        public void Publish(string roomName, int occupants)
        {
            if (_model == null || string.IsNullOrEmpty(roomName)) return;
            var key = KeyOf(roomName);
            if (!_model.rooms.TryGetValue(key, out var entry))
            {
                entry = new RoomEntryModel();
                _model.rooms.Add(key, entry);
            }
            entry.roomName  = roomName;
            entry.occupants = occupants;
            entry.updatedAt = _realtime.roomTime;
        }

        /// <summary>RealtimeDictionary のキーは uint なので、ルーム名から安定したハッシュを作る。</summary>
        static uint KeyOf(string roomName)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (var c in roomName) { hash ^= c; hash *= 16777619u; }
                return hash;
            }
        }

        /// <summary>UI 用。人数が少ない順のインスタンス一覧。</summary>
        public List<(string room, int occupants)> Snapshot()
        {
            var list = new List<(string, int)>();
            if (_model == null) return list;
            foreach (var kvp in _model.rooms) list.Add((kvp.Value.roomName, kvp.Value.occupants));
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return list;
        }
    }

    /// <summary>ロビーの辞書を保持するだけの RealtimeComponent。</summary>
    public class RoomDirectoryComponent : RealtimeComponent<RoomDirectoryModel>
    {
        public RoomDirectoryModel Model => model;
    }
}
#endif
