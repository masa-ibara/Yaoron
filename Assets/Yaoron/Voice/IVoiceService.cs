using System;
using System.Threading.Tasks;

namespace Yaoron.Voice
{
    /// <summary>
    /// 音声機能の抽象 (設計書 §7)。UI とアバターはこの層しか見ないので、
    /// 将来 Normcore 以外へ戻す場合もここで吸収できる。
    /// 戻り値が Task なのは WebGL でも安全に扱えるようにするため (スレッドは使わない)。
    /// </summary>
    public interface IVoiceService
    {
        /// <summary>送信ミュート。</summary>
        bool IsMuted { get; set; }

        /// <summary>true の間だけ送信する (プッシュ・トゥ・トーク)。</summary>
        bool PushToTalk { get; set; }

        /// <summary>自分の発話量 (0-1)。</summary>
        float LocalLevel { get; }

        /// <summary>マイク権限が取れているか。</summary>
        bool HasMicrophonePermission { get; }

        /// <summary>誰かの発話状態が変わったとき。引数は clientID と発話中かどうか。</summary>
        event Action<int, bool> RemoteSpeakingChanged;

        Task<bool> RequestMicrophoneAsync();

        /// <summary>ブラウザ版の聴取範囲 (設計書 ADR-4)。</summary>
        void SetListenRadius(float meters);
    }
}
