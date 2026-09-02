using UnityEngine;

namespace Yaoron.Net
{
    /// <summary>
    /// ルーム命名・人数上限・再接続など、コードに埋めたくない運用パラメータ。
    /// Normcore の App Key 自体は SDK 付属の NormcoreAppSettings アセットが持つ。
    /// </summary>
    [CreateAssetMenu(menuName = "Yaoron/Normcore Config", fileName = "NormcoreConfig")]
    public class NormcoreConfig : ScriptableObject
    {
        [Header("入室方式")]
        [Tooltip("Normcore 3 の Quickmatch を使う。空きのある部屋へサーバー側が振り分けるので、" +
                 "アプリ側の満室判定と連番の繰り上げが不要になる (設計書 §9 の前提が SDK 側で解決された)")]
        public bool useQuickmatch = true;

        [Tooltip("Quickmatch のルームグループ名。空なら worldId を使う")]
        public string roomGroupName = "";

        [Header("ルーム")]
        [Tooltip("roomName = \"{worldId}-{instance}\" (設計書 §4)")]
        public string worldId = "plaza";

        [Tooltip("Normcore 側に人数上限が無いため、アプリで判定する上限 (設計書 §9)")]
        [Min(1)] public int maxPlayersPerRoom = 30;

        [Tooltip("満室時に試す連番インスタンスの最大数")]
        [Min(1)] public int maxInstances = 8;

        [Tooltip("人数一覧を置く固定ルーム。第2段階のロビー UI 用 (設計書 §9)")]
        public string lobbyRoomName = "plaza-lobby";

        [Header("接続")]
        [Min(0)] public int reconnectAttempts = 3;
        [Min(0.5f)] public float reconnectDelaySeconds = 2f;

        [Tooltip("接続直後、人数判定の前に Datastore スナップショットを待つ秒数")]
        [Min(0f)] public float capacityCheckDelaySeconds = 0.75f;

        [Tooltip("モバイルがバックグラウンドに入ってから切断するまでの秒数。無人ルームを延命させない (設計書 §9)")]
        [Min(0f)] public float backgroundDisconnectSeconds = 30f;

        [Header("アバター")]
        [Tooltip("Realtime.Instantiate に渡す Resources 下のプレハブ名")]
        public string avatarPrefabName = "Avatar";

        [Header("音声")]
        [Tooltip("この距離より遠い相手はローカルでミュートする (設計書 §7)")]
        [Min(1f)] public float listenRadius = 20f;

        [Tooltip("境界での断続を防ぐヒステリシス幅。解除 = radius - hysteresis / ミュート = radius + hysteresis")]
        [Min(0f)] public float listenHysteresis = 2f;

        public string RoomName(int instance) => $"{worldId}-{Mathf.Max(1, instance)}";

        public string RoomGroup => string.IsNullOrEmpty(roomGroupName) ? worldId : roomGroupName;
    }
}
