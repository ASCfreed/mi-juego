// Forja3D — IA simple de enemigo: patrulla, persigue al jugador y ataca.
using UnityEngine;
using UnityEngine.AI;

namespace Forja3D
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Forja3DHealth))]
    public class Forja3DEnemy : MonoBehaviour
    {
        public float radioDeteccion = 9f;
        public float radioAtaque = 1.6f;
        public float radioPatrulla = 4f;
        public float enfriamiento = 1.4f;
        public int danio = 1;
        public float velocidad = 2.8f;

        NavMeshAgent agente;
        Animator anim;
        Forja3DHealth vida;
        Transform jugador;
        Vector3 origen;
        float ultimoAtaque = -9f, proximaPatrulla;

        void Awake()
        {
            agente = GetComponent<NavMeshAgent>();
            anim = GetComponentInChildren<Animator>();
            vida = GetComponent<Forja3DHealth>();
            agente.speed = velocidad;
            agente.stoppingDistance = radioAtaque * 0.8f;
            if (anim) anim.applyRootMotion = false;
            vida.AlMorir += Morir;
        }

        void Start()
        {
            origen = transform.position;
            var p = GameObject.FindWithTag("Player");
            if (p) jugador = p.transform;
            Forja3DGameManager.RegistrarEnemigo();
        }

        void Update()
        {
            if (vida.Muerto || !agente.isOnNavMesh) return;
            Forja3DAnim.Float(anim, "Speed", agente.velocity.magnitude);

            var hJugador = jugador ? jugador.GetComponent<Forja3DHealth>() : null;
            bool jugadorVivo = jugador && (!hJugador || !hJugador.Muerto) && !Forja3DGameManager.Terminado;
            float d = jugadorVivo ? Vector3.Distance(transform.position, jugador.position) : Mathf.Infinity;

            if (d <= radioAtaque)
            {
                agente.ResetPath();
                var mirar = jugador.position - transform.position; mirar.y = 0;
                if (mirar.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mirar), 10f * Time.deltaTime);
                if (Time.time - ultimoAtaque > enfriamiento)
                {
                    ultimoAtaque = Time.time;
                    Forja3DAnim.Trigger(anim, "Attack");
                    Invoke(nameof(Golpear), 0.45f);
                }
            }
            else if (d <= radioDeteccion) agente.SetDestination(jugador.position);
            else if (Time.time > proximaPatrulla)
            {
                proximaPatrulla = Time.time + Random.Range(2f, 5f);
                var r = origen + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * radioPatrulla;
                if (NavMesh.SamplePosition(r, out var hit, 2f, NavMesh.AllAreas)) agente.SetDestination(hit.position);
            }
        }

        void Golpear()
        {
            if (vida.Muerto || !jugador) return;
            if (Vector3.Distance(transform.position, jugador.position) <= radioAtaque * 1.25f)
            {
                var h = jugador.GetComponent<Forja3DHealth>();
                if (h) h.Danio(danio);
            }
        }

        void Morir()
        {
            CancelInvoke();
            if (agente.isOnNavMesh) agente.ResetPath();
            agente.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Forja3DGameManager.EnemigoDerrotado();
            if (!anim || !anim.runtimeAnimatorController) Destroy(gameObject, 0.2f);
            else Destroy(gameObject, 4f);
        }
    }
}
