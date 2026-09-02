using Yaoron.Core;
using Yaoron.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yaoron.UI
{
    /// <summary>
    /// モバイル用の仮想スティック (画面左下)。TouchInput へ -1..1 の値を渡すだけ。
    /// 視点ドラッグと取り合わないよう、この UI 上のタッチは EventSystem が吸収する。
    /// </summary>
    public class TouchStickView : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] TouchInput _target;
        [SerializeField] RectTransform _background;
        [SerializeField] RectTransform _handle;
        [SerializeField] float _radiusPixels = 90f;

        Vector2 _value;

        void Awake()
        {
            // 仮想スティックはモバイルだけ。デスクトップ / VR では自分を消す。
            if (!PlatformProfile.IsMobile) { gameObject.SetActive(false); return; }
            if (_target == null) _target = FindFirstObjectByType<TouchInput>(FindObjectsInactive.Include);
            if (_background == null) _background = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (_background == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background, eventData.position, eventData.pressEventCamera, out var local);

            _value = Vector2.ClampMagnitude(local / _radiusPixels, 1f);
            if (_handle != null) _handle.anchoredPosition = _value * _radiusPixels;
            Push();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _value = Vector2.zero;
            if (_handle != null) _handle.anchoredPosition = Vector2.zero;
            Push();
        }

        void Push()
        {
            if (_target != null) _target.StickValue = _value;
        }
    }
}
