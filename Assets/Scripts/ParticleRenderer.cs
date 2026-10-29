using UnityEngine;
using UnityEngine.Rendering;

namespace ParticulasRA
{
    /// <summary>Dibuja las partículas con GPU instancing: una llamada por tipo para el núcleo y otra para el halo.</summary>
    public class ParticleRenderer : MonoBehaviour
    {
        [SerializeField] SimulationEngine engine;
        [SerializeField] Material coreMaterial; // URP Unlit opaco
        [SerializeField] Material glowMaterial; // URP Unlit transparente aditivo
        [SerializeField] Camera viewCamera;

        readonly Matrix4x4[][] coreM = new Matrix4x4[ParticleDefs.Count][];
        readonly Matrix4x4[][] glowM = new Matrix4x4[ParticleDefs.Count][];
        readonly int[] counts = new int[ParticleDefs.Count];
        RenderParams[] rpCore, rpGlow;

        void Awake()
        {
            if (engine == null || coreMaterial == null || glowMaterial == null)
            {
                Debug.LogError("[ParticleRenderer] Faltan referencias.");
                enabled = false;
                return;
            }
            rpCore = new RenderParams[ParticleDefs.Count];
            rpGlow = new RenderParams[ParticleDefs.Count];
            for (int t = 0; t < ParticleDefs.Count; t++)
            {
                var info = ParticleDefs.All[t];
                coreM[t] = new Matrix4x4[SimulationEngine.MaxParticles];
                glowM[t] = new Matrix4x4[SimulationEngine.MaxParticles];

                var glowCol = info.Color; glowCol.a = 0.25f;
                rpCore[t] = new RenderParams(ProceduralAssets.Tinted(coreMaterial, info.Color))
                    { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                rpGlow[t] = new RenderParams(ProceduralAssets.Tinted(glowMaterial, glowCol, ProceduralAssets.GlowTexture))
                    { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            }
        }

        void LateUpdate()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            Quaternion camRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            Transform root = transform;
            float sc = root.lossyScale.x;

            for (int t = 0; t < counts.Length; t++) counts[t] = 0;

            int n = engine.Count;
            for (int i = 0; i < n; i++)
            {
                if (engine.dead[i]) continue;
                int t = (int)engine.type[i];
                float r = ParticleDefs.All[t].Radius * sc;
                Vector3 w = root.TransformPoint(engine.pos[i]);
                int c = counts[t]++;
                coreM[t][c] = Matrix4x4.TRS(w, Quaternion.identity, Vector3.one * (2f * r));
                glowM[t][c] = Matrix4x4.TRS(w, camRot, Vector3.one * (6f * r));
            }

            var bounds = new Bounds(root.position, Vector3.one * (4f * sc + 1f));
            for (int t = 0; t < counts.Length; t++)
            {
                if (counts[t] == 0) continue;
                rpCore[t].worldBounds = bounds;
                rpGlow[t].worldBounds = bounds;
                Graphics.RenderMeshInstanced(rpCore[t], ProceduralAssets.Sphere, 0, coreM[t], counts[t]);
                Graphics.RenderMeshInstanced(rpGlow[t], ProceduralAssets.Quad, 0, glowM[t], counts[t]);
            }
        }
    }
}
