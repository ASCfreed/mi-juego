using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;

namespace ParticulasRA
{
    /// <summary>Lanzamiento de partículas arrastrando el dedo (sección 3.4).</summary>
    public class LaunchInput : MonoBehaviour
    {
        [SerializeField] SimulationEngine engine;
        [SerializeField] ARPlacementController placement;
        [SerializeField] UIController ui;
        [SerializeField] Camera arCamera;
        [SerializeField] Transform simRoot;
        [SerializeField] Material lineMaterial;

        const float TapPx = 20f;

        bool aiming;
        Vector2 startPx, curPx;
        Vector3 originLocal;
        LineRenderer preview;

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void Awake()
        {
            if (engine == null || placement == null || ui == null || arCamera == null || simRoot == null || lineMaterial == null)
            {
                Debug.LogError("[LaunchInput] Faltan referencias.");
                enabled = false;
                return;
            }
            var go = new GameObject("PrevisualizacionLanzamiento");
            go.transform.SetParent(simRoot, false);
            preview = go.AddComponent<LineRenderer>();
            preview.useWorldSpace = false;
            preview.positionCount = 2;
            preview.sharedMaterial = lineMaterial;
            preview.numCapVertices = 2;
            preview.shadowCastingMode = ShadowCastingMode.Off;
            preview.receiveShadows = false;
            preview.enabled = false;
        }

        void Update()
        {
            if (!placement.Placed || Time.frameCount == placement.PlacedFrame) { Cancel(); return; }

            var s = PointerInput.Read();
            if (s.touchCount >= 2) { Cancel(); return; }

            if (s.began)
            {
                if (ui.IsOverUI(s.pos)) return;
                aiming = true;
                startPx = curPx = s.pos;
                originLocal = ComputeOrigin(startPx);
            }
            if (!aiming) return;
            if (s.canceled) { Cancel(); return; }

            curPx = s.pos;
            if (s.ended)
            {
                Launch();
                Cancel();
                return;
            }
            UpdatePreview();
        }

        /// <summary>Punto de entrada del rayo en el cubo [-1,1]³ (slab test), desplazado 0,05 hacia dentro.</summary>
        Vector3 ComputeOrigin(Vector2 px)
        {
            Ray ray = arCamera.ScreenPointToRay(px);
            Vector3 o = simRoot.InverseTransformPoint(ray.origin);
            Vector3 d = simRoot.InverseTransformVector(ray.direction);

            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                if (Mathf.Abs(d[a]) < 1e-6f)
                {
                    if (o[a] < -1f || o[a] > 1f) return Vector3.zero;
                    continue;
                }
                float t1 = (-1f - o[a]) / d[a];
                float t2 = (1f - o[a]) / d[a];
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tmin = Mathf.Max(tmin, t1);
                tmax = Mathf.Min(tmax, t2);
                if (tmin > tmax) return Vector3.zero;
            }
            if (tmax < 0f) return Vector3.zero;

            Vector3 p = o + d * Mathf.Max(tmin, 0f) + d.normalized * 0.05f;
            for (int a = 0; a < 3; a++) p[a] = Mathf.Clamp(p[a], -0.95f, 0.95f);
            return p;
        }

        float DragLength => (curPx - startPx).magnitude;

        Vector3 Direction()
        {
            Vector2 delta = curPx - startPx;
            Transform cam = arCamera.transform;
            Vector3 world = delta.magnitude < TapPx ? cam.forward : cam.right * delta.x + cam.up * delta.y;
            Vector3 local = simRoot.InverseTransformDirection(world);
            return local.sqrMagnitude > 1e-8f ? local.normalized : Vector3.forward;
        }

        float Speed(float len) =>
            len < TapPx ? 0.5f * ParticleDefs.C
                        : Mathf.Min(0.95f * ParticleDefs.C, 0.95f * ParticleDefs.C * len / (0.35f * Screen.width));

        static float PhotonEnergy(float len) =>
            len < TapPx ? 2f : Mathf.Min(30f, 0.3f + 30f * len / (0.8f * Screen.width));

        void Launch()
        {
            PType t = ui.Selected;
            float len = DragLength;
            Vector3 dir = Direction();
            if (t == PType.G) engine.Spawn(PType.G, originLocal, dir, PhotonEnergy(len));
            else engine.Spawn(t, originLocal, dir * Speed(len));
        }

        void UpdatePreview()
        {
            PType t = ui.Selected;
            float len = DragLength;
            Vector3 dir = Direction();
            float frac;
            string label;
            if (t == PType.G)
            {
                float e = PhotonEnergy(len);
                frac = e / 30f;
                label = $"E = {ParticleDefs.Num(e)} MeV";
            }
            else
            {
                float v = Speed(len);
                frac = v / ParticleDefs.C;
                label = $"v = {ParticleDefs.Num(v / ParticleDefs.C, "0.00")} c";
            }

            Color c = ParticleDefs.Get(t).Color;
            preview.startColor = c;
            preview.endColor = new Color(c.r, c.g, c.b, 0.3f);
            preview.widthMultiplier = 0.01f * simRoot.lossyScale.x;
            preview.SetPosition(0, originLocal);
            preview.SetPosition(1, originLocal + dir * (0.15f + 0.6f * frac));
            preview.enabled = true;
            ui.ShowAimLabel(curPx, label);
        }

        void Cancel()
        {
            if (!aiming && (preview == null || !preview.enabled)) return;
            aiming = false;
            if (preview != null) preview.enabled = false;
            ui.HideAimLabel();
        }
    }
}
