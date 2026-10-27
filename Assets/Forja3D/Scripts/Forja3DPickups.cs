// Forja3D — monedas y meta del nivel.
using UnityEngine;

namespace Forja3D
{
    public class Forja3DCollectible : MonoBehaviour
    {
        void Start() => Forja3DGameManager.RegistrarMoneda();
        void Update() => transform.Rotate(0, 120f * Time.deltaTime, 0, Space.World);
        void OnTriggerEnter(Collider c)
        {
            if (!c.CompareTag("Player")) return;
            Forja3DGameManager.MonedaRecogida();
            Destroy(gameObject);
        }
    }

    public class Forja3DGoal : MonoBehaviour
    {
        void Update() => transform.Rotate(0, 45f * Time.deltaTime, 0, Space.World);
        void OnTriggerEnter(Collider c) { if (c.CompareTag("Player")) Forja3DGameManager.MetaAlcanzada(); }
    }
}
