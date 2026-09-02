using Yaoron.Core;
using UnityEngine;

namespace Yaoron.Net
{
    /// <summary>
    /// Normcore にはルーム人数上限が無いため、接続直後にアプリ側で判定する (設計書 §9, §13)。
    /// 厳密さは求めない: 同時入室の競合で一瞬 30 人を超えることは許容し、次の参加者から弾く。
    /// </summary>
    public class RoomCapacityGuard : MonoBehaviour
    {
        [SerializeField] NormcoreConfig _config;

        public int MaxPlayers => _config != null ? _config.maxPlayersPerRoom : 30;

        /// <summary>自分のアバターを生成する前に呼ぶと「先客の数」になる。</summary>
        public int CountOccupants()
        {
            if (YaServices.TryGet<IOccupancyCounter>(out var counter)) return counter.Occupants;
            return 0;
        }

        public bool IsFull => CountOccupants() >= MaxPlayers;

        /// <summary>HUD 表示用。「12 / 30」。</summary>
        public string OccupancyLabel => $"{CountOccupants()} / {MaxPlayers}";
    }
}
