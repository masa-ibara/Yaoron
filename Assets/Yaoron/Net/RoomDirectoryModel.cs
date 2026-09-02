#if YA_NORMCORE
using Normal.Realtime;
using Normal.Realtime.Serialization;

namespace Yaoron.Net
{
    /// <summary>ロビーに置く 1 インスタンス分の在室情報。</summary>
    [RealtimeModel]
    public partial class RoomEntryModel
    {
        [RealtimeProperty(1, true, true)] private string _roomName;
        [RealtimeProperty(2, true, true)] private int    _occupants;
        [RealtimeProperty(3, true, true)] private double _updatedAt;
    }

    /// <summary>
    /// 固定ルーム "plaza-lobby" に置く、インスタンス名 → 人数の辞書 (設計書 §9)。
    /// Normcore にはセッション一覧 API が無いため、各インスタンスの先頭クライアントが
    /// 自分のルームの人数をここに書き込む。
    /// </summary>
    [RealtimeModel]
    public partial class RoomDirectoryModel
    {
        [RealtimeProperty(1, true)] private RealtimeDictionary<RoomEntryModel> _rooms;
    }
}
#endif
