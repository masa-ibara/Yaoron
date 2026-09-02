using System;
using System.Collections;
using Yaoron.Core;
using UnityEngine;
#if YA_NORMCORE
using Normal.Realtime;
#endif

namespace Yaoron.Net
{
    public enum SessionState { Idle, Connecting, Connected, Reconnecting, Failed }

    /// <summary>
    /// ルーム接続の入口。Normcore の Realtime をラップし、以下をアプリ側の責務として持つ:
    ///   ・"{worldId}-{instance}" の連番入室と満室時の繰り上げ (設計書 §9)
    ///   ・切断時の同一ルーム再接続 (既定 3 回)
    ///   ・モバイルがバックグラウンドに落ちたときの自動切断 (ルーム時間の節約)
    /// Normcore 未導入の状態でもオフライン単独モードとして動くので、パッケージ導入前でも
    /// アバター・入力・UI の動作確認ができる。
    /// </summary>
#if YA_NORMCORE
    [RequireComponent(typeof(Realtime))]
#endif
    public class SessionController : MonoBehaviour
    {
        [SerializeField] NormcoreConfig _config;
        [SerializeField] RoomCapacityGuard _capacityGuard;
        [SerializeField] bool _joinOnStart = true;

        public NormcoreConfig Config => _config;
        public SessionState State { get; private set; } = SessionState.Idle;
        public string RoomName { get; private set; }
        public int Instance { get; private set; } = 1;

        /// <summary>入室が確定した (満室判定も通過した) タイミング。アバター生成はこの後。</summary>
        public event Action<SessionController> JoinedRoom;
        public event Action<SessionController> LeftRoom;
        public event Action<SessionState> StateChanged;

        int _reconnectsLeft;
        bool _leaveRequested;
#if YA_NORMCORE
        bool _switchingInstance;
#endif
        Coroutine _backgroundTimer;

#if YA_NORMCORE
        Realtime _realtime;
        public Realtime Realtime => _realtime;
        public bool IsConnected => _realtime != null && _realtime.connected;
        public int ClientId => IsConnected ? _realtime.clientID : -1;
#else
        public bool IsConnected => State == SessionState.Connected;
        public int ClientId => 0;
#endif

        void Awake()
        {
            if (_config == null) _config = ScriptableObject.CreateInstance<NormcoreConfig>();
            if (_capacityGuard == null) _capacityGuard = GetComponent<RoomCapacityGuard>();
            YaServices.Register(this);

#if YA_NORMCORE
            _realtime = GetComponent<Realtime>();
            EnsureAppSettings();
            DisableJoinRoomOnStart();
            _realtime.didConnectToRoom += OnDidConnect;
            _realtime.didDisconnectFromRoom += OnDidDisconnect;
#endif
        }

#if YA_NORMCORE
        /// <summary>
        /// App Key が入った NormcoreAppSettings がインスペクタで未割り当てなら、
        /// Resources から拾って割り当てる。Normcore のセットアップが
        /// Assets/Normal/Resources/NormcoreAppSettings.asset を作る前提。
        /// </summary>
        void EnsureAppSettings()
        {
            if (_realtime.normcoreAppSettings != null) return;

            var settings = Resources.Load<Normal.NormcoreAppSettings>("NormcoreAppSettings");
            if (settings != null)
            {
                _realtime.normcoreAppSettings = settings;
                YaLog.Info("NormcoreAppSettings を Resources から割り当てました。");
                return;
            }
            YaLog.Error("NormcoreAppSettings が見つかりません。Normcore のダッシュボードで取得した App Key を " +
                        "Assets/Normal/Resources/NormcoreAppSettings.asset に設定してください。");
        }

        /// <summary>
        /// Realtime 自身の自動接続を止める。入室のタイミング (マイク権限のあと・ボタン起点) と
        /// 接続先 (Quickmatch グループ) はこのクラスが決めるので、二重接続になると
        /// 別のルームに入ってしまう。setter が無いのでシリアライズフィールドを直接落とす。
        /// </summary>
        void DisableJoinRoomOnStart()
        {
            if (!_realtime.joinRoomOnStart) return;

            var field = typeof(Realtime).GetField("_joinRoomOnStart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                YaLog.Warn("Realtime の Join Room On Start を無効化できませんでした。" +
                           "インスペクタで手動オフにしてください。");
                return;
            }
            field.SetValue(_realtime, false);
            YaLog.Info("Realtime の Join Room On Start を無効化しました (接続は SessionController が管理します)。");
        }
#endif

        void OnDestroy()
        {
#if YA_NORMCORE
            if (_realtime != null)
            {
                _realtime.didConnectToRoom -= OnDidConnect;
                _realtime.didDisconnectFromRoom -= OnDidDisconnect;
            }
#endif
            if (YaServices.Get<SessionController>() == this) YaServices.Unregister<SessionController>();
        }

        void Start()
        {
            if (_joinOnStart) Join();
        }

        // ------------------------------------------------------------------ 接続

        public void Join(string worldId = null, int instance = 1)
        {
            if (!string.IsNullOrEmpty(worldId)) _config.worldId = worldId;
            _leaveRequested = false;
            Instance = Mathf.Max(1, instance);
            _reconnectsLeft = _config.reconnectAttempts;
            Connect();
        }

        public void Leave()
        {
            _leaveRequested = true;
#if YA_NORMCORE
            if (_realtime != null) _realtime.Disconnect();
            SetState(SessionState.Idle);
#else
            SetState(SessionState.Idle);
            LeftRoom?.Invoke(this);
#endif
        }

        void Connect()
        {
            RoomName = _config.RoomName(Instance);
            SetState(State == SessionState.Connected ? SessionState.Reconnecting : SessionState.Connecting);
#if YA_NORMCORE
            if (_config.useQuickmatch)
            {
                // Normcore 3 の Quickmatch。定員に空きのある部屋へサーバー側が割り当てるので、
                // 実際のルーム名は接続後に realtime.roomName で分かる。
                RoomName = _config.RoomGroup;
                YaLog.Info($"Quickmatch 接続: グループ {_config.RoomGroup} / 定員 {_config.maxPlayersPerRoom}");
                _realtime.ConnectToNextAvailableQuickmatchRoom(_config.RoomGroup, _config.maxPlayersPerRoom);
            }
            else
            {
                YaLog.Info($"ルーム接続: {RoomName}");
                _realtime.Connect(RoomName);
            }
#else
            YaLog.Info($"ルーム接続: {RoomName}");
            StartCoroutine(FakeConnect());
#endif
        }

#if !YA_NORMCORE
        IEnumerator FakeConnect()
        {
            yield return null;
            if (_leaveRequested) yield break;
            SettingsStore.LastRoom = RoomName;
            SetState(SessionState.Connected);
            YaLog.Info("Normcore 未導入のためオフライン単独モードで起動しました。");
            JoinedRoom?.Invoke(this);
        }
#endif

#if YA_NORMCORE
        void OnDidConnect(Realtime realtime)
        {
            StartCoroutine(AfterConnect());
        }

        /// <summary>
        /// 接続直後は Datastore のスナップショットが届き切っていないので、少し待ってから人数を数える。
        /// 同時入室の競合で一瞬 31 人になるのは許容し、次の参加者を弾く (設計書 §9)。
        /// </summary>
        IEnumerator AfterConnect()
        {
            yield return new WaitForSeconds(_config.capacityCheckDelaySeconds);
            if (!IsConnected) yield break;

            // Quickmatch のときはサーバーが割り当てた実際のルーム名を採る。
            if (!string.IsNullOrEmpty(_realtime.roomName)) RoomName = _realtime.roomName;

            int occupants = _capacityGuard != null ? _capacityGuard.CountOccupants() : 0;

            // 定員はサーバー側で守られるため、Quickmatch では繰り上げ判定を行わない。
            if (!_config.useQuickmatch && occupants >= _config.maxPlayersPerRoom && Instance < _config.maxInstances)
            {
                YaLog.Info($"{RoomName} は満室 ({occupants}/{_config.maxPlayersPerRoom})。次のインスタンスへ。");
                Instance++;
                _switchingInstance = true;
                _realtime.Disconnect();
                yield return null;
                Connect();
                yield break;
            }

            if (!_config.useQuickmatch && occupants >= _config.maxPlayersPerRoom)
            {
                YaLog.Warn($"全インスタンスが満室です ({RoomName})。");
                SetState(SessionState.Failed);
                _realtime.Disconnect();
                yield break;
            }

            _reconnectsLeft = _config.reconnectAttempts;
            SettingsStore.LastRoom = RoomName;
            SetState(SessionState.Connected);
            YaLog.Info($"入室しました: {RoomName} (先客 {occupants} 人 / clientID {ClientId})");
            JoinedRoom?.Invoke(this);
        }

        void OnDidDisconnect(Realtime realtime)
        {
            LeftRoom?.Invoke(this);

            // インスタンス繰り上げ中の意図的な切断は AfterConnect が続きを持っている。
            if (_switchingInstance) { _switchingInstance = false; return; }

            if (_leaveRequested || State == SessionState.Failed)
            {
                SetState(SessionState.Idle);
                return;
            }

            if (_reconnectsLeft > 0)
            {
                _reconnectsLeft--;
                SetState(SessionState.Reconnecting);
                YaLog.Warn($"切断されました。再接続します (残り {_reconnectsLeft} 回)");
                StartCoroutine(ReconnectAfterDelay());
            }
            else
            {
                YaLog.Error("再接続に失敗しました。");
                SetState(SessionState.Failed);
            }
        }

        IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSeconds(_config.reconnectDelaySeconds);
            if (_leaveRequested) yield break;

            // 待っている間に別経路でつながっていることがある (Quickmatch の接続完了など)。
            // そのまま Connect すると Normcore に「既に接続中」と弾かれるので、ここで降りる。
            if (_realtime.connected || _realtime.connecting) yield break;

            // Quickmatch か固定ルームかの分岐は Connect() が持っている。
            Connect();
        }
#endif

        // ------------------------------------------------- バックグラウンド時の切断

        void OnApplicationPause(bool paused)
        {
            if (_config.backgroundDisconnectSeconds <= 0f) return;
            if (paused)
            {
                if (IsConnected && _backgroundTimer == null) _backgroundTimer = StartCoroutine(BackgroundTimeout());
            }
            else if (_backgroundTimer != null)
            {
                StopCoroutine(_backgroundTimer);
                _backgroundTimer = null;
            }
        }

        IEnumerator BackgroundTimeout()
        {
            yield return new WaitForSecondsRealtime(_config.backgroundDisconnectSeconds);
            _backgroundTimer = null;
            if (!IsConnected) yield break;
            YaLog.Info("バックグラウンドが続いたので切断します。");
            Leave();
        }

        void SetState(SessionState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
