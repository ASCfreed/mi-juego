// Forja3D — pantalla de título: COMENZAR (desde el nivel 1), CONTINUAR (último nivel alcanzado)
// y SELECCIONAR NIVEL. Cada nivel trae su propio equipo en el JSON (hoz desde el Aqueronte, cuerda desde
// los glotones), así que entrar directo a uno da a Dante lo que ya habría ganado.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Forja3D
{
    public class Forja3DTitle : MonoBehaviour
    {
        public string titulo = "JUEGO";
        public string subtitulo = "";
        public string cita = "";
        public string citaTraduccion = "";
        public Color colorFondo = new Color(0.05f, 0.02f, 0.02f);
        public Color colorTitulo = new Color(0.92f, 0.75f, 0.42f);
        public string[] niveles = new string[0]; // títulos, en el orden de las escenas (escena i+1)
        // Para la versión de tienda: true = solo los niveles ya alcanzados. Hoy todos, para probar.
        public bool soloAlcanzados = false;
        Transform raiz; GameObject panelNiveles;

        public const string ClaveProgreso = "forja3d_nivel";

        void Start()
        {
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = colorFondo; }
            if (!FindObjectOfType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Titulo", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;
            raiz = canvasGo.transform;

            var t = Forja3DGameManager.Texto(raiz, titulo, 130, TextAnchor.MiddleCenter);
            t.color = colorTitulo; t.fontStyle = FontStyle.Bold;
            t.rectTransform.anchorMin = new Vector2(0, 0.6f); t.rectTransform.anchorMax = new Vector2(1, 0.85f);
            var s = Forja3DGameManager.Texto(raiz, subtitulo, 50, TextAnchor.MiddleCenter);
            s.fontStyle = FontStyle.Italic; s.color = new Color(1, 1, 1, 0.8f);
            s.rectTransform.anchorMin = new Vector2(0, 0.52f); s.rectTransform.anchorMax = new Vector2(1, 0.6f);
            if (!string.IsNullOrEmpty(cita))
            {
                var c = Forja3DGameManager.Texto(raiz, $"«{cita}»\n<size=28>{citaTraduccion}</size>", 34, TextAnchor.MiddleCenter);
                c.supportRichText = true; c.fontStyle = FontStyle.Italic; c.color = new Color(1, 1, 1, 0.55f);
                c.rectTransform.anchorMin = new Vector2(0.1f, 0.01f); c.rectTransform.anchorMax = new Vector2(0.9f, 0.11f);
            }

            int guardado = PlayerPrefs.GetInt(ClaveProgreso, 1);
            int total = SceneManager.sceneCountInBuildSettings;
            bool continuar = guardado > 1 && guardado < total;
            float y = 0.43f;
            Boton(raiz, "COMENZAR", new Vector2(0.5f, y), () => { PlayerPrefs.SetInt(ClaveProgreso, 1); SceneManager.LoadScene(1); });
            if (continuar) { y -= 0.11f; Boton(raiz, "CONTINUAR", new Vector2(0.5f, y), () => SceneManager.LoadScene(guardado)); }
            if (total > 2) { y -= 0.11f; Boton(raiz, "SELECCIONAR NIVEL", new Vector2(0.5f, y), AbrirNiveles); }
        }

        void AbrirNiveles()
        {
            if (panelNiveles) { panelNiveles.SetActive(true); return; }
            int total = SceneManager.sceneCountInBuildSettings - 1; // la escena 0 es este título
            int alcanzado = Mathf.Max(1, PlayerPrefs.GetInt(ClaveProgreso, 1));
            panelNiveles = new GameObject("Niveles", typeof(RectTransform), typeof(Image));
            panelNiveles.transform.SetParent(raiz, false);
            var pr = panelNiveles.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
            panelNiveles.GetComponent<Image>().color = new Color(0.03f, 0.01f, 0.01f, 0.96f);
            var t = Forja3DGameManager.Texto(panelNiveles.transform, "SELECCIONAR NIVEL", 70, TextAnchor.MiddleCenter);
            t.color = colorTitulo; t.fontStyle = FontStyle.Bold;
            t.rectTransform.anchorMin = new Vector2(0, 0.84f); t.rectTransform.anchorMax = new Vector2(1, 0.96f);
            // Dos columnas; hasta 12 niveles por pantalla (EXPANSIÓN FUTURA: páginas por cántica)
            int filas = Mathf.Max(1, Mathf.CeilToInt(total / 2f));
            float alto = Mathf.Min(0.12f, 0.66f / filas);
            for (int i = 0; i < total; i++)
            {
                int escena = i + 1;
                string nombre = i < niveles.Length && !string.IsNullOrEmpty(niveles[i]) ? niveles[i] : $"Nivel {escena}";
                bool abierto = !soloAlcanzados || escena <= alcanzado;
                var a = new Vector2(i % 2 == 0 ? 0.27f : 0.73f, 0.8f - alto * (i / 2 + 0.5f));
                var b = Boton(panelNiveles.transform, nombre, a, () => { PlayerPrefs.SetInt(ClaveProgreso, escena); SceneManager.LoadScene(escena); }, new Vector2(860, alto * 1080f * 0.82f), 34);
                b.interactable = abierto;
            }
            Boton(panelNiveles.transform, "VOLVER", new Vector2(0.5f, 0.07f), () => panelNiveles.SetActive(false), new Vector2(360, 100), 40);
        }

        static Button Boton(Transform padre, string texto, Vector2 ancla, UnityEngine.Events.UnityAction accion, Vector2? tam = null, int letra = 46)
        {
            var go = new GameObject(texto, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = ancla; r.sizeDelta = tam ?? new Vector2(620, 105);
            go.GetComponent<Image>().color = new Color(0.45f, 0.08f, 0.06f, 0.9f);
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(0.92f, 0.75f, 0.42f, 0.7f); o.effectDistance = new Vector2(2, -2);
            go.GetComponent<Button>().onClick.AddListener(accion);
            Forja3DGameManager.Texto(go.transform, texto, letra, TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }
    }
}
