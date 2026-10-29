using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ParticulasRA
{
    /// <summary>
    /// Motor de simulación. Trabaja en coordenadas normalizadas: la cámara de burbujas es el cubo [-1, 1]³
    /// en el espacio local de SimRoot. Estado en arrays preasignados (SoA) para no generar basura por frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SimulationEngine : MonoBehaviour
    {
        public const int MaxParticles = 300;
        const float K = 0.04f;                    // constante de Coulomb de simulación
        const float Eps2 = 0.04f * 0.04f;         // suavizado (softening) al cuadrado
        const float BaseStep = 1f / 120f;         // paso máximo de integración
        const float ReactionRange2 = 0.1f * 0.1f; // distancia² a partir de la cual se comprueban reacciones

        [SerializeField] ReactionSystem reactions;
        [SerializeField] TrailManager trails;

        // ---------- Estado (SoA) ----------
        [NonSerialized] public readonly PType[] type = new PType[MaxParticles];
        [NonSerialized] public readonly Vector3[] pos = new Vector3[MaxParticles];
        [NonSerialized] public readonly Vector3[] vel = new Vector3[MaxParticles];
        [NonSerialized] public readonly Vector3[] acc = new Vector3[MaxParticles];
        [NonSerialized] public readonly float[] age = new float[MaxParticles];
        [NonSerialized] public readonly float[] life = new float[MaxParticles];
        [NonSerialized] public readonly float[] energy = new float[MaxParticles];
        [NonSerialized] public readonly bool[] forcePair = new bool[MaxParticles];
        [NonSerialized] public readonly bool[] dead = new bool[MaxParticles];
        [NonSerialized] public readonly int[] trailId = new int[MaxParticles];

        public int Count { get; private set; }

        // ---------- Ajustes ----------
        [Range(0.25f, 3f)] public float Speed = 1f;
        [Range(0f, 1f)] public float Density = 0.2f;
        public bool Coulomb = true;
        public bool Bounce = false;
        [NonSerialized] public bool Paused;
        [NonSerialized] public bool Running; // lo activa ARPlacementController al colocar la cámara

        public float B { get; private set; } = 3.6f;
        /// <summary>0 = X, 1 = Y, 2 = Z (local de la cámara).</summary>
        public int FieldAxis { get; private set; } = 2;
        public float SimTime { get; private set; }
        public int Events { get; private set; }

        public Vector3 FieldVector => AxisVector(FieldAxis) * B;

        // ---------- Eventos ----------
        public event Action<string> OnLog;
        public event Action<Vector3, Color, float> OnFlash; // posición local, color, radio (u)
        public event Action OnSettingsChanged;
        public event Action OnCleared;

        struct Scheduled { public float at; public Action fn; }
        readonly List<Scheduled> schedule = new List<Scheduled>(64);

        void Awake()
        {
            if (reactions == null || trails == null)
            {
                Debug.LogError("[SimulationEngine] Faltan referencias (reactions / trails). Ejecuta «Partículas RA/2. Crear escena».");
                enabled = false;
                return;
            }
            reactions.Bind(this);
        }

        public static Vector3 AxisVector(int axis) =>
            axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        public static Vector3 Cap(Vector3 v, float fractionOfC)
        {
            float max = ParticleDefs.C * fractionOfC;
            float s = v.magnitude;
            return s > max ? v * (max / s) : v;
        }

        // ---------- API pública ----------
        public void SetB(float b)
        {
            B = Mathf.Clamp(b, -12f, 12f);
            OnSettingsChanged?.Invoke();
        }

        public void SetAxis(int axis)
        {
            FieldAxis = Mathf.Clamp(axis, 0, 2);
            OnSettingsChanged?.Invoke();
        }

        public void NotifySettingsChanged() => OnSettingsChanged?.Invoke();

        /// <summary>Evento físico: cuenta en el contador y se muestra en el registro.</summary>
        public void Log(string text)
        {
            Events++;
            OnLog?.Invoke(text);
        }

        /// <summary>Mensaje informativo: se muestra en el registro pero no cuenta como evento.</summary>
        public void Info(string text) => OnLog?.Invoke(text);

        public void Flash(Vector3 localPos, Color color, float radius) => OnFlash?.Invoke(localPos, color, radius);

        public void Schedule(float delay, Action fn) => schedule.Add(new Scheduled { at = SimTime + delay, fn = fn });

        /// <summary>Crea una partícula. Devuelve su índice o -1 si se alcanzó el máximo.</summary>
        public int Spawn(PType t, Vector3 p, Vector3 v, float photonEnergy = 0f, bool forcePairFlag = false)
        {
            if (Count >= MaxParticles) return -1;
            var info = ParticleDefs.Get(t);

            if (t == PType.G)
                v = v.sqrMagnitude > 1e-8f ? v.normalized * ParticleDefs.C : Random.onUnitSphere * ParticleDefs.C;
            else
                v = Cap(v, 0.98f);

            int i = Count++;
            type[i] = t;
            pos[i] = p;
            vel[i] = v;
            acc[i] = Vector3.zero;
            age[i] = 0f;
            // Vida exponencial: vida = −ln(1 − U) · τ
            life[i] = info.Unstable ? -Mathf.Log(1f - Random.value * 0.9999f) * info.Tau : float.PositiveInfinity;
            energy[i] = photonEnergy;
            forcePair[i] = forcePairFlag;
            dead[i] = false;
            trailId[i] = trails.Acquire(t, p);
            return i;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) trails.Release(trailId[i], false);
            Count = 0;
            schedule.Clear();
            trails.ClearAll();
            OnCleared?.Invoke();
        }

        // ---------- Bucle ----------
        void Update()
        {
            trails.Frozen = Paused || !Running;
            if (!Running || Paused) return;

            float simDt = Mathf.Min(Time.deltaTime * Speed, 0.05f);
            if (simDt <= 0f) return;
            int n = Mathf.Max(1, Mathf.CeilToInt(simDt / BaseStep));
            float dt = simDt / n;
            for (int k = 0; k < n; k++) Step(dt);
        }

        void Step(float dt)
        {
            int n = Count;
            Vector3 field = FieldVector;
            bool hasField = Mathf.Abs(B) > 1e-5f;

            for (int i = 0; i < n; i++) acc[i] = Vector3.zero;

            // 1) Fuerzas de Coulomb + detección de reacciones en un único bucle O(n²)
            for (int i = 0; i < n; i++)
            {
                if (dead[i]) continue;
                var ti = ParticleDefs.All[(int)type[i]];
                for (int j = i + 1; j < n; j++)
                {
                    if (dead[j]) continue;
                    Vector3 r = pos[i] - pos[j];
                    float d2raw = r.sqrMagnitude;

                    if (Coulomb && ti.Charge != 0)
                    {
                        var tj = ParticleDefs.All[(int)type[j]];
                        if (tj.Charge != 0)
                        {
                            // F = K·qi·qj·r / (|r|² + ε²)^(3/2)
                            float d2 = d2raw + Eps2;
                            float f = K * ti.Charge * tj.Charge / (d2 * Mathf.Sqrt(d2));
                            acc[i] += r * (f / ti.Mass);
                            acc[j] -= r * (f / tj.Mass);
                        }
                    }

                    if (d2raw < ReactionRange2)
                    {
                        reactions.CheckPair(i, j, d2raw, dt);
                        if (dead[i]) break;
                    }
                }
            }

            // 2) Integración (método de Boris para la fuerza de Lorentz)
            float vmax = 0.98f * ParticleDefs.C;
            for (int i = 0; i < n; i++)
            {
                if (dead[i]) continue;
                var info = ParticleDefs.All[(int)type[i]];
                Vector3 v = vel[i];

                if (info.Mass > 0f)
                {
                    Vector3 a = acc[i];
                    float halfDt = dt * 0.5f;

                    v += a * halfDt;                                   // medio impulso eléctrico
                    if (info.Charge != 0 && hasField)
                    {
                        // Rotación magnética exacta: t = (q·B/m)·dt/2 ; s = 2t/(1+|t|²)
                        Vector3 t = field * (info.Charge / info.Mass * halfDt);
                        Vector3 s = t * (2f / (1f + t.sqrMagnitude));
                        Vector3 vPrime = v + Vector3.Cross(v, t);       // v' = v⁻ + v⁻ × t
                        v += Vector3.Cross(vPrime, s);                  // v⁺ = v⁻ + v' × s
                    }
                    v += a * halfDt;                                   // segundo medio impulso

                    // Frenado por ionización del medio (solo partículas con carga)
                    if (Density > 0f && info.Charge != 0)
                        v *= Mathf.Max(0f, 1f - Density * 0.36f * dt);

                    float sp = v.magnitude;
                    if (sp > vmax) { v *= vmax / sp; sp = vmax; }
                    vel[i] = v;

                    // Partículas detenidas por el medio
                    if (Density > 0f && info.Charge != 0 && sp < 0.036f && age[i] > 0.5f)
                    {
                        if (type[i] == PType.Ep) { reactions.PositronStopped(i); continue; }
                        if (type[i] == PType.Em) { dead[i] = true; continue; }
                        // μ y π esperan quietos a desintegrarse; p y p̄ permanecen.
                    }
                }

                Vector3 p = pos[i] + v * dt;

                if (Bounce)
                {
                    Reflect(ref p.x, ref v.x);
                    Reflect(ref p.y, ref v.y);
                    Reflect(ref p.z, ref v.z);
                    vel[i] = v;
                }
                else if (Mathf.Abs(p.x) > 1.05f || Mathf.Abs(p.y) > 1.05f || Mathf.Abs(p.z) > 1.05f)
                {
                    pos[i] = p;
                    dead[i] = true; // escapa: su traza pasa a fantasma
                    continue;
                }

                pos[i] = p;
                age[i] += dt;

                if (age[i] > life[i])
                {
                    dead[i] = true;
                    reactions.Decay(i);
                    continue;
                }

                trails.AddPoint(trailId[i], p);
            }

            SimTime += dt;
            RunSchedule();
            Compact();
        }

        static void Reflect(ref float x, ref float v)
        {
            if (x < -1f) { x = -2f - x; v = -v; }
            else if (x > 1f) { x = 2f - x; v = -v; }
        }

        void RunSchedule()
        {
            for (int i = schedule.Count - 1; i >= 0; i--)
            {
                if (i >= schedule.Count) continue;
                if (schedule[i].at > SimTime) continue;
                var fn = schedule[i].fn;
                schedule.RemoveAt(i);
                fn?.Invoke();
            }
        }

        /// <summary>Elimina las partículas marcadas como muertas sin reasignar memoria.</summary>
        void Compact()
        {
            int w = 0;
            for (int r = 0; r < Count; r++)
            {
                if (dead[r])
                {
                    trails.Release(trailId[r], true);
                    continue;
                }
                if (w != r)
                {
                    type[w] = type[r]; pos[w] = pos[r]; vel[w] = vel[r]; acc[w] = acc[r];
                    age[w] = age[r]; life[w] = life[r]; energy[w] = energy[r];
                    forcePair[w] = forcePair[r]; trailId[w] = trailId[r];
                }
                dead[w] = false;
                w++;
            }
            Count = w;
        }

        public int ChargedCount()
        {
            int m = 0;
            for (int i = 0; i < Count; i++) if (ParticleDefs.All[(int)type[i]].Charge != 0) m++;
            return m;
        }
    }
}
