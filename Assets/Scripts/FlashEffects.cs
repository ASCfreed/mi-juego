using UnityEngine;
using UnityEngine.Rendering;

namespace ParticulasRA
{
    /// <summary>Destellos de las reacciones: un halo que se expande y se desvanece en 0,6 s.</summary>
    public class FlashEffects : MonoBehaviour
    {
        [SerializeField] SimulationEngine engine;
        [SerializeField] Material glowMaterial;
        [SerializeField] Camera viewCamera;

        const int Max = 32;
        const float Life = 0.6f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        struct Flash { public Vector3 p; public Color c; public float r, age; public bool on; }

        readonly Flash[] flashes = new Flash[Max];
        readonly MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[Max];
        Material mat;
        int next;

        void Awake()
        {
            if (engine == null || glowMaterial == null)
            {
                Debug.LogError("[FlashEffects] Faltan referencias.");
                enabled = false;
                return;
            }
            mat = ProceduralAssets.Tinted(glowMaterial, Color.white, ProceduralAssets.GlowTexture);
            for (int i = 0; i < Max; i++) blocks[i] = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            if (engine == null) return;
            engine.OnFlash += Add;
            engine.OnCleared += ClearAll;
        }

        void OnDisable()
        {
            if (engine == null) return;
            engine.OnFlash -= Add;
            engine.OnCleared -= ClearAll;
        }

        void Add(Vector3 localPos, Color color, float radius)
        {
            flashes[next] = new Flash { p = localPos, c = color, r = radius, age = 0f, on = true };
            next = (next + 1) % Max;
        }

        void ClearAll()
        {
            for (int i = 0; i < Max; i++) flashes[i].on = false;
        }

        void LateUpdate()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            Quaternion camRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            float dt = engine.Paused || !engine.Running ? 0f : Time.deltaTime * engine.Speed;
            Transform root = transform;
            float sc = root.lossyScale.x;

            for (int i = 0; i < Max; i++)
            {
                if (!flashes[i].on) continue;
                flashes[i].age += dt;
                float k = flashes[i].age / Life;
                if (k >= 1f) { flashes[i].on = false; continue; }

                Vector3 w = root.TransformPoint(flashes[i].p);
                float size = (0.04f + flashes[i].r * k * 2f) * sc * 2f;
                Color c = flashes[i].c;
                c.a = 1f - k;
                blocks[i].SetColor(BaseColorId, c);
                blocks[i].SetColor(ColorId, c);

                var rp = new RenderParams(mat)
                {
                    matProps = blocks[i],
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(w, Vector3.one * (size + 0.01f)),
                };
                Graphics.RenderMesh(rp, ProceduralAssets.Quad, 0, Matrix4x4.TRS(w, camRot, Vector3.one * size));
            }
        }
    }
}
