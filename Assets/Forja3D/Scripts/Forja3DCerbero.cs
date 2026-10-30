// Forja3D — Cerbero (Inf. VI): un mastín de una cabeza al que el constructor de niveles le añade dos
// cabezas más, copiadas de la suya. Aquí cada cabeza se mueve por su cuenta:
//   · respira y se balancea con un ritmo distinto;
//   · muerde por turnos (una cabeza distinta en cada ataque);
//   · con tierra de la Cuerda queda SACIADA: baja al suelo y mastica unos segundos sin morder.
// Reglas del combate: si las tres están saciadas no ataca; con dos saciadas la tercera queda expuesta y
// recibe el doble de daño; furioso (mitad de vida) y con menos de dos saciadas, devora almas y se cura.
// EXPANSIÓN FUTURA: el remate del guion (atar las tres cabezas con la Cuerda y estrellarlas).
using System.Collections.Generic;
using UnityEngine;

namespace Forja3D
{
    [DefaultExecutionOrder(1001)] // después del Animator y del estabilizador de Tripo
    public class Forja3DCerbero : MonoBehaviour
    {
        class CabezaInfo { public Transform t; public Quaternion baseLocal; public float fase, saciadaHasta, mordiscoHasta; public bool hueso; }
        readonly List<CabezaInfo> cabezas = new List<CabezaInfo>();
        Forja3DEnemy enemigo; Forja3DHealth vida;
        int turno; float proximaCura; bool avisado;

        public int Cabezas => cabezas.Count;
        public Transform Cabeza(int i) => i >= 0 && i < cabezas.Count ? cabezas[i].t : null;
        int Saciadas { get { int n = 0; foreach (var c in cabezas) if (Time.time < c.saciadaHasta) n++; return n; } }

        void Awake()
        {
            enemigo = GetComponent<Forja3DEnemy>(); vida = GetComponent<Forja3DHealth>();
            // La cabeza original (hueso) y las copias que dejó el constructor
            Transform hueso = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("Head_0") || (hueso == null && t.name.ToLowerInvariant().Contains("head"))) { hueso = t; if (t.name.Contains("Head_0")) break; }
            if (hueso) cabezas.Add(new CabezaInfo { t = hueso, hueso = true });
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("CabezaCopia")) cabezas.Add(new CabezaInfo { t = t, baseLocal = t.localRotation });
            for (int i = 0; i < cabezas.Count; i++) cabezas[i].fase = i * 2.1f;
            if (enemigo)
            {
                enemigo.AlAtacar += Morder;
                enemigo.PuedeAtacar = () => Saciadas < cabezas.Count;
            }
        }

        public void Saciar(int i, float segundos)
        {
            if (i < 0 || i >= cabezas.Count) return;
            cabezas[i].saciadaHasta = Time.time + segundos;
            Forja3DAudio.Sonar("voz_fiera", cabezas[i].t.position, 0.5f, 0.1f, 0.7f);
            if (Saciadas >= 2) Forja3DGameManager.Mensaje("¡LA TERCERA CABEZA ESTÁ EXPUESTA!");
        }

        void Morder()
        {
            // Turno de la siguiente cabeza libre
            for (int k = 0; k < cabezas.Count; k++)
            {
                var c = cabezas[(turno + k) % cabezas.Count];
                if (Time.time < c.saciadaHasta) continue;
                c.mordiscoHasta = Time.time + 0.45f; turno = (turno + k + 1) % cabezas.Count;
                return;
            }
        }

        void Update()
        {
            if (!vida || vida.Muerto) return;
            int s = Saciadas;
            vida.multiplicadorDanio = s >= 2 ? 2f : 1f;
            // Furioso y con hambre: devora almas del suelo y se cura
            if (enemigo && enemigo.Furioso && s < 2 && Time.time >= proximaCura && vida.Vida < vida.vidaMaxima)
            {
                proximaCura = Time.time + 3f;
                vida.Curar(1);
                Forja3DFX.Polvo(transform.position + transform.forward * 1.5f, 1.2f);
                if (!avisado) { avisado = true; Forja3DGameManager.Mensaje("¡CERBERO DEVORA ALMAS! LLÉNALE LAS FAUCES DE TIERRA"); }
            }
        }

        void LateUpdate()
        {
            if (vida && vida.Muerto) return;
            // Giros en ejes del cuerpo (derecha = asentir, arriba = girar), así no dependen de los ejes de cada hueso
            var der = transform.right; var arriba = transform.up;
            foreach (var c in cabezas)
            {
                if (!c.t) continue;
                float t = Time.time + c.fase;
                float cabeceo, giro;
                if (Time.time < c.saciadaHasta) { cabeceo = 32f + Mathf.Sin(t * 9f) * 6f; giro = 0f; }          // agachada, masticando
                else if (Time.time < c.mordiscoHasta) { float k = 1f - (c.mordiscoHasta - Time.time) / 0.45f; cabeceo = 22f * Mathf.Sin(k * Mathf.PI); giro = 0f; } // muerde
                else { cabeceo = Mathf.Sin(t * 1.3f) * 5f; giro = Mathf.Sin(t * 0.8f) * 9f; }                     // respira y mira
                var extra = Quaternion.AngleAxis(cabeceo, der) * Quaternion.AngleAxis(giro, arriba);
                if (c.hueso) c.t.rotation = extra * c.t.rotation;                         // sobre la pose del Animator
                else c.t.rotation = extra * (c.t.parent ? c.t.parent.rotation * c.baseLocal : c.baseLocal);
            }
        }
    }
}
