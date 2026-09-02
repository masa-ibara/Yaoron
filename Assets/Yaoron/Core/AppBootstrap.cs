using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yaoron.Core
{
    /// <summary>
    /// Boot シーンの唯一の常駐オブジェクト。プラットフォーム判定 → 品質・フレームレート適用 →
    /// ワールドシーンのロード、までを担当する。ネットワークには触らない。
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] string _worldScene = "World_Plaza";
        [SerializeField] bool _loadWorldOnStart = true;

        [Header("品質設定 (プロジェクトの Quality レベル名)")]
        [Tooltip("見つからない場合は URP テンプレートの既定名 (High Fidelity / Balanced / Performant) を順に探す")]
        [SerializeField] string _qualityDesktop = "Desktop";
        [SerializeField] string _qualityMobile = "Mobile";
        [SerializeField] string _qualityWeb = "Web";
        [SerializeField] string _qualityVR = "VR";

        public static AppBootstrap Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            PlatformProfile.EnsureInitialized();
            ApplyQuality();
            ApplyFrameRate();
            YaLog.Info($"起動: {Application.platform} / rig={PlatformProfile.Rig}");
        }

        IEnumerator Start()
        {
            if (!_loadWorldOnStart) yield break;
            if (SceneManager.GetActiveScene().name == _worldScene) yield break;
            var op = SceneManager.LoadSceneAsync(_worldScene, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
        }

        void ApplyFrameRate()
        {
            // VR は XR 側のリフレッシュレートに従わせる。それ以外は 60 fps 固定 (設計書 §12)。
            var target = PlatformProfile.TargetFrameRate;
            if (target > 0)
            {
                Application.targetFrameRate = target;
                QualitySettings.vSyncCount = PlatformProfile.IsWeb ? 1 : 0;
            }
            else
            {
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
            }
        }

        /// <summary>
        /// 設定した名前が無ければ、URP テンプレートの既定名へ寄せる。
        /// プロジェクトごとに Quality レベルの命名が違っても破綻させないため。
        /// </summary>
        void ApplyQuality()
        {
            string[] candidates =
                PlatformProfile.IsVR ? new[] { _qualityVR, "Performant", "Balanced" } :
                PlatformProfile.IsMobile ? new[] { _qualityMobile, "Performant", "Balanced" } :
                PlatformProfile.IsWeb ? new[] { _qualityWeb, "Balanced", "Performant" } :
                                        new[] { _qualityDesktop, "High Fidelity", "Balanced" };

            var names = QualitySettings.names;
            foreach (var wanted in candidates)
            {
                if (string.IsNullOrEmpty(wanted)) continue;
                for (int i = 0; i < names.Length; i++)
                {
                    if (!string.Equals(names[i], wanted, System.StringComparison.OrdinalIgnoreCase)) continue;
                    QualitySettings.SetQualityLevel(i, true);
                    YaLog.Info($"品質レベル: {names[i]}");
                    return;
                }
            }
            YaLog.Info($"該当する品質レベルが無いので既定のまま ({names[QualitySettings.GetQualityLevel()]})");
        }
    }
}
