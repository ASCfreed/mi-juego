using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace ParticulasRA
{
    /// <summary>Detección de planos, indicador, colocación anclada, Reubicar y gestos de pellizco / giro.</summary>
    public class ARPlacementController : MonoBehaviour
    {
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] Camera arCamera;
        [SerializeField] Transform chamber;   // CamaraBurbujas
        [SerializeField] Transform simRoot;   // hijo de chamber; su escala = tamaño/2
        [SerializeField] SimulationEngine engine;
        [SerializeField] UIController ui;
        [SerializeField] ExperimentPresets presets;
        [SerializeField] Material lineMaterial;
        [SerializeField] float chamberSize = 0.5f; // TAMANO_CAMARA en metros

        public bool Placed { get; private set; }
        public int PlacedFrame { get; private set; } = -1;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>(8);
        static readonly Color AccentColor = new Color(0.31f, 0.82f, 1f, 0.9f);

        LineRenderer indicator;
        GameObject anchorGO;
        bool firstPlacement = true;
        float userScale = 1f, pinchStartDist, pinchStartScale, twistPrevAngle;
        bool twoFingerActive;
        float permissionTimer;
        bool cameraPermissionOk = true;

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void Awake()
        {
            if (raycastManager == null || planeManager == null || arCamera == null || chamber == null ||
                simRoot == null || engine == null || ui == null || presets == null || lineMaterial == null)
            {
                Debug.LogError("[ARPlacementController] Faltan referencias. Ejecuta «Partículas RA/2. Crear escena».");
                enabled = false;
                return;
            }
            if (GetComponent<XRArranque>() == null) gameObject.AddComponent<XRArranque>();
            simRoot.localScale = Vector3.one * (chamberSize * 0.5f);
            chamber.gameObject.SetActive(false);
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            BuildIndicator();
        }

        void Start()
        {
            ui.OnRelocate += Relocate;
            ui.OnRequestPermission += RequestPermission;
        }

        void OnDestroy()
        {
            if (ui == null) return;
            ui.OnRelocate -= Relocate;
            ui.OnRequestPermission -= RequestPermission;
        }

        void BuildIndicator()
        {
            var go = new GameObject("IndicadorColocacion");
            indicator = go.AddComponent<LineRenderer>();
            indicator.useWorldSpace = false;
            indicator.loop = true;
            indicator.sharedMaterial = lineMaterial;
            indicator.startColor = indicator.endColor = AccentColor;
            indicator.widthMultiplier = 0.006f;
            indicator.shadowCastingMode = ShadowCastingMode.Off;
            const int seg = 48;
            indicator.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                indicator.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.1f, 0.002f, Mathf.Sin(a) * 0.1f));
            }
            indicator.enabled = false;
        }

        void Update()
        {
            string blocking = BlockingStatus();
            if (blocking != null)
            {
                ui.SetStatus(blocking + "\n" + XRArranque.Linea());
                if (indicator) indicator.enabled = false;
                return;
            }

            if (!Placed) UpdatePlacement();
            else HandleGestures();
        }

        /// <summary>Mensajes que impiden usar la RA (permiso, soporte, instalación).</summary>
        string BlockingStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            permissionTimer -= Time.unscaledDeltaTime;
            if (permissionTimer <= 0f)
            {
                permissionTimer = 0.5f;
                cameraPermissionOk = Permission.HasUserAuthorizedPermission(Permission.Camera);
                ui.ShowPermissionButton(!cameraPermissionOk);
            }
            if (!cameraPermissionOk) return "Se necesita permiso de cámara para la realidad aumentada";
#endif
            if (Application.isEditor) return null;
            switch (ARSession.state)
            {
                case ARSessionState.Unsupported: return "Este dispositivo no es compatible con ARCore";
                case ARSessionState.NeedsInstall:
                case ARSessionState.Installing: return "Instalando Servicios de Google Play para RA…";
                case ARSessionState.None:
                case ARSessionState.CheckingAvailability: return "Comprobando compatibilidad con ARCore…";
                default: return null;
            }
        }

        void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Permission.RequestUserPermission(Permission.Camera);
            permissionTimer = 0.5f;
#endif
        }

        void UpdatePlacement()
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool hasHit = raycastManager.Raycast(center, Hits, TrackableType.PlaneWithinPolygon);
            Pose pose = hasHit ? Hits[0].pose : default;

            if (hasHit)
            {
                indicator.enabled = true;
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
                indicator.transform.SetPositionAndRotation(pose.position, Quaternion.identity);
                indicator.transform.localScale = Vector3.one * pulse;
                ui.SetStatus("Toca para colocar la cámara de burbujas");
            }
            else
            {
                indicator.enabled = false;
                ui.SetStatus(Application.isEditor
                    ? "Editor: haz clic para colocar la cámara frente a ti"
                    : "Mueve el móvil despacio para detectar el suelo…\n" + XRArranque.Linea());
            }

            var s = PointerInput.Read();
            if (!s.began || s.touchCount != 1 || ui.IsOverUI(s.pos)) return;

            if (hasHit) Place(pose);
            else if (Application.isEditor)
            {
                // Modo de prueba sin RA: coloca la cámara 1,2 m delante de la cámara virtual
                var cam = arCamera.transform;
                Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
                Place(new Pose(cam.position + f.normalized * 1.2f - Vector3.up * 0.4f, Quaternion.identity));
            }
        }

        void Place(Pose pose)
        {
            Vector3 camF = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up);
            if (camF.sqrMagnitude < 1e-4f) camF = Vector3.forward;
            // +Z local de la cámara de burbujas apunta en sentido contrario al usuario
            Quaternion rot = Quaternion.LookRotation(camF.normalized, Vector3.up);

            chamber.SetParent(null, true);
            if (anchorGO != null) Destroy(anchorGO);

            anchorGO = new GameObject("AnclaCamaraBurbujas");
            anchorGO.transform.SetPositionAndRotation(pose.position, rot);
            if (!Application.isEditor) anchorGO.AddComponent<ARAnchor>();

            chamber.SetParent(anchorGO.transform, false);
            chamber.localRotation = Quaternion.identity;
            ApplyScale();

            chamber.gameObject.SetActive(true);
            Placed = true;
            PlacedFrame = Time.frameCount;
            indicator.enabled = false;
            SetPlanesVisible(false);
            engine.Running = true;
            ui.SetPlaced(true);
            ui.SetStatus("Elige una partícula y arrastra para lanzarla", 6f);

            if (firstPlacement)
            {
                firstPlacement = false;
                presets.Cosmic();
            }
        }

        public void Relocate()
        {
            if (!Placed) return;
            Placed = false;
            engine.Running = false;
            chamber.gameObject.SetActive(false);
            chamber.SetParent(null, true);
            if (anchorGO != null) Destroy(anchorGO);
            anchorGO = null;
            SetPlanesVisible(true);
            ui.SetPlaced(false);
        }

        void SetPlanesVisible(bool visible)
        {
            planeManager.enabled = visible;
            foreach (var plane in planeManager.trackables) plane.gameObject.SetActive(visible);
        }

        void ApplyScale()
        {
            chamber.localScale = Vector3.one * userScale;
            // La base del cubo apoya en el plano
            chamber.localPosition = new Vector3(0f, chamberSize * 0.5f * userScale, 0f);
        }

        void HandleGestures()
        {
            var touches = Touch.activeTouches;
            if (touches.Count == 2)
            {
                Vector2 a = touches[0].screenPosition, b = touches[1].screenPosition;
                float d = Vector2.Distance(a, b);
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                if (!twoFingerActive)
                {
                    if (ui.IsOverUI(a) || ui.IsOverUI(b)) return;
                    twoFingerActive = true;
                    pinchStartDist = Mathf.Max(d, 1f);
                    pinchStartScale = userScale;
                    twistPrevAngle = ang;
                }
                else
                {
                    userScale = Mathf.Clamp(pinchStartScale * d / pinchStartDist, 0.2f, 4f);
                    ApplyScale();
                    float delta = Mathf.DeltaAngle(twistPrevAngle, ang);
                    twistPrevAngle = ang;
                    chamber.Rotate(Vector3.up, -delta, Space.World);
                }
                return;
            }
            twoFingerActive = false;

            // Editor: rueda del ratón para escalar
            var mouse = Mouse.current;
            if (mouse != null && touches.Count == 0)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    userScale = Mathf.Clamp(userScale * (1f + scroll * 0.001f), 0.2f, 4f);
                    ApplyScale();
                }
            }
        }
    }
}
