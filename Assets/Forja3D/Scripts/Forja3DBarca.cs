// Forja3D — la barca de Flegias (Inf. VIII): una decoración "pisable" escalada como cubierta.
// Se mece sobre la Estigia mientras se pelea encima. Cuando Dante termina de escuchar a las almas clave
// (Filippo Argenti), la barca encalla contra la orilla de Dite: sacudida, polvo y la salida queda abierta.
// EXPANSIÓN FUTURA: avanzar de verdad por el agua (mover el nivel bajo la barca) y almas que trepan por la borda.
using UnityEngine;

namespace Forja3D
{
    public class Forja3DBarca : MonoBehaviour
    {
        Transform casco; Vector3 basePos; Quaternion baseRot;
        float choque = -1f;

        void Awake()
        {
            casco = transform.childCount > 0 ? transform.GetChild(0) : transform;
            basePos = casco.localPosition; baseRot = casco.localRotation;
            Forja3DGameManager.AlOirClaves += Chocar;
        }
        void OnDestroy() => Forja3DGameManager.AlOirClaves -= Chocar;

        void Update()
        {
            float t = Time.time;
            // Mecerse: cabeceo y balanceo suaves (la cubierta real es el suelo; solo se mueve el casco visible)
            float balanceo = Mathf.Sin(t * 0.7f) * 1.1f, cabeceo = Mathf.Sin(t * 0.45f + 1f) * 0.6f, alto = Mathf.Sin(t * 0.9f) * 0.06f;
            if (choque > 0f)
            {
                float k = Time.time - choque;
                if (k < 2.5f) { float a = Mathf.Exp(-k * 2f) * Mathf.Sin(k * 14f); cabeceo += a * 5f; balanceo += a * 2.5f; }
                else { balanceo *= 0.15f; cabeceo *= 0.15f; alto *= 0.15f; } // encallada: casi quieta
            }
            casco.localPosition = basePos + Vector3.up * alto;
            casco.localRotation = Quaternion.Euler(cabeceo, 0, balanceo) * baseRot;
        }

        void Chocar()
        {
            if (choque > 0f) return;
            choque = Time.time;
            Forja3DFX.Temblor(0.7f, 1.2f);
            Forja3DFX.HitStop(0.12f, 0.1f);
            var proa = transform.position + transform.right * 20f;
            Forja3DFX.Polvo(proa, 4f); Forja3DFX.Polvo(transform.position, 3f);
            Forja3DAudio.Sonar("guardia_rota", transform.position, 1f, 0.05f, 0.5f);
            Forja3DGameManager.Mensaje("¡LA BARCA ENCALLA EN LA ORILLA DE DITE!");
            Debug.Log("[Forja3D] La barca encalla: salida abierta");
        }
    }

    // Personaje que aparece cuando cae el último jefe del nivel (Filippo Argenti trepa a la barca)
    public class Forja3DTrasJefe : MonoBehaviour
    {
        public Forja3DNPC personaje;
        void Start()
        {
            // Cuenta ya como alma clave (así la salida no se abre antes de hablar con él)
            if (personaje && personaje.clave) { Forja3DGameManager.RegistrarClave(); personaje.claveRegistrada = true; }
            Forja3DGameManager.AlCaerJefes += Aparecer;
        }
        void OnDestroy() => Forja3DGameManager.AlCaerJefes -= Aparecer;

        void Aparecer()
        {
            if (!personaje || personaje.gameObject.activeSelf) return;
            StartCoroutine(Trepar());
        }
        System.Collections.IEnumerator Trepar()
        {
            yield return new WaitForSeconds(2.5f); // después del golpe final y del aviso
            var g = personaje.gameObject;
            g.SetActive(true);
            Forja3DFX.Polvo(g.transform.position, 1.5f);
            Forja3DAudio.Sonar("voz_condenado", g.transform.position, 0.8f, 0.1f, 0.85f);
            Forja3DGameManager.Mensaje("UN ALMA ENFANGADA TREPA A LA BARCA");
            // Sube desde el agua
            var fin = g.transform.position; var ini = fin - Vector3.up * 1.6f;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.8f) { g.transform.position = Vector3.Lerp(ini, fin, t); yield return null; }
            g.transform.position = fin;
        }
    }
}
