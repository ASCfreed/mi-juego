// Forja3D — personaje que aparece al caer el último jefe (Filippo Argenti trepa a la barca tras Flegias).
using UnityEngine;

namespace Forja3D
{
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
