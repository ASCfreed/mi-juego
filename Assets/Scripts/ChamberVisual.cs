using UnityEngine;
using UnityEngine.Rendering;

namespace ParticulasRA
{
    /// <summary>Caja translúcida con aristas luminosas que delimita la cámara de burbujas ([-1, 1]³ local).</summary>
    public class ChamberVisual : MonoBehaviour
    {
        [SerializeField] Material glowMaterial; // URP Unlit transparente aditivo
        [SerializeField] Material lineMaterial; // Sprites/Default

        static readonly Color EdgeColor = new Color(0.31f, 0.82f, 1f, 0.55f);
        static readonly Color FaceColor = new Color(0.31f, 0.82f, 1f, 0.045f);
        const float EdgeWidth = 0.012f; // u

        readonly LineRenderer[] edges = new LineRenderer[12];
        float lastScale = -1f;

        void Awake()
        {
            if (glowMaterial == null || lineMaterial == null)
            {
                Debug.LogError("[ChamberVisual] Faltan materiales.");
                enabled = false;
                return;
            }

            var box = new GameObject("Caja");
            box.transform.SetParent(transform, false);
            box.transform.localScale = Vector3.one * 2f;
            box.AddComponent<MeshFilter>().sharedMesh = ProceduralAssets.Cube;
            var mr = box.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ProceduralAssets.Tinted(glowMaterial, FaceColor, Texture2D.whiteTexture);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f);

            int e = 0;
            for (int i = 0; i < 8; i++)
            for (int bit = 1; bit <= 4; bit <<= 1)
            {
                if ((i & bit) != 0) continue;
                var go = new GameObject("Arista");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.SetPosition(0, corners[i]);
                lr.SetPosition(1, corners[i | bit]);
                lr.sharedMaterial = lineMaterial;
                lr.startColor = lr.endColor = EdgeColor;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                edges[e++] = lr;
            }
        }

        void LateUpdate()
        {
            float s = transform.lossyScale.x;
            if (Mathf.Approximately(s, lastScale)) return;
            lastScale = s;
            foreach (var lr in edges) if (lr != null) lr.widthMultiplier = EdgeWidth * s;
        }
    }
}
