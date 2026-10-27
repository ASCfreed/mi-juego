// Forja3D — entrada unificada: teclado/ratón en PC y controles táctiles en Android.
// En móvil el ratón simulado se ignora por completo: cada dedo tiene un solo papel
// (joystick, cámara o botón), así el joystick nunca mueve la cámara.
using UnityEngine;

namespace Forja3D
{
    public static class Forja3DInput
    {
        public static Vector2 JoystickTactil;   // lo escribe Forja3DTouchControls
        public static Vector2 MiradaTactil;     // grados acumulados este frame
        static int saltoFrame = -10, ataqueFrame = -10;

        public static bool EsTactil => Application.isMobilePlatform || Input.touchCount > 0;

        public static void PulsarSalto() => saltoFrame = Time.frameCount;
        public static void PulsarAtaque() => ataqueFrame = Time.frameCount;

        public static Vector2 Mover
        {
            get
            {
                var v = JoystickTactil;
                if (!EsTactil)
                {
                    var k = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    if (k.sqrMagnitude > v.sqrMagnitude) v = k;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool Correr =>
            JoystickTactil.magnitude > 0.9f || (!EsTactil && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));

        public static bool Saltar => Time.frameCount - saltoFrame <= 1 && Consumir(ref saltoFrame) || (!EsTactil && Input.GetKeyDown(KeyCode.Space));
        public static bool Atacar => Time.frameCount - ataqueFrame <= 1 && Consumir(ref ataqueFrame) || (!EsTactil && Input.GetMouseButtonDown(0));

        public static Vector2 Mirar
        {
            get
            {
                var m = MiradaTactil; MiradaTactil = Vector2.zero;
                if (!EsTactil && Input.GetMouseButton(1)) m += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f;
                return m;
            }
        }

        static bool Consumir(ref int frame) { frame = -10; return true; }
    }
}
