using UnityEngine;
using Random = UnityEngine.Random;

namespace ParticulasRA
{
    /// <summary>Reacciones (aniquilaciones, producción de pares) y desintegraciones (sección 5).</summary>
    public class ReactionSystem : MonoBehaviour
    {
        SimulationEngine e;

        static readonly Vector3[] Tetra =
        {
            new Vector3(1, 1, 1).normalized, new Vector3(1, -1, -1).normalized,
            new Vector3(-1, 1, -1).normalized, new Vector3(-1, -1, 1).normalized,
        };
        static readonly PType[] Pions = { PType.PiP, PType.PiM, PType.PiP, PType.PiM };

        static readonly Color FlashEE = new Color(1f, 0.96f, 0.76f);
        static readonly Color FlashPP = new Color(1f, 0.82f, 0.96f);
        static readonly Color FlashPair = new Color(1f, 0.95f, 0.48f);

        public void Bind(SimulationEngine engine) => e = engine;

        /// <summary>Llamado por el motor para cada par cercano (d² &lt; 0,01).</summary>
        public void CheckPair(int i, int j, float d2, float dt)
        {
            PType a = e.type[i], b = e.type[j];

            // 5.1 e⁻ + e⁺ → γγ
            if ((a == PType.Em && b == PType.Ep) || (a == PType.Ep && b == PType.Em))
            {
                if (d2 < 0.03f * 0.03f && e.age[i] > 0.25f && e.age[j] > 0.25f) AnnihilateEE(i, j);
                return;
            }

            // 5.2 p + p̄ → 2π⁺ + 2π⁻ + 2γ
            if ((a == PType.P && b == PType.Pb) || (a == PType.Pb && b == PType.P))
            {
                if (d2 < 0.06f * 0.06f) AnnihilatePP(i, j);
                return;
            }

            // 5.3 γ cerca de un nucleón → e⁻ + e⁺
            if (a == PType.G || b == PType.G)
            {
                int g = a == PType.G ? i : j;
                int o = g == i ? j : i;
                if (!ParticleDefs.IsNucleon(e.type[o]) || e.energy[g] <= 1.022f) return;
                bool go = e.forcePair[g] ? d2 < 0.07f * 0.07f : Random.value < 9f * dt;
                if (go) PairProduce(g);
            }
        }

        void TwoPhotons(Vector3 at, float energy)
        {
            Vector3 d = Random.onUnitSphere;
            e.Spawn(PType.G, at, d, energy);
            e.Spawn(PType.G, at, -d, energy);
        }

        void AnnihilateEE(int i, int j)
        {
            e.dead[i] = e.dead[j] = true;
            Vector3 m = (e.pos[i] + e.pos[j]) * 0.5f;
            TwoPhotons(m, 0.511f);
            e.Flash(m, FlashEE, 0.15f);
            e.Log("e⁻ + e⁺ → γ + γ  (aniquilación)");
        }

        /// <summary>4.5: un positrón frenado se aniquila con un electrón atómico del medio.</summary>
        public void PositronStopped(int i)
        {
            e.dead[i] = true;
            TwoPhotons(e.pos[i], 0.511f);
            e.Flash(e.pos[i], FlashEE, 0.12f);
            e.Log("e⁺ frenado + e⁻ del medio → γ + γ");
        }

        void AnnihilatePP(int i, int j)
        {
            e.dead[i] = e.dead[j] = true;
            Vector3 m = (e.pos[i] + e.pos[j]) * 0.5f;
            Vector3 vcm = (e.vel[i] + e.vel[j]) * 0.5f;     // velocidad del centro de masas
            Quaternion rot = Random.rotationUniform;

            for (int k = 0; k < 4; k++)
            {
                // Direcciones tetraédricas (momento total ≈ 0) con ±15° de ruido
                Vector3 dir = (rot * Tetra[k] + Random.insideUnitSphere * 0.26f).normalized;
                float sp = Random.Range(0.66f, 1.08f);
                e.Spawn(Pions[k], m, SimulationEngine.Cap(vcm + dir * sp, 0.97f));
            }
            TwoPhotons(m, 67f); // π⁰ → γγ
            e.Flash(m, FlashPP, 0.3f);
            e.Log("p + p̄ → 2π⁺ + 2π⁻ + π⁰(→γγ)");
        }

        void PairProduce(int g)
        {
            e.dead[g] = true;
            Vector3 dir = e.vel[g].normalized;

            // Eje perpendicular aleatorio: abrir el par en un plano que contiene la dirección del fotón
            Vector3 perp = Vector3.Cross(dir, Random.onUnitSphere);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();

            float ang = Random.Range(0.16f, 0.26f) * Mathf.Rad2Deg;
            Vector3 dA = Quaternion.AngleAxis(ang, perp) * dir;
            Vector3 dB = Quaternion.AngleAxis(-ang, perp) * dir;

            float E = e.energy[g];
            float sp = Mathf.Min(0.95f * ParticleDefs.C, 0.42f + 0.135f * (E - 1.022f));
            Vector3 at = e.pos[g];
            e.Spawn(PType.Em, at, dA * sp);
            e.Spawn(PType.Ep, at, dB * sp);
            e.Flash(at, FlashPair, 0.09f);
            e.Log($"γ ({ParticleDefs.Num(E)} MeV) → e⁻ + e⁺  (producción de pares)");
        }

        /// <summary>5.4: llamado cuando la edad supera la vida. Los neutrinos solo aparecen en el texto.</summary>
        public void Decay(int i)
        {
            switch (e.type[i])
            {
                case PType.PiP: Kick(i, PType.MuP, 0.36f); e.Log("π⁺ → μ⁺ + ν_μ"); break;
                case PType.PiM: Kick(i, PType.MuM, 0.36f); e.Log("π⁻ → μ⁻ + ν̄_μ"); break;
                case PType.MuM: Kick(i, PType.Em, 0.78f); e.Log("μ⁻ → e⁻ + ν̄ₑ + ν_μ"); break;
                case PType.MuP: Kick(i, PType.Ep, 0.78f); e.Log("μ⁺ → e⁺ + νₑ + ν̄_μ"); break;
                case PType.N:
                    Kick(i, PType.P, 0.09f);
                    Kick(i, PType.Em, 0.84f);
                    e.Log("n → p + e⁻ + ν̄ₑ  (desintegración β)");
                    break;
                default: return;
            }
            e.Flash(e.pos[i], ParticleDefs.Get(e.type[i]).Color, 0.05f);
        }

        void Kick(int i, PType daughter, float k)
        {
            Vector3 v = SimulationEngine.Cap(e.vel[i] + Random.onUnitSphere * k, 0.97f);
            e.Spawn(daughter, e.pos[i], v);
        }
    }
}
