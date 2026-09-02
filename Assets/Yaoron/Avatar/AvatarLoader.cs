using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Yaoron.Core;
using UnityEngine;
using UnityEngine.Networking;
#if YA_VRM
using UniGLTF;
using UniVRM10;
#endif

namespace Yaoron.Avatar
{
    /// <summary>
    /// VRM の取得と生成 (設計書 §8)。取得はメモリ → ディスク → HTTPS の順。
    /// WebGL はスレッドが無いので NoThread の IAwaitCaller に切り替える。
    /// 同じ ID を複数人が使っても、バイト列はキャッシュを共有しインスタンスだけ分ける。
    /// </summary>
    public class AvatarLoader : MonoBehaviour
    {
        [SerializeField] AvatarCatalog _catalog;
        [Tooltip("ディスクキャッシュを使う (WebGL では常に無効)")]
        [SerializeField] bool _useDiskCache = true;

        readonly Dictionary<string, byte[]> _memoryCache = new Dictionary<string, byte[]>();
        readonly Dictionary<string, Task<byte[]>> _inFlight = new Dictionary<string, Task<byte[]>>();
        readonly Dictionary<YaAvatar, string> _current = new Dictionary<YaAvatar, string>();

        public AvatarCatalog Catalog => _catalog;

        void Awake() => YaServices.Register(this);
        void OnDestroy()
        {
            if (YaServices.Get<AvatarLoader>() == this) YaServices.Unregister<AvatarLoader>();
        }

        string DiskPath(string id)
            => Path.Combine(Application.persistentDataPath, "vrm-cache", id + ".vrm");

        bool DiskCacheAvailable => _useDiskCache && !PlatformProfile.IsWeb;

        /// <summary>指定アバターに VRM を読み込んで差し替える。失敗時はプレースホルダのまま残す。</summary>
        public async Task LoadIntoAsync(YaAvatar avatar, string avatarId)
        {
            if (avatar == null || string.IsNullOrEmpty(avatarId)) return;

            // 同じアバターに対する多重リクエストは、最後に来た ID だけを反映する。
            _current[avatar] = avatarId;

            try
            {
                var bytes = await GetBytesAsync(avatarId);
                if (bytes == null || bytes.Length == 0) return;
                if (avatar == null) return;
                if (!_current.TryGetValue(avatar, out var wanted) || wanted != avatarId) return;

                var model = await InstantiateVrmAsync(bytes);
                if (model == null) return;
                if (avatar == null || !_current.TryGetValue(avatar, out wanted) || wanted != avatarId)
                {
                    Destroy(model);
                    return;
                }
                avatar.SetModel(model);
                avatar.SetHeadVisible(!(avatar.IsLocal && PlatformProfile.IsVR));
                YaLog.Info($"VRM を適用しました: {avatarId}");
            }
            catch (Exception e)
            {
                YaLog.Error($"VRM の読み込みに失敗しました ({avatarId}): {e.Message}");
            }
        }

        // ------------------------------------------------------------ バイト列の取得

        Task<byte[]> GetBytesAsync(string avatarId)
        {
            if (_memoryCache.TryGetValue(avatarId, out var cached))
                return Task.FromResult(cached);
            if (_inFlight.TryGetValue(avatarId, out var running))
                return running;

            var task = FetchBytesAsync(avatarId);
            _inFlight[avatarId] = task;
            return task;
        }

        async Task<byte[]> FetchBytesAsync(string avatarId)
        {
            try
            {
                var entry = _catalog != null ? _catalog.Find(avatarId) : null;
                if (entry == null)
                {
                    YaLog.Warn($"カタログに ID '{avatarId}' がありません。");
                    return null;
                }

                if (DiskCacheAvailable)
                {
                    var path = DiskPath(avatarId);
                    if (File.Exists(path))
                    {
                        var fromDisk = File.ReadAllBytes(path);
                        _memoryCache[avatarId] = fromDisk;
                        return fromDisk;
                    }
                }

                var url = ResolveUrl(entry);
                if (string.IsNullOrEmpty(url))
                {
                    YaLog.Warn($"ID '{avatarId}' に URL も StreamingAssets のパスもありません。");
                    return null;
                }

                var bytes = await DownloadAsync(url);
                if (bytes == null) return null;

                int limit = _catalog != null ? _catalog.maxFileBytes : 15 * 1024 * 1024;
                if (bytes.Length > limit)
                {
                    YaLog.Error($"VRM が上限 {limit / (1024 * 1024)} MB を超えています ({avatarId}: {bytes.Length / (1024 * 1024)} MB)");
                    return null;
                }

                _memoryCache[avatarId] = bytes;
                if (DiskCacheAvailable) WriteDiskCache(avatarId, bytes);
                return bytes;
            }
            finally
            {
                _inFlight.Remove(avatarId);
            }
        }

        string ResolveUrl(AvatarCatalog.Entry entry)
        {
            if (!string.IsNullOrEmpty(entry.url)) return entry.url;
            if (string.IsNullOrEmpty(entry.streamingAssetsPath)) return null;
            var full = Path.Combine(Application.streamingAssetsPath, entry.streamingAssetsPath);
            // Android / WebGL の StreamingAssets は UnityWebRequest でしか読めないので URL に揃える。
            return full.Contains("://") ? full : "file://" + full.Replace('\\', '/');
        }

        void WriteDiskCache(string avatarId, byte[] bytes)
        {
            try
            {
                var path = DiskPath(avatarId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception e)
            {
                YaLog.Warn($"ディスクキャッシュに書けませんでした: {e.Message}");
            }
        }

        Task<byte[]> DownloadAsync(string url)
        {
            var tcs = new TaskCompletionSource<byte[]>();
            YaAwait.Run(DownloadRoutine(url, tcs));
            return tcs.Task;
        }

        static IEnumerator DownloadRoutine(string url, TaskCompletionSource<byte[]> tcs)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                bool failed = request.result != UnityWebRequest.Result.Success;
                if (failed)
                {
                    YaLog.Error($"VRM の取得に失敗: {url} ({request.error})");
                    tcs.TrySetResult(null);
                    yield break;
                }
                tcs.TrySetResult(request.downloadHandler.data);
            }
        }

        // ------------------------------------------------------------ VRM の生成

        async Task<GameObject> InstantiateVrmAsync(byte[] bytes)
        {
#if YA_VRM
            var awaitCaller =
#if UNITY_WEBGL && !UNITY_EDITOR
                (IAwaitCaller)new RuntimeOnlyNoThreadAwaitCaller();   // WebGL はスレッド不可
#else
                (IAwaitCaller)new RuntimeOnlyAwaitCaller();           // フレーム分散
#endif
            var instance = await Vrm10.LoadBytesAsync(
                bytes,
                canLoadVrm0X: true,
                controlRigGenerationOption: ControlRigGenerationOption.Generate,
                showMeshes: false,
                awaitCaller: awaitCaller,
                materialGenerator: null);

            if (instance == null) return null;

            // 一人称設定 (VR の自視点で頭を隠すためのレイヤ分け) を済ませてから表示する。
            await instance.Vrm.FirstPerson.SetupAsync(instance.gameObject, awaitCaller);

            // showMeshes: false で読み込んでいるので、セットアップ完了後に自分で表示する。
            var gltfInstance = instance.GetComponent<RuntimeGltfInstance>();
            if (gltfInstance != null)
            {
                gltfInstance.ShowMeshes();
                gltfInstance.EnableUpdateWhenOffscreen();
            }
            return instance.gameObject;
#else
            await YaAwait.NextFrame();
            YaLog.Warn("UniVRM が未導入のため、VRM は読み込めません (プレースホルダのまま)。");
            return null;
#endif
        }
    }
}
