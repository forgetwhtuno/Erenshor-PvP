using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ErenshorPvP
{
    // Mod-owned uGUI drag handler. Never touches the game's native UI edit-mode flag and never
    // polls the legacy mouse API. The native game already respects EventSystem raycasts and
    // GameData.DraggingUIElement, so no camera/PlayerControl Harmony workaround is needed.
    internal sealed class PvpDragGuard : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private static int _ownedDrags;

        internal RectTransform Target;
        internal Action OnDragCompleted;

        private RectTransform _parent;
        private Vector2 _startPointer;
        private Vector2 _startPosition;
        private bool _dragging;
        private bool _owning;

        private void Awake()
        {
            if (Target == null) Target = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            _parent = Target.parent as RectTransform;
            if (_parent == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out local))
                return;
            _startPointer = local;
            _startPosition = Target.anchoredPosition;
            _dragging = true;
            if (!_owning)
            {
                _owning = true;
                _ownedDrags++;
            }
            GameData.DraggingUIElement = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || Target == null || _parent == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out local))
                return;
            Target.anchoredPosition = _startPosition + (local - _startPointer);
        }

        public void OnEndDrag(PointerEventData eventData) { EndDrag(true); }
        public void OnPointerUp(PointerEventData eventData) { EndDrag(false); }
        private void OnDisable() { EndDrag(true); }
        private void OnDestroy() { EndDrag(true); }

        private void EndDrag(bool notify)
        {
            bool completed = _dragging;
            _dragging = false;
            Release();
            if (notify && completed)
            {
                try { if (OnDragCompleted != null) OnDragCompleted(); }
                catch { }
            }
        }

        private void Release()
        {
            if (!_owning) return;
            _owning = false;
            _ownedDrags--;
            if (_ownedDrags < 0) _ownedDrags = 0;
            if (_ownedDrags == 0)
            {
                try { GameData.DraggingUIElement = false; } catch { }
            }
        }

        internal static void ForceReleaseIfOwned()
        {
            if (_ownedDrags <= 0)
            {
                _ownedDrags = 0;
                return;
            }
            _ownedDrags = 0;
            try { GameData.DraggingUIElement = false; } catch { }
        }
    }
}
