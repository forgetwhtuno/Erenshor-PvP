using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ErenshorPvP
{
    // Mod-owned retained-uGUI drag handler. Ownership begins at pointer-down rather than waiting
    // for uGUI's drag threshold because Erenshor camera input is evaluated every frame.
    internal sealed class PvpDragGuard : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private static readonly HashSet<PvpDragGuard> ActiveOwners = new HashSet<PvpDragGuard>();
        private static bool _nativeFlagBeforeFirstOwner;
        private static bool _cameraUsingUiBeforeFirstOwner;
        private static bool _cameraUsingUiWasAvailable;
        private static bool _cameraUsingUiResolved;
        private static FieldInfo _cameraUsingUiField;
        private static PropertyInfo _cameraUsingUiProperty;

        internal RectTransform Target;
        internal Action OnDragCompleted;
        internal Action OnPointerActivated;

        private RectTransform _parent;
        private Vector2 _startPointer;
        private Vector2 _startPosition;
        private readonly PvpPointerOwnershipState _gesture = new PvpPointerOwnershipState();

        private void Awake()
        {
            if (Target == null) Target = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            Acquire();
            try { if (OnPointerActivated != null) OnPointerActivated(); } catch { }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || Target == null) return;
            Acquire();
            _gesture.BeginDrag();
            _parent = Target.parent as RectTransform;
            if (_parent == null) { EndDrag(false); return; }
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out local))
            { EndDrag(false); return; }
            _startPointer = local;
            _startPosition = Target.anchoredPosition;
            ReassertNativeFlag();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_gesture.IsDragging || eventData == null || Target == null || _parent == null) return;
            ReassertNativeFlag();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, eventData.position, eventData.pressEventCamera, out local)) return;
            Target.anchoredPosition = _startPosition + (local - _startPointer);
        }

        private void Update()
        {
            if (!_gesture.OwnsPointer) return;
            ReassertNativeFlag();
            // Pointer-up is normally routed back to pointerPress. If focus/canvas teardown loses
            // that callback, release as soon as the physical left-button press is gone.
            try { if (!Input.GetMouseButton(0)) EndDrag(false); } catch { }
        }

        private void OnApplicationFocus(bool focused) { if (!focused) EndDrag(false); }
        private void OnApplicationPause(bool paused) { if (paused) EndDrag(false); }
        public void OnEndDrag(PointerEventData eventData) { EndDrag(true); }
        public void OnPointerUp(PointerEventData eventData) { EndDrag(false); }
        private void OnDisable() { EndDrag(false); }
        private void OnDestroy() { EndDrag(false); }

        private void Acquire()
        {
            if (!_gesture.PointerDown()) return;
            if (ActiveOwners.Count == 0)
            {
                try { _nativeFlagBeforeFirstOwner = GameData.DraggingUIElement; }
                catch { _nativeFlagBeforeFirstOwner = false; }
                _cameraUsingUiWasAvailable = TryGetCameraUsingUi(out _cameraUsingUiBeforeFirstOwner);
            }
            ActiveOwners.Add(this);
            ReassertNativeFlag();
        }

        private static void ReassertNativeFlag()
        {
            if (ActiveOwners.Count <= 0) return;
            try { if (!GameData.DraggingUIElement) GameData.DraggingUIElement = true; } catch { }
            // Some current game builds expose only GameData.DraggingUIElement, while newer
            // modern-camera builds additionally expose CameraController.UsingUI. Resolve the
            // latter at runtime so one source works against both assemblies. This is monotonic:
            // while any PvP grip owns a left-button gesture we only ever promote it to true.
            bool ignored;
            if (TryGetCameraUsingUi(out ignored)) TrySetCameraUsingUi(true);
        }

        private void EndDrag(bool notify)
        {
            bool completed = _gesture.IsDragging;
            if (_gesture.Release())
            {
                ActiveOwners.Remove(this);
                RestoreNativeFlagIfLastOwnerReleased();
            }
            _parent = null;
            if (notify && completed)
            {
                try { if (OnDragCompleted != null) OnDragCompleted(); }
                catch { }
            }
        }

        private static void RestoreNativeFlagIfLastOwnerReleased()
        {
            if (ActiveOwners.Count != 0) return;
            try { GameData.DraggingUIElement = _nativeFlagBeforeFirstOwner; } catch { }
            if (_cameraUsingUiWasAvailable) TrySetCameraUsingUi(_cameraUsingUiBeforeFirstOwner);
            _nativeFlagBeforeFirstOwner = false;
            _cameraUsingUiBeforeFirstOwner = false;
            _cameraUsingUiWasAvailable = false;
        }

        private static bool TryGetCameraUsingUi(out bool value)
        {
            value = false;
            try
            {
                object camera = GameData.CamControl;
                if (camera == null) return false;
                Type type = camera.GetType();
                if (!_cameraUsingUiResolved)
                {
                    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    _cameraUsingUiField = type.GetField("UsingUI", Flags);
                    _cameraUsingUiProperty = _cameraUsingUiField == null ? type.GetProperty("UsingUI", Flags) : null;
                    _cameraUsingUiResolved = true;
                }
                if (_cameraUsingUiField != null && _cameraUsingUiField.FieldType == typeof(bool))
                { value = (bool)_cameraUsingUiField.GetValue(camera); return true; }
                if (_cameraUsingUiProperty != null && _cameraUsingUiProperty.PropertyType == typeof(bool) && _cameraUsingUiProperty.CanRead)
                { value = (bool)_cameraUsingUiProperty.GetValue(camera, null); return true; }
            }
            catch { }
            return false;
        }

        private static void TrySetCameraUsingUi(bool value)
        {
            try
            {
                object camera = GameData.CamControl;
                if (camera == null) return;
                if (_cameraUsingUiField != null && _cameraUsingUiField.FieldType == typeof(bool)) _cameraUsingUiField.SetValue(camera, value);
                else if (_cameraUsingUiProperty != null && _cameraUsingUiProperty.PropertyType == typeof(bool) && _cameraUsingUiProperty.CanWrite)
                    _cameraUsingUiProperty.SetValue(camera, value, null);
            }
            catch { }
        }

        internal static void ForceReleaseIfOwned()
        {
            if (ActiveOwners.Count == 0) return;
            PvpDragGuard[] owners = new PvpDragGuard[ActiveOwners.Count];
            ActiveOwners.CopyTo(owners);
            for (int i = 0; i < owners.Length; i++)
            {
                PvpDragGuard owner = owners[i];
                if (owner == null) continue;
                owner._gesture.Release();
                owner._parent = null;
            }
            ActiveOwners.Clear();
            RestoreNativeFlagIfLastOwnerReleased();
        }
    }
}
