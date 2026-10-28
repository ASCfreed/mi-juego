// Forja3D — controlador en tercera persona (PC y táctil).
// PC: WASD mover · Shift correr · Espacio saltar · Clic izq. atacar · Clic der. + ratón girar cámara
// Android: joystick, botones SALTAR/ATACAR y arrastrar a la derecha para girar la cámara.
using UnityEngine;

// EXPANSIÓN FUTURA: los controles vienen de Forja3DInput (táctil hoy; mando/teclado para PC después).
// Los combos, esquivar y bloquear del guion se añadirían aquí como nuevos estados y triggers del Animator.
namespace Forja3D
{
    [RequireComponent(typeof(CharacterController))]
    public class Forja3DPlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        public float velocidadCaminar = 2.2f;
        public float velocidadCorrer = 5.5f;
        public float giro = 12f;
        public float fuerzaSalto = 5f;
        public float gravedad = -20f;

        [Header("Combate")]
        public float alcanceAtaque = 1.4f;
        public int danioAtaque = 1;
        public float enfriamientoAtaque = 0.5f;

        [Header("Cámara")]
        public bool crearCamaraAutomatica = true;

        CharacterController cc;
        Animator anim;
        Forja3DHealth vida;
        Transform cam;
        float vy, velAnim, ultimoAtaque = -9f;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            anim = GetComponentInChildren<Animator>();
            vida = GetComponent<Forja3DHealth>();
            if (anim) anim.applyRootMotion = false;
        }

        void Start()
        {
            cam = Camera.main ? Camera.main.transform : null;
            if (!crearCamaraAutomatica) return;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); cam = go.transform;
            }
            var seguir = cam.GetComponent<Forja3DCamera>();
            if (!seguir) seguir = cam.gameObject.AddComponent<Forja3DCamera>();
            if (!seguir.objetivo) seguir.objetivo = transform;
        }

        void Update()
        {
            bool muerto = vida && vida.Muerto;
            bool bloqueado = muerto || Forja3DGameManager.Bloqueado;

            var entrada = bloqueado ? Vector2.zero : Forja3DInput.Mover;
            Vector3 dir = new Vector3(entrada.x, 0, entrada.y);
            if (cam)
            {
                var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                dir = f * entrada.y + r * entrada.x;
            }
            float vel = entrada.magnitude * (Forja3DInput.Correr ? velocidadCorrer : velocidadCaminar);
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), giro * Time.deltaTime);

            if (cc.isGrounded && vy < 0) vy = -2f;
            if (!bloqueado && cc.isGrounded && Forja3DInput.Saltar) { vy = fuerzaSalto; Forja3DAnim.Trigger(anim, "Jump"); }
            vy += gravedad * Time.deltaTime;
            cc.Move((dir.normalized * vel + Vector3.up * vy) * Time.deltaTime);

            if (!bloqueado && Forja3DInput.Atacar && Time.time - ultimoAtaque > enfriamientoAtaque)
            {
                ultimoAtaque = Time.time;
                Forja3DAnim.Trigger(anim, "Attack");
                Invoke(nameof(Golpear), 0.25f);
            }

            velAnim = Mathf.Lerp(velAnim, vel, 10f * Time.deltaTime);
            Forja3DAnim.Float(anim, "Speed", velAnim);
            Forja3DAnim.Bool(anim, "Grounded", cc.isGrounded);

            if (transform.position.y < -30f && vida) vida.Danio(999); // cayó al vacío
        }

        void Golpear()
        {
            var centro = transform.position + transform.forward * (alcanceAtaque * 0.7f) + Vector3.up * 1f;
            foreach (var c in Physics.OverlapSphere(centro, alcanceAtaque, ~0, QueryTriggerInteraction.Ignore))
            {
                var h = c.GetComponentInParent<Forja3DHealth>();
                if (h && !h.esJugador) h.Danio(danioAtaque);
            }
        }
    }
}
