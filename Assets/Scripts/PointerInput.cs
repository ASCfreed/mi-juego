using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ParticulasRA
{
    /// <summary>
    /// Lectura unificada de toque (EnhancedTouch) y ratón (para probar en el Editor).
    /// Requiere EnhancedTouchSupport.Enable(), que hacen ARPlacementController y LaunchInput.
    /// </summary>
    public static class PointerInput
    {
        public struct State
        {
            public bool began, held, ended, canceled;
            public Vector2 pos;
            public int touchCount;
        }

        public static State Read()
        {
            var s = new State();
            var touches = Touch.activeTouches;
            s.touchCount = touches.Count;
            if (touches.Count > 0)
            {
                var t = touches[0];
                s.pos = t.screenPosition;
                var ph = t.phase;
                s.began = ph == TouchPhase.Began;
                s.ended = ph == TouchPhase.Ended;
                s.canceled = ph == TouchPhase.Canceled;
                s.held = s.began || ph == TouchPhase.Moved || ph == TouchPhase.Stationary;
                return s;
            }

            var m = Mouse.current;
            if (m != null)
            {
                s.pos = m.position.ReadValue();
                s.began = m.leftButton.wasPressedThisFrame;
                s.ended = m.leftButton.wasReleasedThisFrame;
                s.held = m.leftButton.isPressed;
                s.touchCount = s.held || s.ended ? 1 : 0;
            }
            return s;
        }
    }
}
