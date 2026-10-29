using UnityEngine;
using UnityEngine.Rendering;

namespace ParticulasRA
{
    /// <summary>Rejilla 4×4×4 de flechas instanciadas que muestran la dirección del campo magnético.</summary>
    public class FieldVisualizer : MonoBehaviour
    {
        [SerializeField] SimulationEngine engine;
        [SerializeField] Material glowMaterial;

        const int N = 64;
        static readonly Color BaseColor = new Color(0.47f, 0.59f, 0.78f, 1f);

        readonly Vector3[] points = new Vector3[N];
        readonly Matrix4x4[] shafts = new Matrix4x4[N];
        readonly Matrix4x4[] heads = new Matrix4x4[N];
        Material mat;
        RenderParams rp;

        void Awake()
        {
            if (engine == null || glowMaterial == null)
            {
                Debug.LogError("[FieldVisualizer] Faltan referencias.");
                enabled = false;
                return;
            }
            float[] g = { -0.75f, -0.25f, 0.25f, 0.75f };
            int k = 0;
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
            for (int z = 0; z < 4; z++)
                points[k++] = new Vector3(g[x], g[y], g[z]);

            mat = ProceduralAssets.Tinted(glowMaterial, BaseColor, Texture2D.whiteTexture);
            rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
        }

        void LateUpdate()
        {
            float b = engine.B;
            float mag = Mathf.Abs(b);
            if (mag < 0.05f) return;

            // Opacidad proporcional a |B|
            Color c = BaseColor;
            c.a = 0.06f + 0.3f * Mathf.Clamp01(mag / 12f);
            ProceduralAssets.SetColor(mat, c);

            Vector3 dirLocal = SimulationEngine.AxisVector(engine.FieldAxis) * Mathf.Sign(b);
            Transform root = transform;
            Quaternion rot = root.rotation * Quaternion.FromToRotation(Vector3.forward, dirLocal);
            Vector3 fw = rot * Vector3.forward;
            float s = root.lossyScale.x;

            for (int k = 0; k < N; k++)
            {
                Vector3 w = root.TransformPoint(points[k]);
                shafts[k] = Matrix4x4.TRS(w - fw * (0.03f * s), rot, new Vector3(0.012f, 0.012f, 0.2f) * s);
                heads[k] = Matrix4x4.TRS(w + fw * (0.07f * s), rot, new Vector3(0.045f, 0.045f, 0.07f) * s);
            }

            rp.worldBounds = new Bounds(root.position, Vector3.one * (3f * s + 0.1f));
            Graphics.RenderMeshInstanced(rp, ProceduralAssets.Cube, 0, shafts, N);
            Graphics.RenderMeshInstanced(rp, ProceduralAssets.Pyramid, 0, heads, N);
        }
    }
}
