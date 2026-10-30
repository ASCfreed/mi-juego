// Forja3D — Pluto, el lobo maldito (Inf. VII). Comportamiento "pluto" del nivel:
//   · fase 1: SE HINCHA de oro (más grande, los golpes débiles rebotan) y escupe abanicos de monedas.
//     Las monedas se bloquean, y con un parry se le devuelven.
//   · fase 2: SE DESINFLA tras cada carga: queda aturdido y recibe el doble de daño.
//   · remate "Vela rota": al caer, se derrumba "como las velas hinchadas por el viento" y saltan sus cadenas de oro.
// Furioso (mitad de vida) se hincha más a menudo.
// EXPANSIÓN FUTURA: animación propia de hincharse (hoy se escala el modelo) y cadenas de oro que se cortan con la hoz.
using UnityEngine;

namespace Forja3D
{
    public class Forja3DPluto : MonoBehaviour
    {
        enum Fase { Normal, Hinchado, Desinflado }
        Fase fase = Fase.Normal;
        float hasta, proximaRafaga, avisoRebote;
        int rafagas;
        Vector3 escalaBase;
        Forja3DEnemy enemigo; Forja3DHealth vida; Transform jugador;
        bool gritado, caido;

        void Awake()
        {
            enemigo = GetComponent<Forja3DEnemy>(); vida = GetComponent<Forja3DHealth>();
            escalaBase = transform.localScale;
            hasta = Time.time + 5f;
            if (enemigo) enemigo.PuedeAtacar = () => fase == Fase.Normal;
            if (vida)
            {
                vida.AlRebotar += () => { if (Time.time > avisoRebote) { avisoRebote = Time.time + 2.5f; Forja3DGameManager.Mensaje("SU ORO LO PROTEGE: ESPERA A QUE SE DESINFLE"); Forja3DFX.Chispas(transform.position + Vector3.up * 1.5f * escalaBase.y, 10); } };
                vida.AlMorir += VelaRota;
            }
        }

        void Start() { var p = GameObject.FindWithTag("Player"); if (p) jugador = p.transform; }

        void Update()
        {
            if (!vida || vida.Muerto || !jugador || Forja3DGameManager.Bloqueado) return;
            float d = Vector3.Distance(jugador.position, transform.position);
            if (!gritado && d < 16f)
            {
                gritado = true;
                Forja3DGameManager.Mensaje("¡PAPE SATÀN, PAPE SATÀN ALEPPE!");
                Forja3DAudio.Sonar("voz_jefe", transform.position, 1f, 0.2f, 0.75f);
                Forja3DFX.Temblor(0.25f, 0.6f);
            }
            if (!gritado) return;

            float objetivoEscala = fase == Fase.Hinchado ? 1.22f : fase == Fase.Desinflado ? 0.9f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, escalaBase * objetivoEscala, 4f * Time.deltaTime);

            switch (fase)
            {
                case Fase.Normal:
                    vida.multiplicadorDanio = 1f;
                    if (Time.time >= hasta) Hincharse();
                    break;
                case Fase.Hinchado:
                    vida.multiplicadorDanio = 0.34f; // solo los golpes fuertes (gracia, tercer golpe) atraviesan el oro
                    if (Time.time >= proximaRafaga && rafagas > 0) { rafagas--; proximaRafaga = Time.time + 1.1f; Escupir(); }
                    if (rafagas <= 0 && Time.time >= proximaRafaga) Desinflarse();
                    break;
                case Fase.Desinflado:
                    vida.multiplicadorDanio = 2f;
                    if (Time.time >= hasta) { fase = Fase.Normal; hasta = Time.time + (enemigo && enemigo.Furioso ? 4f : 6.5f); }
                    break;
            }
        }

        void Hincharse()
        {
            fase = Fase.Hinchado; rafagas = enemigo && enemigo.Furioso ? 4 : 3; proximaRafaga = Time.time + 0.9f;
            Forja3DAudio.Sonar("voz_jefe", transform.position, 0.9f, 0.15f, 0.6f);
            Forja3DGameManager.Mensaje("¡PLUTO SE HINCHA DE ORO!");
        }

        void Escupir()
        {
            var boca = transform.position + Vector3.up * 1.6f * escalaBase.y + transform.forward * 0.8f * escalaBase.z;
            var haciaJ = jugador.position - transform.position; haciaJ.y = 0; haciaJ.Normalize();
            transform.rotation = Quaternion.LookRotation(haciaJ);
            int n = enemigo && enemigo.Furioso ? 9 : 7;
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.Lerp(-40f, 40f, n == 1 ? 0.5f : i / (float)(n - 1)) + Random.Range(-4f, 4f);
                var m = Forja3DProyectil.Moneda(boca, Quaternion.Euler(0, ang, 0) * haciaJ, transform);
                // bajan hacia la altura de Dante mientras vuelan
                m.transform.position = new Vector3(boca.x, jugador.position.y + 1.1f, boca.z);
            }
            Forja3DFX.Chispas(boca, 18);
            Forja3DAudio.Sonar("bloqueo", boca, 0.6f, 0.2f, 1.6f);
        }

        void Desinflarse()
        {
            fase = Fase.Desinflado; hasta = Time.time + 3.5f;
            if (enemigo) enemigo.Aturdir(3.2f);
            Forja3DFX.Polvo(transform.position, 2.5f * escalaBase.x);
            Forja3DAudio.Sonar("voz_jefe", transform.position, 0.7f, 0.1f, 0.45f);
            Forja3DGameManager.Mensaje("¡SE DESINFLA! ¡AHORA, DANTE!");
        }

        // Remate del guion: "Vela rota" (Inf. VII, 13-14)
        void VelaRota()
        {
            if (caido) return; caido = true;
            transform.localScale = escalaBase;
            var p = transform.position + Vector3.up * 1.4f * escalaBase.y;
            for (int i = 0; i < 4; i++) Forja3DFX.Chispas(p + Random.insideUnitSphere * escalaBase.x, 30);
            Forja3DFX.Polvo(transform.position, 3f * escalaBase.x);
            Forja3DFX.Temblor(0.5f, 0.8f);
            Forja3DGameManager.Mensaje("VELA ROTA: CAEN SUS CADENAS DE ORO");
            Forja3DProgreso.Ganar(40, "vela rota");
        }
    }
}
