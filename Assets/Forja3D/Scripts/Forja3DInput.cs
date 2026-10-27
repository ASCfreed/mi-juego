// Forja3D — entrada unificada: teclado/ratón en PC y controles táctiles en Android.
using UnityEngine;

namespace Forja3D
{
    public static class Forja3DInput
    {
        // Valores que escriben los controles táctiles
        public static Vector2 JoystickTactil;
        public static Vector2 MiradaTactil;
        static int saltoFrame = -1, ataqueFrame = -1;

        public static void PulsarSalto() => saltoFrame = Time.frameCount;
        public static void PulsarAtaque() => ataqueFrame = Time.frameCount;

        public static Vector2 Mover
        {
            get
            {
                var k = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                var v = k.sqrMagnitude > JoystickTactil.sqrMagnitude ? k : JoystickTactil;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool Correr =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || JoystickTactil.magnitude > 0.85f;

        // Se consumen en el frame siguiente al toque (los botones UI se procesan antes que Update)
        public static bool Saltar => Input.GetKeyDown(KeyCode.Space) || Time.frameCount - saltoFrame <= 1 && Consumir(ref saltoFrame);
        public static bool Atacar => Input.GetMouseButtonDown(0) && !SobreUI() || Time.frameCount - ataqueFrame <= 1 && Consumir(ref ataqueFrame);

        public static Vector2 Mirar
        {
            get
            {
                var m = MiradaTactil; MiradaTactil = Vector2.zero;
                if (Input.GetMouseButton(1)) m += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f;
                return m;
            }
        }

        static bool Consumir(ref int frame) { frame = -10; return true; }

        static bool SobreUI() =>
            UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
