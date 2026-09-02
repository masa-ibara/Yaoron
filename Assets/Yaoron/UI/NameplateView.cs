using Yaoron.Avatar;
using Yaoron.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Yaoron.UI
{
    /// <summary>
    /// アバターの頭上の名札。ワールド空間 Canvas をカメラへ向けるだけの薄い実装。
    /// モバイルは表示数が効くので、距離に応じて縮小・非表示にする (設計書 §10)。
    /// </summary>
    public class NameplateView : MonoBehaviour
    {
        [SerializeField] YaAvatar _avatar;
        [SerializeField] Text _label;
        [SerializeField] Canvas _canvas;
        [SerializeField] float _hideDistance = 25f;
        [SerializeField] float _minScale = 0.5f;

        Transform _viewer;

        void Awake()
        {
            if (_avatar == null) _avatar = GetComponentInParent<YaAvatar>();
            if (_canvas == null) _canvas = GetComponentInChildren<Canvas>();
            if (_avatar != null)
            {
                _avatar.State.DisplayNameChanged += SetName;
                SetName(_avatar.State.DisplayName);
            }
        }

        void OnDestroy()
        {
            if (_avatar != null) _avatar.State.DisplayNameChanged -= SetName;
        }

        void SetName(string value)
        {
            if (_label == null) return;
            _label.text = string.IsNullOrWhiteSpace(value) ? "ゲスト" : value;
        }

        void LateUpdate()
        {
            if (_canvas == null) return;
            if (_viewer == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                _viewer = cam.transform;
            }

            float distance = Vector3.Distance(_viewer.position, transform.position);
            bool visible = distance < _hideDistance;
            if (_canvas.enabled != visible) _canvas.enabled = visible;
            if (!visible) return;

            transform.rotation = Quaternion.LookRotation(transform.position - _viewer.position);

            // 遠い相手の名札は縮小して、画面が名前で埋まらないようにする (設計書 §10)。
            float scale = Mathf.Lerp(_minScale, 1f, Mathf.InverseLerp(_hideDistance, 2f, distance));
            transform.localScale = Vector3.one * scale;
        }
    }
}
