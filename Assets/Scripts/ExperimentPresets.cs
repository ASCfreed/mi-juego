using UnityEngine;
using Random = UnityEngine.Random;

namespace ParticulasRA
{
    /// <summary>Experimentos predefinidos (sección 6).</summary>
    public class ExperimentPresets : MonoBehaviour
    {
        [SerializeField] SimulationEngine engine;

        static readonly PType[] CosmicTypes =
        {
            PType.MuM, PType.MuP, PType.MuM, PType.MuP, PType.PiP, PType.PiM,
            PType.Em, PType.Ep, PType.G, PType.N, PType.MuM, PType.MuP,
        };

        void Awake()
        {
            if (engine == null) { Debug.LogError("[ExperimentPresets] Falta la referencia a SimulationEngine."); enabled = false; }
        }

        void EnsureField()
        {
            if (Mathf.Abs(engine.B) < 2f) engine.SetB(3.6f);
        }

        /// <summary>1) Lluvia de rayos cósmicos desde la cara superior.</summary>
        public void Cosmic()
        {
            engine.Clear();
            EnsureField();
            for (int i = 0; i < 16; i++) engine.Schedule(i * 0.3f, SpawnCosmic);
            engine.Info("Lluvia de rayos cósmicos: muones, piones y más llegando desde arriba");
        }

        void SpawnCosmic()
        {
            PType t = CosmicTypes[Random.Range(0, CosmicTypes.Length)];
            Vector3 p = new Vector3(Random.Range(-0.8f, 0.8f), 0.98f, Random.Range(-0.8f, 0.8f));
            Vector3 d = Quaternion.Euler(Random.Range(-17f, 17f), 0f, Random.Range(-17f, 17f)) * Vector3.down;
            float s = Random.Range(0.72f, 1.38f);
            engine.Spawn(t, p, d * s, t == PType.G ? Random.Range(2f, 27f) : 0f);
        }

        /// <summary>2) Antiprotón contra protón en reposo, compensando la curvatura (sagita).</summary>
        public void ProtonAntiproton()
        {
            engine.Clear();
            EnsureField();

            Vector3 target = new Vector3(0.3f, 0f, 0f);
            const float D = 0.9f;
            Vector3 v = new Vector3(0.72f, 0f, 0f);
            engine.Spawn(PType.P, target, Vector3.zero);

            var info = ParticleDefs.Get(PType.Pb);
            Vector3 field = engine.FieldVector;
            Vector3 bPerp = field - Vector3.Project(field, v.normalized);
            Vector3 start = target - new Vector3(D, 0f, 0f);

            if (bPerp.magnitude > 1e-4f)
            {
                // r = m·|v| / (|q|·|B⊥|) ; sagita s = D² / (2r), en sentido opuesto a q·v×B
                float r = info.Mass * v.magnitude / (Mathf.Abs(info.Charge) * bPerp.magnitude);
                float s = D * D / (2f * r);
                Vector3 force = info.Charge * Vector3.Cross(v, field);
                if (force.sqrMagnitude > 1e-8f) start -= force.normalized * s;
            }

            engine.Spawn(PType.Pb, start, v);
            engine.Info("Un antiprotón se dirige hacia un protón en reposo…");
        }

        /// <summary>3) Fotones de alta energía contra 4 núcleos: producción de pares.</summary>
        public void PairProduction()
        {
            engine.Clear();
            EnsureField();
            float[] ys = { -0.6f, -0.2f, 0.2f, 0.6f };
            for (int i = 0; i < ys.Length; i++)
            {
                float y = ys[i];
                engine.Spawn(PType.P, new Vector3(0.2f, y, 0f), Vector3.zero);
                engine.Schedule(i * 0.45f, () =>
                    engine.Spawn(PType.G, new Vector3(-0.95f, y, 0f), Vector3.right, Random.Range(4f, 12f), true));
            }
            engine.Info("Fotones de alta energía contra núcleos: mira las «V» que se abren");
        }

        /// <summary>4) Electrón y positrón con el campo apagado.</summary>
        public void ElectronPositron()
        {
            engine.Clear();
            engine.SetB(0f);
            engine.Spawn(PType.Em, new Vector3(-0.55f, 0f, 0f), new Vector3(0.33f, 0f, 0f));
            engine.Spawn(PType.Ep, new Vector3(0.55f, 0f, 0f), new Vector3(-0.33f, 0f, 0f));
            engine.Info("Campo apagado: el electrón y el positrón se atraen…");
        }
    }
}
