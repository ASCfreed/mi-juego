// Forja3D — joystick virtual, botones y zona para girar la cámara. Se crean por código (sin prefabs).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Forja3D
{
    public class Forja3DTouchControls : MonoBehaviour
    {
        public bool mostrarEnPC = false;

        public static void Crear(Transform canvas)
        {
            var go = new GameObject("ControlesTactiles", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            Estirar(go.GetComponent<RectTransform>());
            go.AddComponent<Forja3DTouchControls>();
        }

        void Start()
        {
            if (!mostrarEnPC && !Application.isMobilePlatform) { gameObject.SetActive(false); return; }

            // Zona derecha para girar la cámara (detrás de los botones)
            var look = Panel("ZonaMirar", transform, new Color(0, 0, 0, 0));
            var rl = look.GetComponent<RectTransform>();
            rl.anchorMin = new Vector2(0.45f, 0); rl.anchorMax = new Vector2(1, 1); rl.offsetMin = rl.offsetMax = Vector2.zero;
            look.AddComponent<Forja3DZonaMirar>();

            // Joystick abajo a la izquierda
            var fondo = Panel("Joystick", transform, new Color(1, 1, 1, 0.18f));
            var rf = fondo.GetComponent<RectTransform>();
            rf.anchorMin = rf.anchorMax = new Vector2(0, 0); rf.pivot = new Vector2(0.5f, 0.5f);
            rf.sizeDelta = new Vector2(300, 300); rf.anchoredPosition = new Vector2(260, 260);
            var palanca = Panel("Palanca", fondo.transform, new Color(1, 1, 1, 0.5f));
            var rp = palanca.GetComponent<RectTransform>();
            rp.anchorMin = rp.anchorMax = new Vector2(0.5f, 0.5f); rp.sizeDelta = new Vector2(130, 130);
            palanca.GetComponent<Image>().raycastTarget = false;
            fondo.AddComponent<Forja3DJoystick>().palanca = rp;

            Boton("ATACAR", new Vector2(-200, 230), new Color(0.9f, 0.3f, 0.25f, 0.75f), Forja3DInput.PulsarAtaque);
            Boton("SALTAR", new Vector2(-420, 130), new Color(0.25f, 0.6f, 0.95f, 0.75f), Forja3DInput.PulsarSalto);
        }

        void Boton(string texto, Vector2 pos, Color color, System.Action accion)
        {
            var b = Panel(texto, transform, color);
            var r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(1, 0); r.sizeDelta = new Vector2(190, 190); r.anchoredPosition = pos;
            b.AddComponent<Forja3DBotonTactil>().accion = accion;
            Forja3DGameManager.Texto(b.transform, texto, 34, TextAnchor.MiddleCenter);
        }

        static GameObject Panel(string nombre, Transform padre, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static void Estirar(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    }

    public class Forja3DJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform palanca;
        RectTransform rt;
        void Awake() => rt = GetComponent<RectTransform>();
        public void OnPointerDown(PointerEventData e) => OnDrag(e);
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var p);
            var radio = rt.sizeDelta.x * 0.5f;
            var v = Vector2.ClampMagnitude(p / radio, 1f);
            Forja3DInput.JoystickTactil = v;
            palanca.anchoredPosition = v * radio;
        }
        public void OnPointerUp(PointerEventData e) { Forja3DInput.JoystickTactil = Vector2.zero; palanca.anchoredPosition = Vector2.zero; }
    }

    public class Forja3DBotonTactil : MonoBehaviour, IPointerDownHandler
    {
        public System.Action accion;
        public void OnPointerDown(PointerEventData e) => accion?.Invoke();
    }

    public class Forja3DZonaMirar : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e) => Forja3DInput.MiradaTactil += e.delta * 0.15f;
    }
}
