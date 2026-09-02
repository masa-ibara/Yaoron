using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// VRM の読み込みが終わるまでの仮表示 (カプセル + 名札)。
    /// 読み込みに失敗したときもこれが残るので、無言で消えるより事故が分かりやすい (設計書 §8)。
    /// </summary>
    public class AvatarPlaceholder : MonoBehaviour
    {
        [SerializeField] GameObject _visual;

        public void SetVisible(bool visible)
        {
            if (_visual == null) _visual = gameObject;
            if (_visual.activeSelf != visible) _visual.SetActive(visible);
        }
    }
}
