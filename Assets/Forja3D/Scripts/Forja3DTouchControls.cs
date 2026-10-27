// Forja3D — controles táctiles leyendo los dedos directamente (sin EventSystem).
// Cada dedo recibe un papel al tocar la pantalla y lo conserva hasta soltarlo:
//   · mitad izquierda  → joystick flotante (aparece donde pones el dedo)
//   · botones          → SALTAR / ATACAR
//   · resto de la mitad derecha → girar la cámara
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ── EXPANSIÓN FUTURA ───────────────────────────────────────────────────────────
// iOS: en iPhone con notch/isla dinámica, colocar los botones dentro de Screen.safeArea
//   (convertir safeArea a coordenadas del canvas y desplazar 'raiz').
// PC: este componente se desactiva solo fuera de móvil (mostrarEnPC = false).
// Ajustes de jugabilidad: sensibilidadCamara, radioJoystick y las posiciones de Boton() en Start().
// ──────────────────────────────────────────────────────────────────────────────
namespace Forja3D
{
    public class Forja3DTouchControls : MonoBehaviour
    {
        public bool mostrarEnPC = false;
        [Tooltip("Grados de giro por cada pantalla completa arrastrada")]
        public float sensibilidadCamara = 220f;
        public float radioJoystick = 150f; // en unidades del canvas

        enum Papel { Joystick, Camara, Boton }
        readonly Dictionary<int, Papel> papeles = new Dictionary<int, Papel>();
        int dedoJoystick = -1;
        Vector2 origenJoystick;
        RectTransform raiz, fondo, palanca;
        readonly List<(RectTransform rect, System.Action accion, Image img, Color color)> botones = new List<(RectTransform, System.Action, Image, Color)>();
        Vector2 posReposo;
        Canvas canvas;

        public static void Crear(Transform canvas)
        {
            var go = new GameObject("ControlesTactiles", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            go.AddComponent<Forja3DTouchControls>();
        }

        void Start()
        {
            if (!mostrarEnPC && !Application.isMobilePlatform) { gameObject.SetActive(false); return; }
            Input.simulateMouseWithTouches = false;
            Input.multiTouchEnabled = true;
            raiz = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            fondo = Panel("Joystick", transform, new Color(1, 1, 1, 0.15f), new Vector2(radioJoystick * 2, radioJoystick * 2));
            fondo.anchorMin = fondo.anchorMax = Vector2.zero;
            posReposo = new Vector2(260, 260);
            fondo.anchoredPosition = posReposo;
            palanca = Panel("Palanca", fondo, new Color(1, 1, 1, 0.45f), new Vector2(120, 120));
            palanca.anchorMin = palanca.anchorMax = new Vector2(0.5f, 0.5f);

            Boton("ATACAR", new Vector2(-200, 230), 200, new Color(0.9f, 0.3f, 0.25f, 0.7f), Forja3DInput.PulsarAtaque);
            Boton("SALTAR", new Vector2(-430, 130), 170, new Color(0.25f, 0.6f, 0.95f, 0.7f), Forja3DInput.PulsarSalto);
        }

        void Boton(string texto, Vector2 pos, float tam, Color color, System.Action accion)
        {
            var r = Panel(texto, transform, color, new Vector2(tam, tam));
            r.anchorMin = r.anchorMax = new Vector2(1, 0); r.anchoredPosition = pos;
            Forja3DGameManager.Texto(r, texto, 32, TextAnchor.MiddleCenter);
            botones.Add((r, accion, r.GetComponent<Image>(), color));
        }

        static RectTransform Panel(string nombre, Transform padre, Color color, Vector2 tam)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            var r = go.GetComponent<RectTransform>(); r.sizeDelta = tam;
            return r;
        }

        Vector2 ACanvas(Vector2 pantalla)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(raiz, pantalla, null, out var p);
            return p - raiz.rect.min; // origen abajo a la izquierda
        }

        void Update()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                switch (t.phase)
                {
                    case TouchPhase.Began: Empezar(t); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: Mover(t); break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled: Terminar(t); break;
                }
            }
            // Seguridad: si se perdió algún dedo, suelta todo
            if (Input.touchCount == 0 && (papeles.Count > 0 || dedoJoystick >= 0)) { papeles.Clear(); SoltarJoystick(); }
        }

        void Empezar(Touch t)
        {
            foreach (var b in botones)
                if (RectTransformUtility.RectangleContainsScreenPoint(b.rect, t.position, null))
                {
                    papeles[t.fingerId] = Papel.Boton;
                    b.accion();
                    b.img.color = Color.Lerp(b.color, Color.white, 0.5f);
                    return;
                }
            if (t.position.x < Screen.width * 0.45f && dedoJoystick < 0)
            {
                papeles[t.fingerId] = Papel.Joystick;
                dedoJoystick = t.fingerId;
                origenJoystick = ACanvas(t.position);
                fondo.anchoredPosition = origenJoystick;
                fondo.GetComponent<Image>().color = new Color(1, 1, 1, 0.25f);
                Mover(t);
                return;
            }
            papeles[t.fingerId] = Papel.Camara;
        }

        void Mover(Touch t)
        {
            if (!papeles.TryGetValue(t.fingerId, out var papel)) return;
            if (papel == Papel.Joystick)
            {
                var d = ACanvas(t.position) - origenJoystick;
                var v = Vector2.ClampMagnitude(d / radioJoystick, 1f);
                if (v.magnitude < 0.12f) v = Vector2.zero; // zona muerta
                Forja3DInput.JoystickTactil = v;
                palanca.anchoredPosition = v * radioJoystick;
            }
            else if (papel == Papel.Camara && t.phase == TouchPhase.Moved)
            {
                // Independiente de la resolución: arrastrar toda la altura de la pantalla = sensibilidadCamara grados
                Forja3DInput.MiradaTactil += t.deltaPosition / Mathf.Max(1, Screen.height) * sensibilidadCamara;
            }
        }

        void Terminar(Touch t)
        {
            if (papeles.TryGetValue(t.fingerId, out var papel) && papel == Papel.Boton)
                foreach (var b in botones) b.img.color = b.color;
            papeles.Remove(t.fingerId);
            if (t.fingerId == dedoJoystick) SoltarJoystick();
        }

        void SoltarJoystick()
        {
            dedoJoystick = -1;
            Forja3DInput.JoystickTactil = Vector2.zero;
            if (palanca) palanca.anchoredPosition = Vector2.zero;
            if (fondo) { fondo.anchoredPosition = posReposo; fondo.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f); }
        }

        void OnDisable() { Forja3DInput.JoystickTactil = Vector2.zero; }
    }
}
