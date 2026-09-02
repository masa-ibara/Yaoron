using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Yaoron.Core
{
    /// <summary>
    /// WebGL はスレッドが無いので Task.Delay / Task.Run が使えない (設計書 §2)。
    /// 待機はすべてコルーチン駆動のこのヘルパーを経由させ、Task.* を直接触らせない。
    /// </summary>
    public static class YaAwait
    {
        class Runner : MonoBehaviour { }

        static Runner _runner;

        static Runner Host
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[YaAwait]") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<Runner>();
                }
                return _runner;
            }
        }

        public static Coroutine Run(IEnumerator routine) => Host.StartCoroutine(routine);

        public static void Stop(Coroutine routine)
        {
            if (routine != null && _runner != null) _runner.StopCoroutine(routine);
        }

        /// <summary>スレッドを使わない秒待ち。</summary>
        public static Task Seconds(float seconds)
        {
            var tcs = new TaskCompletionSource<bool>();
            Run(SecondsRoutine(seconds, tcs));
            return tcs.Task;
        }

        public static Task NextFrame()
        {
            var tcs = new TaskCompletionSource<bool>();
            Run(SecondsRoutine(0f, tcs));
            return tcs.Task;
        }

        static IEnumerator SecondsRoutine(float seconds, TaskCompletionSource<bool> tcs)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            else yield return null;
            tcs.TrySetResult(true);
        }

        /// <summary>await されない fire-and-forget を握りつぶさず必ずログに出す。</summary>
        public static async void Forget(this Task task)
        {
            try { await task; }
            catch (Exception e) { YaLog.Error($"未処理の非同期例外: {e}"); }
        }
    }
}
