// Forja3D — cámara orbital en tercera persona (ratón con clic derecho o arrastre táctil).
using UnityEngine;

namespace Forja3D
{
    public class Forja3DCamera : MonoBehaviour
    {
        public Transform objetivo;
        public float distancia = 5f;
        public float altura = 1.5f;
        public float sensibilidad = 1f;
        public Vector2 limitesPitch = new Vector2(-10f, 70f);
        float yaw, pitch = 18f;

        void Start() { if (objetivo) yaw = objetivo.eulerAngles.y; }

        void LateUpdate()
        {
            if (!objetivo) return;
            var m = Forja3DInput.Mirar * sensibilidad;
            yaw += m.x;
            pitch = Mathf.Clamp(pitch - m.y, limitesPitch.x, limitesPitch.y);
            distancia = Mathf.Clamp(distancia - Input.mouseScrollDelta.y * 0.5f, 2f, 12f);

            var foco = objetivo.position + Vector3.up * altura;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var deseada = foco - rot * Vector3.forward * distancia;
            if (Physics.Linecast(foco, deseada, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(objetivo))
                deseada = hit.point + hit.normal * 0.2f;
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, deseada, 15f * Time.deltaTime), rot);
        }
    }
}
