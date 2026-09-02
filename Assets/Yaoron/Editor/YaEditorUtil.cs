using System;
using UnityEditor;
using UnityEngine;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// エディタ拡張の共通処理。Normcore / UniVRM の型は未導入だとアセンブリ参照できないので、
    /// 型名の文字列で解決して AddComponent する (導入済みなら普通に付く)。
    /// </summary>
    public static class YaEditorUtil
    {
        public static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null) return type;
                }
                catch (Exception) { }
            }
            return null;
        }

        /// <summary>型が見つからなければ何もせず null を返す (未導入時は静かに飛ばす)。</summary>
        public static Component AddComponentByName(GameObject target, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null) return null;
            var existing = target.GetComponent(type);
            return existing != null ? existing : target.AddComponent(type);
        }

        /// <summary>private [SerializeField] を名前で埋める。</summary>
        public static void SetField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[Yaoron] {target.GetType().Name}.{fieldName} が見つかりません。");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(UnityEngine.Object target, string fieldName, float value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null) return;
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(UnityEngine.Object target, string fieldName, bool value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null) return;
            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// private な OnValidate を明示的に走らせる。RealtimeView は OnValidate で
        /// 子の RealtimeTransform / RealtimeComponent を _components に集めるため、
        /// スクリプトで組み立てたプレハブは保存前にこれを呼ばないと登録が空のままになる。
        /// </summary>
        public static void InvokeValidate(Component component)
        {
            if (component == null) return;
            var method = component.GetType().GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (method == null || method.GetParameters().Length != 0) return;
            try { method.Invoke(component, null); }
            catch (Exception e) { Debug.LogWarning($"[Yaoron] OnValidate の呼び出しに失敗: {e.Message}"); }
        }

        public static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
