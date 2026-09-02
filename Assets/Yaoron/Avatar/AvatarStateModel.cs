#if YA_NORMCORE
using Normal.Realtime;
using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// アバターの同期状態 (設計書 §6)。位置・頭・手は RealtimeTransform に任せ、
    /// ここには「見た目を復元するのに必要な最小の状態」だけを置く。
    /// reliable = 変化がまれで取りこぼすと破綻する項目、unreliable = 毎フレーム上書きされる項目。
    /// </summary>
    [RealtimeModel]
    public partial class AvatarStateModel
    {
        [RealtimeProperty(1, true,  true)]  private string  _avatarId;
        [RealtimeProperty(2, true,  true)]  private string  _displayName;
        [RealtimeProperty(3, true,  true)]  private bool    _isVR;
        [RealtimeProperty(4, true,  true)]  private byte    _locomotion;
        [RealtimeProperty(5, false, false)] private Vector2 _moveDir;
        [RealtimeProperty(6, true,  true)]  private byte    _expression;
    }
}
#endif
