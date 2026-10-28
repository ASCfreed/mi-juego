// Forja3D — personaje con diálogo (Virgilio, Francesca…): habla cuando Dante se acerca.
// Con "seguir" activado acompaña al jugador (como Virgilio a Dante).
using UnityEngine;
using UnityEngine.AI;

namespace Forja3D
{
    public class Forja3DNPC : MonoBehaviour
    {
        public string nombre = "";
        public Linea[] dialogo;
        public bool repetir = false;
        public bool seguir = false;
        public float distancia = 3f;

        Transform jugador;
        Animator anim;
        NavMeshAgent agente;
        bool hablado;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p) jugador = p.transform;
            anim = GetComponentInChildren<Animator>();
            if (anim) anim.applyRootMotion = false;
            agente = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (!jugador) return;
            var d = jugador.position - transform.position; d.y = 0;
            float dist = d.magnitude;

            if (seguir && agente && agente.isOnNavMesh)
            {
                if (dist > 3.5f && !Forja3DDialogue.Activo) agente.SetDestination(jugador.position - d.normalized * 2.2f);
                else if (dist < 2.5f) agente.ResetPath();
                Forja3DAnim.Float(anim, "Speed", agente.velocity.magnitude);
            }

            // Mira a Dante cuando está cerca
            if (dist < distancia * 2f && d.sqrMagnitude > 0.01f && (!agente || agente.velocity.sqrMagnitude < 0.1f))
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 4f * Time.deltaTime);

            if (!hablado && dist <= distancia && !Forja3DDialogue.Activo && !Forja3DGameManager.Terminado && dialogo != null && dialogo.Length > 0)
            {
                hablado = !repetir;
                Forja3DAnim.Trigger(anim, "Alert");
                Forja3DDialogue.Mostrar(dialogo);
            }
            if (repetir && dist > distancia * 1.5f) hablado = false;
        }
    }
}
