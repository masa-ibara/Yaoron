using System.Diagnostics;
using UnityEngine;

namespace Yaoron.Core
{
    /// <summary>
    /// Yaoron 共通ログ。リリースビルドでは YA_VERBOSE_LOG が無い限り呼び出しごと除去される。
    /// </summary>
    public static class YaLog
    {
        const string Prefix = "[Yaoron] ";

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("YA_VERBOSE_LOG")]
        public static void Info(string message) => UnityEngine.Debug.Log(Prefix + message);

        public static void Warn(string message) => UnityEngine.Debug.LogWarning(Prefix + message);

        public static void Error(string message) => UnityEngine.Debug.LogError(Prefix + message);
    }
}
