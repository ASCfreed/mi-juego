// Forja3D — reglas del nivel, HUD, victoria/derrota y paso al siguiente nivel.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Forja3D
{
    public enum Objetivo { Meta, Enemigos, Monedas, Jefe }

    public class Forja3DGameManager : MonoBehaviour
    {
        public string titulo = "Nivel";
        public Objetivo objetivo = Objetivo.Meta;

        public static bool Terminado { get; private set; }
        static Forja3DGameManager inst;
        static int enemigos, derrotados, monedas, recogidas, jefes, jefesDerrotados;
        static System.Collections.Generic.List<Forja3DHealth> vidasJefe = new System.Collections.Generic.List<Forja3DHealth>();

        Forja3DHealth jugador;
        Text hud;
        GameObject panelFinal;
        Text textoFinal, textoBoton;
        float mensajeHasta;

        public static void RegistrarEnemigo() => enemigos++;
        public static void EnemigoDerrotado() { derrotados++; inst?.Revisar(); }
        public static void RegistrarJefe(Forja3DHealth h) { jefes++; vidasJefe.Add(h); }
        public static void JefeDerrotado() { jefesDerrotados++; inst?.Aviso("¡Jefe derrotado!"); inst?.Revisar(); }
        public static void RegistrarMoneda() => monedas++;
        public static void MonedaRecogida() { recogidas++; inst?.Revisar(); }
        public static void MetaAlcanzada()
        {
            if (inst == null || Terminado) return;
            if (inst.objetivo == Objetivo.Meta || inst.Cumplido()) inst.Ganar();
            else inst.Aviso(inst.objetivo == Objetivo.Enemigos ? "¡Derrota a todos los enemigos primero!" : inst.objetivo == Objetivo.Jefe ? "¡Derrota al jefe primero!" : "¡Recoge todas las monedas primero!");
        }

        void Awake()
        {
            // EXPANSIÓN FUTURA: en PC conviene un menú de pausa (Escape), opciones de gráficos y resolución;
            // en iOS/Android, pausar al salir de la app (OnApplicationPause). Hoy el juego no tiene menú.
            inst = this; Terminado = false;
            enemigos = derrotados = monedas = recogidas = jefes = jefesDerrotados = 0; vidasJefe.Clear();
            Application.targetFrameRate = 60; // PC: dejar -1 y usar QualitySettings.vSyncCount = 1
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            CrearUI();
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p) { jugador = p.GetComponent<Forja3DHealth>(); if (jugador) jugador.AlMorir += Perder; }
            Aviso(titulo + ObjetivoTexto());
        }

        void OnDestroy() { if (inst == this) inst = null; }

        string ObjetivoTexto() => objetivo switch
        {
            Objetivo.Enemigos => "\nObjetivo: derrota a todos los enemigos y llega a la meta",
            Objetivo.Monedas => "\nObjetivo: recoge todas las monedas y llega a la meta",
            Objetivo.Jefe => "\nObjetivo: derrota al jefe y llega a la meta",
            _ => "\nObjetivo: llega a la meta verde",
        };

        bool Cumplido() => objetivo switch
        {
            Objetivo.Enemigos => derrotados >= enemigos,
            Objetivo.Monedas => recogidas >= monedas,
            Objetivo.Jefe => jefesDerrotados >= jefes,
            _ => true,
        };

        void Revisar() { if (Cumplido() && objetivo != Objetivo.Meta) Aviso("¡Objetivo cumplido! Ve a la meta"); }

        void Update()
        {
            if (!hud) return;
            var t = $"VIDA {(jugador ? jugador.Vida : 0)}";
            if (enemigos > 0) t += $"    ENEMIGOS {derrotados}/{enemigos}";
            if (monedas > 0) t += $"    MONEDAS {recogidas}/{monedas}";
            foreach (var j in vidasJefe) if (j && !j.Muerto) t += $"\nJEFE {new string('|', Mathf.CeilToInt(20f * j.Vida / Mathf.Max(1, j.vidaMaxima)))}";
            if (Time.time < mensajeHasta) t += "\n\n" + mensaje;
            hud.text = t;
        }

        string mensaje;
        void Aviso(string m) { mensaje = m; mensajeHasta = Time.time + 4f; }

        void Ganar()
        {
            Terminado = true;
            bool hayMas = SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings;
            MostrarFinal(hayMas ? "¡NIVEL COMPLETADO!" : "¡GANASTE EL JUEGO!", hayMas ? "SIGUIENTE" : "JUGAR DE NUEVO",
                () => SceneManager.LoadScene(hayMas ? SceneManager.GetActiveScene().buildIndex + 1 : 0));
        }

        void Perder()
        {
            Terminado = true;
            MostrarFinal("HAS CAÍDO", "REINTENTAR", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        }

        void MostrarFinal(string titulo, string boton, UnityEngine.Events.UnityAction accion)
        {
            panelFinal.SetActive(true);
            textoFinal.text = titulo; textoBoton.text = boton;
            var b = panelFinal.GetComponentInChildren<Button>();
            b.onClick.RemoveAllListeners(); b.onClick.AddListener(accion);
        }

        // ---------- UI por código ----------
        void CrearUI()
        {
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;

            hud = Texto(canvasGo.transform, "", 40, TextAnchor.UpperLeft);
            var rh = hud.rectTransform; rh.offsetMin = new Vector2(40, 40); rh.offsetMax = new Vector2(-40, -40);

            Forja3DTouchControls.Crear(canvasGo.transform);

            panelFinal = new GameObject("PanelFinal", typeof(RectTransform), typeof(Image));
            panelFinal.transform.SetParent(canvasGo.transform, false);
            var rp = panelFinal.GetComponent<RectTransform>(); rp.anchorMin = Vector2.zero; rp.anchorMax = Vector2.one; rp.offsetMin = rp.offsetMax = Vector2.zero;
            panelFinal.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            textoFinal = Texto(panelFinal.transform, "", 90, TextAnchor.MiddleCenter);
            textoFinal.rectTransform.offsetMin = new Vector2(0, 150);

            var bGo = new GameObject("Boton", typeof(RectTransform), typeof(Image), typeof(Button));
            bGo.transform.SetParent(panelFinal.transform, false);
            var rb = bGo.GetComponent<RectTransform>(); rb.anchorMin = rb.anchorMax = new Vector2(0.5f, 0.35f); rb.sizeDelta = new Vector2(520, 140);
            bGo.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.35f);
            textoBoton = Texto(bGo.transform, "", 50, TextAnchor.MiddleCenter);
            panelFinal.SetActive(false);
        }

        public static Text Texto(Transform padre, string contenido, int tam, TextAnchor anc)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = contenido; t.fontSize = tam; t.alignment = anc; t.color = Color.white; t.raycastTarget = false;
            var sombra = go.AddComponent<Shadow>(); sombra.effectDistance = new Vector2(2, -2);
            return t;
        }
    }
}
