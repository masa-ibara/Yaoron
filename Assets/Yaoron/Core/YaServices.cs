using System;
using System.Collections.Generic;

namespace Yaoron.Core
{
    /// <summary>
    /// 極小のサービスロケータ。DI コンテナを持ち込むほどの規模ではないが、
    /// UI から音声実装を直接掴ませないための緩衝材は要る (Normcore を差し替える余地を残す)。
    /// シーン遷移で消えるので、登録側は OnEnable / OnDisable で対にすること。
    /// </summary>
    public static class YaServices
    {
        static readonly Dictionary<Type, object> Map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) { Unregister<T>(); return; }
            Map[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => Map.Remove(typeof(T));

        public static T Get<T>() where T : class
            => Map.TryGetValue(typeof(T), out var s) ? (T)s : null;

        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        public static void Clear() => Map.Clear();
    }
}
