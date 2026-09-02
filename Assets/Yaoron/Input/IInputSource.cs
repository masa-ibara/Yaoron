using UnityEngine;

namespace Yaoron.Inputs
{
    /// <summary>
    /// デバイス差を吸収した入力。Desktop / Touch / XR の 3 実装があり、
    /// PlatformProfile が起動時にどれを生かすかを決める (設計書 §10)。
    /// </summary>
    public interface IInputSource
    {
        /// <summary>x = 左右, y = 前後 (-1..1)。</summary>
        Vector2 Move { get; }

        /// <summary>視点の増分。x = yaw, y = pitch (度)。</summary>
        Vector2 Look { get; }

        /// <summary>VR のスナップターン入力。-1 / 0 / +1 を 1 回だけ返す。</summary>
        float SnapTurn { get; }

        bool JumpPressed { get; }
        bool Sprint { get; }

        /// <summary>PTT を押している間 true。</summary>
        bool PushToTalkHeld { get; }

        bool MuteTogglePressed { get; }

        /// <summary>一人称 / 三人称の切り替え (非VR のみ)。</summary>
        bool ViewTogglePressed { get; }

        /// <summary>毎フレーム 1 回、リグから呼ぶ。押下フラグはこの呼び出し単位で消費される。</summary>
        void Tick();
    }
}
