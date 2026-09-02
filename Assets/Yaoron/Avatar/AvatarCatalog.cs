using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// 配布するプリセット VRM の一覧 (設計書 §8)。VRM 本体はネットワークに流さず、
    /// ここに載せた ID だけを同期して、各クライアントが URL から取りに行く (ADR-3)。
    /// </summary>
    [CreateAssetMenu(menuName = "Yaoron/Avatar Catalog", fileName = "AvatarCatalog")]
    public class AvatarCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("同期される ID。一度公開したら変更しないこと。")]
            public string id;
            public string displayName;
            public Sprite thumbnail;

            [Tooltip("HTTPS の配信先。StreamingAssets を使う場合は空でよい。")]
            public string url;

            [Tooltip("StreamingAssets/ からの相対パス (url が空のときに使う)")]
            public string streamingAssetsPath;

            [Tooltip("表示用。実ファイルとの一致は検証しない。")]
            public int approximateBytes;
        }

        [SerializeField] List<Entry> _entries = new List<Entry>();

        [Header("制約 (設計書 §8)")]
        [Tooltip("これを超える VRM は読み込まない")]
        public int maxFileBytes = 15 * 1024 * 1024;

        public IReadOnlyList<Entry> Entries => _entries;

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null && _entries[i].id == id) return _entries[i];
            return null;
        }

        public Entry FirstOrDefault() => _entries.Count > 0 ? _entries[0] : null;
    }
}
