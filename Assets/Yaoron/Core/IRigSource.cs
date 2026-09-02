using UnityEngine;

namespace Yaoron.Core
{
    /// <summary>
    /// ローカルプレイヤーの身体そのもの (Desktop / Mobile / XR リグ)。
    /// アバターはこのトランスフォームを追従するだけで、移動計算はリグ側が持つ (設計書 §10)。
    /// </summary>
    public interface IRigSource
    {
        Transform Root { get; }
        Transform Head { get; }
        Transform LeftHand { get; }
        Transform RightHand { get; }

        /// <summary>VR のみ true。非VR では手を同期しない (設計書 §6)。</summary>
        bool HasHands { get; }

        /// <summary>ブレンドツリー用の入力方向 (-1..1)。</summary>
        Vector2 MoveInput { get; }

        bool IsRunning { get; }
        bool IsGrounded { get; }

        void Teleport(Vector3 position, float yawDegrees);
    }
}
