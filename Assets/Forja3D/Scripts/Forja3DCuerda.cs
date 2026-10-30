// Forja3D — la Cuerda de Dante (la "corda" de Inf. XVI).
// El modelo enrollado cuelga de la cintura; al lanzarla, el tramo estirado se dibuja por código
// (LineRenderer), así llega a cualquier distancia sin deformarse.
//   · Contra una cabeza de Cerbero: le llena la fauce de tierra (Inf. VI) y la deja saciada unos segundos.
//   · Contra un enemigo: lo atrae hacia Dante y lo deja tambaleándose.
//   · Contra nada: lanza un puñado de tierra delante.
// EXPANSIÓN FUTURA: agarrar y arrastrar objetos (pesos de los avaros, palancas) y el lazo a Gerión.
using System.Collections;
using UnityEngine;

namespace Forja3D
{
    public class Forja3DCuerda : MonoBehaviour
    {
        public Material material;          // lo asigna el constructor de niveles (se incluye en la compilación)
        public float alcance = 11f;
        public float enfriamiento = 1.1f;
        public float costoEstamina = 12f;
        public Transform mano;

        LineRenderer linea;
        float listaEn;
        Forja3DPlayerController jugador;
        Animator anim;

        void Start()
        {
            jugador = GetComponent<Forja3DPlayerController>();
            anim = GetComponentInChildren<Animator>();
            if (!mano && anim && anim.isHuman) mano = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var go = new GameObject("CuerdaLanzada"); go.transform.SetParent(transform, false);
            linea = go.AddComponent<LineRenderer>();
            linea.positionCount = 12; linea.widthMultiplier = 0.045f; linea.numCapVertices = 2;
            linea.useWorldSpace = true; linea.enabled = false;
            linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material) linea.sharedMaterial = material;
        }

        void Update()
        {
            if (Forja3DGameManager.Bloqueado || !jugador) return;
            if (Forja3DInput.Cuerda && Time.time >= listaEn && jugador.Estamina >= costoEstamina) StartCoroutine(Lanzar());
        }

        // Elige el blanco: la cabeza de Cerbero o el enemigo más centrado delante de Dante
        Transform Blanco(out Forja3DCerbero cerbero, out int cabeza, out Forja3DEnemy enemigo)
        {
            cerbero = null; cabeza = -1; enemigo = null;
            Transform mejor = null; float mejorPuntos = float.MaxValue;
            foreach (var e in Forja3DEnemy.Activos)
            {
                if (!e || e.Muerto) continue;
                var c = e.GetComponent<Forja3DCerbero>();
                if (c)
                {
                    for (int i = 0; i < c.Cabezas; i++)
                    {
                        var t = c.Cabeza(i); if (!t) continue;
                        float p = Puntos(t.position); if (p < mejorPuntos) { mejorPuntos = p; mejor = t; cerbero = c; cabeza = i; enemigo = e; }
                    }
                }
                else
                {
                    float p = Puntos(e.transform.position + Vector3.up); if (p < mejorPuntos) { mejorPuntos = p; mejor = e.transform; cerbero = null; cabeza = -1; enemigo = e; }
                }
            }
            return mejorPuntos < float.MaxValue ? mejor : null;
        }

        float Puntos(Vector3 pos)
        {
            var d = pos - transform.position; float dist = d.magnitude;
            if (dist > alcance) return float.MaxValue;
            d.y = 0; float ang = Vector3.Angle(transform.forward, d);
            if (ang > 55f) return float.MaxValue;
            return dist + ang * 0.08f;
        }

        IEnumerator Lanzar()
        {
            listaEn = Time.time + enfriamiento;
            jugador.GastarEstamina(costoEstamina);
            var blanco = Blanco(out var cerbero, out int cabeza, out var enemigo);
            if (blanco)
            {
                var d = blanco.position - transform.position; d.y = 0;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            }
            Forja3DAnim.Trigger(anim, "Attack");
            Forja3DAudio.Sonar("esquiva", transform.position + Vector3.up, 0.6f, 0.05f, 1.3f);
            Vector3 Destino() => blanco ? blanco.position : transform.position + transform.forward * alcance * 0.7f + Vector3.up * 0.5f;
            Vector3 Origen() => mano ? mano.position : transform.position + Vector3.up * 1.3f + transform.right * 0.3f;

            linea.enabled = true;
            // Sale (0,16 s) con una curva que se tensa
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.16f) { Dibujar(Origen(), Vector3.Lerp(Origen(), Destino(), t), 1f - t); yield return null; }
            var punto = Destino();
            Forja3DFX.Polvo(punto, 0.8f);
            Forja3DFX.Chispas(punto, 6);
            if (cerbero && cabeza >= 0)
            {
                cerbero.Saciar(cabeza, 5f);
                Forja3DGameManager.Mensaje("¡TIERRA EN LA FAUCE!");
            }
            else if (enemigo)
            {
                var hacia = transform.position - enemigo.transform.position; hacia.y = 0;
                enemigo.Empujar(hacia.normalized * Mathf.Min(6f, hacia.magnitude * 0.6f), true);
                enemigo.Aturdir(0.6f);
            }
            // Se queda tensa un instante y vuelve
            for (float t = 0; t < 0.12f; t += Time.deltaTime) { Dibujar(Origen(), Destino(), 0f); yield return null; }
            var fin = Destino();
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f) { Dibujar(Origen(), Vector3.Lerp(fin, Origen(), t), t * 0.6f); yield return null; }
            linea.enabled = false;
        }

        // Dibuja la cuerda con un poco de comba (flojedad = 0 → recta)
        void Dibujar(Vector3 a, Vector3 b, float flojedad)
        {
            int n = linea.positionCount; float largo = Vector3.Distance(a, b);
            for (int i = 0; i < n; i++)
            {
                float k = i / (n - 1f);
                var p = Vector3.Lerp(a, b, k) + Vector3.down * Mathf.Sin(k * Mathf.PI) * largo * 0.12f * flojedad;
                linea.SetPosition(i, p);
            }
        }
    }
}
