using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticulasRA
{
    /// <summary>
    /// Pool de LineRenderer en espacio local de SimRoot (coordenadas de simulación).
    /// Cada traza usa un buffer circular; al morir la partícula pasa a "fantasma" y se va acortando.
    /// </summary>
    public class TrailManager : MonoBehaviour
    {
        public const int PoolSize = 560;     // 300 vivas + 260 fantasmas
        public const int Capacity = 400;     // puntos máximos por traza
        public const int MaxGhosts = 260;
        const float ChargedWidth = 0.006f;   // u
        const float NeutralWidth = 0.004f;   // u

        [SerializeField] Material trailMaterial; // Sprites/Default
        [SerializeField] Material dashMaterial;  // Sprites/Default (se le asigna textura discontinua)

        public int MaxPoints { get; private set; } = 180;
        /// <summary>Lo controla el motor: con la simulación en pausa los fantasmas no se desvanecen.</summary>
        [System.NonSerialized] public bool Frozen;

        sealed class Trail
        {
            public LineRenderer lr;
            public readonly Vector3[] ring = new Vector3[Capacity];
            public int start, len;
            public bool active, ghost, dirty;
            public float baseWidth;
        }

        Trail[] trails;
        readonly Stack<int> free = new Stack<int>(PoolSize);
        readonly List<int> ghosts = new List<int>(MaxGhosts + 8);
        NativeArray<Vector3> scratch;
        Gradient[] gradients;
        Material dashRuntime;
        float lastScale = -1f;

        void Awake()
        {
            if (trailMaterial == null || dashMaterial == null)
            {
                Debug.LogError("[TrailManager] Faltan materiales de traza.");
                enabled = false;
                return;
            }
            scratch = new NativeArray<Vector3>(Capacity, Allocator.Persistent);
            dashRuntime = new Material(dashMaterial) { mainTexture = ProceduralAssets.DashTexture };

            gradients = new Gradient[ParticleDefs.Count];
            for (int t = 0; t < ParticleDefs.Count; t++)
            {
                var info = ParticleDefs.All[t];
                float head = info.Charged ? 0.9f : info.Type == PType.G ? 0.4f : 0.5f;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(info.Color, 0f), new GradientColorKey(info.Color, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(head * 0.55f, 0.6f), new GradientAlphaKey(head, 1f) });
                gradients[t] = g;
            }

            trails = new Trail[PoolSize];
            for (int k = PoolSize - 1; k >= 0; k--)
            {
                var go = new GameObject("Traza");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 0;
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;
                lr.alignment = LineAlignment.View;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = trailMaterial;
                lr.enabled = false;
                trails[k] = new Trail { lr = lr };
                free.Push(k);
            }
        }

        void OnDestroy()
        {
            if (scratch.IsCreated) scratch.Dispose();
        }

        public void SetMaxPoints(int value)
        {
            MaxPoints = Mathf.Clamp(value, 0, Capacity);
            if (trails == null) return;
            foreach (var tr in trails)
            {
                if (!tr.active && !tr.ghost) continue;
                if (tr.len > MaxPoints) Trim(tr, tr.len - MaxPoints);
            }
        }

        /// <summary>Reserva una traza para una partícula nueva. Devuelve -1 si no hay disponibles.</summary>
        public int Acquire(PType t, Vector3 p)
        {
            if (trails == null) return -1;
            int id;
            if (free.Count > 0) id = free.Pop();
            else if (ghosts.Count > 0) { id = ghosts[0]; ghosts.RemoveAt(0); }
            else return -1;

            var tr = trails[id];
            var info = ParticleDefs.Get(t);
            tr.start = 0; tr.len = 0;
            tr.active = true; tr.ghost = false; tr.dirty = true;
            tr.baseWidth = info.Charged ? ChargedWidth : NeutralWidth;
            tr.lr.sharedMaterial = info.Charged ? trailMaterial : dashRuntime;
            tr.lr.textureMode = info.Charged ? LineTextureMode.Stretch : LineTextureMode.Tile;
            tr.lr.colorGradient = gradients[(int)t];
            tr.lr.widthMultiplier = tr.baseWidth * transform.lossyScale.x;
            tr.lr.positionCount = 0;
            tr.lr.enabled = true;
            AddPoint(id, p);
            return id;
        }

        public void AddPoint(int id, Vector3 p)
        {
            if (id < 0 || trails == null || MaxPoints <= 0) return;
            var tr = trails[id];
            if (!tr.active) return;
            if (tr.len < Capacity)
            {
                tr.ring[(tr.start + tr.len) % Capacity] = p;
                tr.len++;
            }
            else
            {
                tr.ring[tr.start] = p;
                tr.start = (tr.start + 1) % Capacity;
            }
            if (tr.len > MaxPoints) Trim(tr, tr.len - MaxPoints);
            tr.dirty = true;
        }

        /// <summary>Libera la traza de una partícula muerta: pasa a fantasma o vuelve directamente al pool.</summary>
        public void Release(int id, bool toGhost)
        {
            if (id < 0 || trails == null) return;
            var tr = trails[id];
            if (!tr.active) return;
            tr.active = false;
            if (toGhost && tr.len >= 2 && MaxPoints > 0)
            {
                tr.ghost = true;
                ghosts.Add(id);
                if (ghosts.Count > MaxGhosts)
                {
                    int oldest = ghosts[0];
                    ghosts.RemoveAt(0);
                    Free(oldest);
                }
            }
            else Free(id);
        }

        public void ClearAll()
        {
            if (trails == null) return;
            for (int i = 0; i < trails.Length; i++)
                if (trails[i].active || trails[i].ghost) Free(i);
            ghosts.Clear();
        }

        void Free(int id)
        {
            var tr = trails[id];
            if (!tr.active && !tr.ghost && !tr.lr.enabled) return;
            tr.active = false;
            tr.ghost = false;
            tr.len = 0;
            tr.start = 0;
            tr.lr.positionCount = 0;
            tr.lr.enabled = false;
            free.Push(id);
        }

        static void Trim(Trail tr, int drop)
        {
            drop = Mathf.Min(drop, tr.len);
            tr.start = (tr.start + drop) % Capacity;
            tr.len -= drop;
            tr.dirty = true;
        }

        void LateUpdate()
        {
            if (trails == null) return;

            float s = transform.lossyScale.x;
            bool scaleChanged = !Mathf.Approximately(s, lastScale);
            lastScale = s;

            // Fantasmas: pierden max(2, 1,2 %) de sus puntos por frame desde la cola
            if (!Frozen)
            {
                for (int g = ghosts.Count - 1; g >= 0; g--)
                {
                    var tr = trails[ghosts[g]];
                    int rm = Mathf.Max(2, Mathf.CeilToInt(tr.len * 0.012f));
                    Trim(tr, rm);
                    if (tr.len < 2)
                    {
                        int id = ghosts[g];
                        ghosts.RemoveAt(g);
                        Free(id);
                    }
                }
            }

            // Subir a los LineRenderer las trazas modificadas
            for (int i = 0; i < trails.Length; i++)
            {
                var tr = trails[i];
                if (!tr.active && !tr.ghost) continue;
                if (scaleChanged) tr.lr.widthMultiplier = tr.baseWidth * s;
                if (!tr.dirty) continue;
                tr.dirty = false;

                int len = MaxPoints > 0 ? tr.len : 0;
                for (int k = 0; k < len; k++) scratch[k] = tr.ring[(tr.start + k) % Capacity];
                tr.lr.positionCount = len;
                if (len > 0) tr.lr.SetPositions(new NativeSlice<Vector3>(scratch, 0, len));
            }
        }
    }
}
