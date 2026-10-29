using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ParticulasRA
{
    /// <summary>Interfaz completa en español, construida por código (sección 8).</summary>
    [DefaultExecutionOrder(-40)]
    public class UIController : MonoBehaviour
    {
        [SerializeField] SimulationEngine engine;
        [SerializeField] ExperimentPresets presets;
        [SerializeField] TrailManager trails;

        public PType Selected { get; private set; } = PType.Em;
        /// <summary>Contenedor ajustado al área segura; lo usa TutorialController.</summary>
        public RectTransform Root { get; private set; }

        public event Action OnRelocate;
        public event Action OnRequestPermission;

        Canvas canvas;
        RectTransform safe, panel, aimRt;
        Rect lastSafe;
        Text statsText, statusText, fichaText, aimText, panelToggleText, settingsToggleText;
        Text oB, oV, oT, oD, coulombText, bounceText, pauseText;
        GameObject statusBox, aimBox, permissionBtn, relocateBtn, settingsBox;
        Slider sB, sV, sT, sD;
        bool panelOpen = true;
        float statusHideAt = -1f;
        string lastStatus;

        readonly Image[] chipBg = new Image[ParticleDefs.Count];
        readonly Outline[] chipOutline = new Outline[ParticleDefs.Count];
        readonly Image[] axisBtn = new Image[3];

        struct LogEntry { public GameObject go; public CanvasGroup cg; public Text text; public float t; }
        readonly LogEntry[] logs = new LogEntry[5];
        int logNext;

        PointerEventData ped;
        readonly List<RaycastResult> raycastHits = new List<RaycastResult>(16);
        float statsTimer;
        int lastN = -1, lastM = -1, lastK = -1;

        // ------------------------------------------------------------------
        void Awake()
        {
            if (engine == null || presets == null || trails == null)
            {
                Debug.LogError("[UIController] Faltan referencias.");
                enabled = false;
                return;
            }
            EnsureEventSystem();
            BuildCanvas();
            BuildTopBar();
            BuildStatus();
            BuildLog();
            BuildPanel();
            BuildAimLabel();
            Select(PType.Em);
            RefreshSettings();
        }

        void OnEnable()
        {
            if (engine == null) return;
            engine.OnLog += AddLog;
            engine.OnSettingsChanged += RefreshSettings;
        }

        void OnDisable()
        {
            if (engine == null) return;
            engine.OnLog -= AddLog;
            engine.OnSettingsChanged -= RefreshSettings;
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        // ---------------- Construcción ----------------
        void BuildCanvas()
        {
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var sc = cgo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 1920);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0f;
            cgo.AddComponent<GraphicRaycaster>();

            safe = UIFactory.Rect("AreaSegura", cgo.transform);
            ApplySafeArea();
            Root = safe;
        }

        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            if (sa == lastSafe || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafe = sa;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        void BuildTopBar()
        {
            var bar = UIFactory.Rect("BarraSuperior", safe);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 120);
            bar.anchoredPosition = Vector2.zero;
            UIFactory.Img(bar, new Color(0.05f, 0.07f, 0.12f, 0.88f));

            var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(28, 20, 14, 14);
            h.spacing = 16;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var title = UIFactory.Label(bar, Application.productName, 40, UIFactory.Txt);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Layout(title.gameObject, prefW: 330, flexW: 0);

            statsText = UIFactory.Label(bar, "", 24, UIFactory.Mut, TextAnchor.MiddleRight);
            UIFactory.Layout(statsText.gameObject, flexW: 1);

            var rb = UIFactory.Btn(bar, "Reubicar", () => OnRelocate?.Invoke(), out _, null, 30, 92);
            UIFactory.Layout(rb.gameObject, prefW: 210, flexW: 0);
            relocateBtn = rb.gameObject;
            relocateBtn.SetActive(false);
        }

        void BuildStatus()
        {
            var box = UIFactory.Rect("Estado", safe);
            box.anchorMin = new Vector2(0.04f, 1);
            box.anchorMax = new Vector2(0.96f, 1);
            box.pivot = new Vector2(0.5f, 1);
            box.sizeDelta = new Vector2(0, 84);
            box.anchoredPosition = new Vector2(0, -132);
            UIFactory.Img(box, new Color(0.05f, 0.07f, 0.12f, 0.8f), false);
            statusText = UIFactory.Label(box, "", 28, UIFactory.Txt, TextAnchor.MiddleCenter);
            UIFactory.Stretch(statusText.rectTransform, 16, 16, 0, 0);
            statusBox = box.gameObject;
            statusBox.SetActive(false);

            var pb = UIFactory.Btn(safe, "Conceder permiso de cámara", () => OnRequestPermission?.Invoke(), out _, UIFactory.Acc, 30, 96);
            var prt = (RectTransform)pb.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1);
            prt.pivot = new Vector2(0.5f, 1);
            prt.sizeDelta = new Vector2(640, 96);
            prt.anchoredPosition = new Vector2(0, -230);
            pb.GetComponentInChildren<Text>().color = UIFactory.PanelCol;
            permissionBtn = pb.gameObject;
            permissionBtn.SetActive(false);
        }

        void BuildLog()
        {
            var cont = UIFactory.Rect("Registro", safe);
            cont.anchorMin = cont.anchorMax = new Vector2(0, 1);
            cont.pivot = new Vector2(0, 1);
            cont.sizeDelta = new Vector2(1000, 460);
            cont.anchoredPosition = new Vector2(20, -232);
            var v = cont.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;

            for (int i = 0; i < logs.Length; i++)
            {
                var e = UIFactory.Rect("Evento", cont);
                UIFactory.Img(e, new Color(0.05f, 0.07f, 0.12f, 0.82f), false);
                var h = e.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(18, 18, 8, 8);
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                var cg = e.gameObject.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
                var t = UIFactory.Label(e, "", 26, UIFactory.Txt);
                e.gameObject.SetActive(false);
                logs[i] = new LogEntry { go = e.gameObject, cg = cg, text = t, t = -100f };
            }
        }

        void BuildPanel()
        {
            panel = UIFactory.Rect("Panel", safe);
            panel.pivot = new Vector2(0.5f, 0);
            UIFactory.Img(panel, UIFactory.PanelCol);
            var line = UIFactory.Rect("Borde", panel);
            line.anchorMin = new Vector2(0, 1);
            line.anchorMax = new Vector2(1, 1);
            line.pivot = new Vector2(0.5f, 1);
            line.sizeDelta = new Vector2(0, 2);
            UIFactory.Img(line, UIFactory.LineCol, false);

            // Cabecera desplegable
            var header = UIFactory.Rect("Cabecera", panel);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 88);
            header.anchoredPosition = Vector2.zero;
            var hb = UIFactory.Btn(header, "Ocultar panel", TogglePanel, out panelToggleText, new Color(0, 0, 0, 0), 28, 88);
            UIFactory.Stretch((RectTransform)hb.transform);
            panelToggleText.color = UIFactory.Mut;

            // Zona desplazable
            var scroll = UIFactory.Rect("Desplazable", panel);
            UIFactory.Stretch(scroll, 0, 0, 0, 88);
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            var vp = UIFactory.Rect("Viewport", scroll);
            UIFactory.Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            UIFactory.Img(vp, new Color(0, 0, 0, 0));
            var content = UIFactory.Rect("Contenido", vp);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(30, 30, 4, 48);
            vl.spacing = 14;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp;
            sr.content = content;

            // Partículas
            Section(content, "PARTÍCULA");
            var grid = UIFactory.Grid(content, new Vector2(186, 84), 5, 12);
            for (int k = 0; k < ParticleDefs.Count; k++)
            {
                var info = ParticleDefs.All[k];
                var rt = UIFactory.Rect("Chip_" + info.Id, grid);
                var bg = UIFactory.Img(rt, UIFactory.ChipCol);
                chipOutline[k] = UIFactory.Border(bg, UIFactory.LineCol);
                chipBg[k] = bg;
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                PType captured = info.Type;
                btn.onClick.AddListener(() => Select(captured));

                var dot = UIFactory.Rect("Punto", rt);
                dot.anchorMin = dot.anchorMax = new Vector2(0, 0.5f);
                dot.sizeDelta = new Vector2(26, 26);
                dot.anchoredPosition = new Vector2(38, 0);
                UIFactory.Img(dot, info.Color, false, ProceduralAssets.CircleSprite);

                var lbl = UIFactory.Label(rt, info.Symbol, 36, UIFactory.Txt, TextAnchor.MiddleCenter);
                UIFactory.Stretch(lbl.rectTransform, 56, 8, 0, 0);
            }

            fichaText = UIFactory.Label(content, "", 28, UIFactory.Mut, TextAnchor.UpperLeft);

            // Experimentos
            Section(content, "EXPERIMENTOS");
            var eg = UIFactory.Grid(content, new Vector2(504, 96), 2, 12);
            UIFactory.Btn(eg, "Rayos cósmicos", presets.Cosmic, out _);
            UIFactory.Btn(eg, "p̄ + p", presets.ProtonAntiproton, out _);
            UIFactory.Btn(eg, "γ → e⁻e⁺", presets.PairProduction, out _);
            UIFactory.Btn(eg, "e⁻ + e⁺", presets.ElectronPositron, out _);

            var cg = UIFactory.Grid(content, new Vector2(504, 96), 2, 12);
            UIFactory.Btn(cg, "Pausa", TogglePause, out pauseText, UIFactory.ChipCol);
            UIFactory.Btn(cg, "Limpiar", () => engine.Clear(), out _, UIFactory.ChipCol);

            // Ajustes
            UIFactory.Btn(content, "Ajustes de la cámara  (+)", ToggleSettings, out settingsToggleText, new Color(0, 0, 0, 0), 30, 80);
            settingsToggleText.color = UIFactory.Mut;
            settingsToggleText.alignment = TextAnchor.MiddleLeft;

            var set = UIFactory.VCol(content, 12);
            settingsBox = set.gameObject;

            sB = SliderRow(set, "Campo magnético B", -120, 120, 36, v => engine.SetB(v / 10f), out oB);

            var axisRow = UIFactory.HRow(set, 12, 84, false);
            var al = UIFactory.Label(axisRow, "Dirección de B", 26, UIFactory.Mut);
            UIFactory.Layout(al.gameObject, prefW: 300, flexW: 0);
            string[] axisNames = { "X", "Y", "Z" };
            for (int a = 0; a < 3; a++)
            {
                int captured = a;
                var b = UIFactory.Btn(axisRow, axisNames[a], () => engine.SetAxis(captured), out _, UIFactory.ChipCol, 30, 80);
                UIFactory.Layout(b.gameObject, prefW: 150, flexW: 0);
                axisBtn[a] = b.GetComponent<Image>();
            }

            sV = SliderRow(set, "Velocidad", 1, 12, 4, v => { engine.Speed = v / 4f; RefreshSettings(); }, out oV);
            sT = SliderRow(set, "Longitud de trazas", 0, 40, 18, v => { trails.SetMaxPoints(Mathf.RoundToInt(v) * 10); RefreshSettings(); }, out oT);
            sD = SliderRow(set, "Densidad del medio", 0, 20, 4, v => { engine.Density = v / 20f; RefreshSettings(); }, out oD);

            UIFactory.Btn(set, "", () => { engine.Coulomb = !engine.Coulomb; RefreshSettings(); }, out coulombText, UIFactory.ChipCol, 28, 84);
            UIFactory.Btn(set, "", () => { engine.Bounce = !engine.Bounce; RefreshSettings(); }, out bounceText, UIFactory.ChipCol, 28, 84);

            var note = UIFactory.Label(set,
                "Masas, tiempos de vida y velocidades están reescalados para que se vean; las proporciones (curvatura, cargas, " +
                "productos de cada reacción) siguen la física real. Los neutrinos (ν) no dejan traza.",
                22, UIFactory.NoteCol, TextAnchor.UpperLeft);
            UIFactory.Layout(note.gameObject, prefH: -1);
            settingsBox.SetActive(false);

            SetPanelOpen(true);
        }

        static void Section(Transform parent, string title)
        {
            var t = UIFactory.Label(parent, title, 22, UIFactory.Mut);
            UIFactory.Layout(t.gameObject, prefH: 40);
        }

        Slider SliderRow(Transform parent, string label, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChange, out Text valueText)
        {
            var row = UIFactory.HRow(parent, 14, 72, false);
            var l = UIFactory.Label(row, label, 26, UIFactory.Mut);
            UIFactory.Layout(l.gameObject, prefW: 300, flexW: 0);
            var s = UIFactory.Slider(row, min, max, value, onChange);
            valueText = UIFactory.Label(row, "", 26, UIFactory.Txt, TextAnchor.MiddleRight);
            UIFactory.Layout(valueText.gameObject, prefW: 130, flexW: 0);
            return s;
        }

        void BuildAimLabel()
        {
            aimRt = UIFactory.Rect("EtiquetaLanzamiento", canvas.transform);
            aimRt.anchorMin = aimRt.anchorMax = Vector2.zero;
            aimRt.pivot = new Vector2(0, 0);
            UIFactory.Img(aimRt, new Color(0.05f, 0.07f, 0.12f, 0.85f), false);
            var h = aimRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 8, 8);
            h.childControlWidth = true;
            h.childControlHeight = true;
            var csf = aimRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            aimText = UIFactory.Label(aimRt, "", 30, UIFactory.Txt);
            aimText.horizontalOverflow = HorizontalWrapMode.Overflow;
            aimBox = aimRt.gameObject;
            aimBox.SetActive(false);
        }

        // ---------------- Acciones ----------------
        void Select(PType t)
        {
            Selected = t;
            for (int k = 0; k < ParticleDefs.Count; k++)
            {
                bool on = k == (int)t;
                chipBg[k].color = on ? UIFactory.ChipOn : UIFactory.ChipCol;
                chipOutline[k].effectColor = on ? ParticleDefs.All[k].Color : UIFactory.LineCol;
                chipOutline[k].effectDistance = on ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            }
            fichaText.text = ParticleDefs.Ficha(t);
        }

        void TogglePanel() => SetPanelOpen(!panelOpen);

        void SetPanelOpen(bool open)
        {
            panelOpen = open;
            if (open)
            {
                panel.anchorMin = new Vector2(0, 0);
                panel.anchorMax = new Vector2(1, 0.45f);
                panel.offsetMin = Vector2.zero;
                panel.offsetMax = Vector2.zero;
            }
            else
            {
                panel.anchorMin = new Vector2(0, 0);
                panel.anchorMax = new Vector2(1, 0);
                panel.offsetMin = Vector2.zero;
                panel.offsetMax = new Vector2(0, 88);
            }
            panelToggleText.text = open ? "Ocultar panel  (−)" : "Mostrar panel  (+)";
        }

        void ToggleSettings()
        {
            bool open = !settingsBox.activeSelf;
            settingsBox.SetActive(open);
            settingsToggleText.text = open ? "Ajustes de la cámara  (−)" : "Ajustes de la cámara  (+)";
        }

        void TogglePause()
        {
            engine.Paused = !engine.Paused;
            pauseText.text = engine.Paused ? "Seguir" : "Pausa";
        }

        void RefreshSettings()
        {
            if (sB == null) return;
            float b = engine.B;
            sB.SetValueWithoutNotify(Mathf.Round(b * 10f));
            oB.text = (b > 0 ? "+" : "") + ParticleDefs.Num(b);
            sV.SetValueWithoutNotify(Mathf.Round(engine.Speed * 4f));
            oV.text = ParticleDefs.Num(engine.Speed, "0.##") + "×";
            sT.SetValueWithoutNotify(trails.MaxPoints / 10);
            oT.text = trails.MaxPoints == 0 ? "sin" : trails.MaxPoints.ToString();
            sD.SetValueWithoutNotify(Mathf.Round(engine.Density * 20f));
            oD.text = Mathf.RoundToInt(engine.Density * 100f) + " %";
            for (int a = 0; a < 3; a++) axisBtn[a].color = engine.FieldAxis == a ? UIFactory.ChipOn : UIFactory.ChipCol;
            coulombText.text = "Fuerza eléctrica (Coulomb): " + (engine.Coulomb ? "Sí" : "No");
            bounceText.text = "Rebotar en los bordes: " + (engine.Bounce ? "Sí" : "No");
        }

        // ---------------- API para otros componentes ----------------
        public void SetPlaced(bool placed)
        {
            if (relocateBtn != null) relocateBtn.SetActive(placed);
        }

        /// <summary>Muestra un mensaje de estado. autoHide &gt; 0 lo oculta tras esos segundos.</summary>
        public void SetStatus(string text, float autoHide = 0f)
        {
            if (statusText == null) return;
            if (text == lastStatus) return;
            lastStatus = text;
            statusText.text = text;
            statusBox.SetActive(!string.IsNullOrEmpty(text));
            statusHideAt = autoHide > 0f ? Time.unscaledTime + autoHide : -1f;
        }

        public void ShowPermissionButton(bool show)
        {
            if (permissionBtn != null && permissionBtn.activeSelf != show) permissionBtn.SetActive(show);
        }

        public void ShowAimLabel(Vector2 screenPos, string text)
        {
            if (aimBox == null) return;
            if (!aimBox.activeSelf) aimBox.SetActive(true);
            if (aimText.text != text) aimText.text = text;
            aimRt.position = screenPos + new Vector2(30f, 30f) * canvas.scaleFactor;
        }

        public void HideAimLabel()
        {
            if (aimBox != null && aimBox.activeSelf) aimBox.SetActive(false);
        }

        /// <summary>¿Hay un elemento de interfaz bajo este punto de pantalla?</summary>
        public bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (ped == null || ped.currentInputModule != es.currentInputModule) ped = new PointerEventData(es);
            ped.position = screenPos;
            raycastHits.Clear();
            es.RaycastAll(ped, raycastHits);
            for (int i = 0; i < raycastHits.Count; i++)
                if (raycastHits[i].gameObject != null && raycastHits[i].gameObject.GetComponentInParent<Canvas>() == canvas) return true;
            return false;
        }

        void AddLog(string text)
        {
            int idx = -1;
            for (int i = 0; i < logs.Length; i++) if (!logs[i].go.activeSelf) { idx = i; break; }
            if (idx < 0) { idx = logNext; logNext = (logNext + 1) % logs.Length; }
            var e = logs[idx];
            e.text.text = text;
            e.t = Time.unscaledTime;
            e.cg.alpha = 1f;
            e.go.SetActive(true);
            e.go.transform.SetAsLastSibling();
            logs[idx] = e;
        }

        // ---------------- Bucle ----------------
        void Update()
        {
            ApplySafeArea();
            float now = Time.unscaledTime;

            for (int i = 0; i < logs.Length; i++)
            {
                if (!logs[i].go.activeSelf) continue;
                float age = now - logs[i].t;
                if (age > 3.8f)
                {
                    float a = 1f - (age - 3.8f) / 0.8f;
                    if (a <= 0f) logs[i].go.SetActive(false);
                    else logs[i].cg.alpha = a;
                }
            }

            if (statusHideAt > 0f && now > statusHideAt)
            {
                statusHideAt = -1f;
                lastStatus = "";
                statusBox.SetActive(false);
            }

            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer <= 0f)
            {
                statsTimer = 0.25f;
                int n = engine.Count, m = engine.ChargedCount(), k = engine.Events;
                if (n != lastN || m != lastM || k != lastK)
                {
                    lastN = n; lastM = m; lastK = k;
                    statsText.text = $"{n} partículas · {m} con carga · {k} eventos";
                }
            }
        }
    }
}
