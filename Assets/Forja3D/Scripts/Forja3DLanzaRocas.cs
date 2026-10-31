// Forja3D — lanzador de rocas de los avaros y pródigos (símbolo "rodante" del nivel).
// Cada componente va en su propio archivo con su mismo nombre: si no, Unity no lo guarda bien en la escena del APK.
using UnityEngine;

namespace Forja3D
{
    // Lanzador de rocas (símbolo "rodante"): cada "periodo" segundos, una roca rueda en su dirección
    public class Forja3DLanzaRocas : MonoBehaviour
    {
        public Vector3 direccion = Vector3.right;
        public float periodo = 5f, radio = 1.1f;
        float proxima;
        void Start() => proxima = Time.time + Random.Range(0.5f, periodo);
        void Update()
        {
            if (Forja3DGameManager.Bloqueado || Time.time < proxima) return;
            proxima = Time.time + periodo * Random.Range(0.85f, 1.15f);
            Forja3DProyectil.Roca(transform.position, direccion, radio);
        }
    }
}
