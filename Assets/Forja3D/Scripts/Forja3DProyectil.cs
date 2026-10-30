// Forja3D — proyectiles que se pueden bloquear y devolver con un parry:
// las monedas que escupe Pluto y las rocas que empujan los avaros y pródigos (Inf. VII).
// Un parry en el momento justo invierte su rumbo; devuelto, daña a los enemigos (y a Pluto) en lugar de a Dante.
// EXPANSIÓN FUTURA: en PC el mismo script sirve con ratón (bloquear = clic derecho); no depende de la plataforma.
using UnityEngine;

namespace Forja3D
{
    public class Forja3DProyectil : MonoBehaviour
    {
        public Vector3 direccion = Vector3.forward;
        public float velocidad = 9f;
        public int danio = 1;
        public float radio = 0.25f;
        public float vida = 4f;
        public bool rueda;            // roca: gira sobre sí misma y se rompe contra los muros
        public int danioDevuelto = 2; // daño a los enemigos si Dante la devuelve
        public Transform lanzador;       // quién la lanzó (no se golpea a sí mismo al salir)

        bool devuelta; float nace; Transform jugador; Forja3DPlayerController control;

        void Start()
        {
            nace = Time.time;
            var p = GameObject.FindWithTag("Player");
            if (p) { jugador = p.transform; control = p.GetComponent<Forja3DPlayerController>(); }
            direccion.y = 0; direccion = direccion.sqrMagnitude > 0.001f ? direccion.normalized : Vector3.forward;
        }

        void Update()
        {
            if (Forja3DGameManager.Bloqueado) return;
            if (Time.time - nace > vida) { Romper(); return; }
            float paso = velocidad * Time.deltaTime;
            // Muros: la roca se parte; la moneda se pierde
            int mascara = ~LayerMask.GetMask("Ignore Raycast");
            var centro = transform.position;
            if (Physics.SphereCast(centro, radio * 0.6f, direccion, out var hit, paso + radio * 0.4f, mascara, QueryTriggerInteraction.Ignore)
                && hit.normal.y < 0.5f // el suelo y las cuestas no la paran
                && !hit.collider.GetComponentInParent<Forja3DHealth>() && !hit.collider.CompareTag("Player"))
            { Romper(); return; }

            transform.position += direccion * paso;
            // La roca sigue el relieve del terreno
            if (rueda && Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down, out var suelo, 8f, mascara, QueryTriggerInteraction.Ignore)
                && !suelo.collider.GetComponentInParent<Forja3DHealth>())
                transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, suelo.point.y + radio, 12f * Time.deltaTime), transform.position.z);
            if (rueda) transform.Rotate(Vector3.Cross(Vector3.up, direccion), paso / Mathf.Max(0.1f, radio) * Mathf.Rad2Deg, Space.World);

            if (!devuelta)
            {
                if (jugador && Vector3.Distance(Plano(transform.position), Plano(jugador.position)) < radio + 0.45f
                    && Mathf.Abs(transform.position.y - (jugador.position.y + 1f)) < radio + 1.2f)
                {
                    var h = jugador.GetComponent<Forja3DHealth>();
                    float antes = control ? control.UltimoParry : -1f;
                    if (h) h.Danio(danio, transform);
                    if (control && control.UltimoParry > antes && control.UltimoParry >= Time.time - 0.01f) Devolver();
                    else Romper();
                }
            }
            else
            {
                foreach (var e in Forja3DEnemy.Activos)
                {
                    if (!e || e.transform == lanzador && Time.time - nace < 0.15f) continue;
                    var alto = e.transform.lossyScale.y;
                    if (Vector3.Distance(Plano(transform.position), Plano(e.transform.position)) < radio + 0.6f * Mathf.Max(1f, alto))
                    {
                        var h = e.GetComponent<Forja3DHealth>();
                        if (h && h.Danio(danioDevuelto, jugador)) { e.Aturdir(rueda ? 1.4f : 0.6f); Forja3DFX.Sangre(transform.position, direccion, 20); }
                        Romper(); return;
                    }
                }
            }
        }

        void Devolver()
        {
            devuelta = true; nace = Time.time; velocidad *= 1.35f;
            // Vuelve hacia quien la lanzó, o hacia el enemigo más cercano delante de Dante
            Vector3 destino = lanzador ? lanzador.position : transform.position - direccion * 10f;
            if (!lanzador)
            {
                float mejor = 20f;
                foreach (var e in Forja3DEnemy.Activos)
                {
                    if (!e) continue;
                    float d = Vector3.Distance(e.transform.position, transform.position);
                    if (d < mejor && Vector3.Dot(e.transform.position - transform.position, -direccion) > 0) { mejor = d; destino = e.transform.position; }
                }
            }
            direccion = Plano(destino - transform.position).normalized;
            Forja3DGameManager.Mensaje(rueda ? "¡ROCA DEVUELTA!" : "¡DEVUELTA!");
            Forja3DProgreso.Ganar(rueda ? 8 : 3, "devolver");
        }

        void Romper()
        {
            if (rueda) { Forja3DFX.Polvo(transform.position, radio * 1.4f); Forja3DAudio.Sonar("bloqueo", transform.position, 0.8f, 0.1f, 0.6f); }
            else Forja3DFX.Chispas(transform.position, 6);
            Destroy(gameObject);
        }

        static Vector3 Plano(Vector3 v) { v.y = 0; return v; }

        // ── Fábricas ──
        public static Forja3DProyectil Moneda(Vector3 desde, Vector3 dir, Transform lanzador)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Moneda"; g.transform.position = desde; g.transform.localScale = new Vector3(0.32f, 0.03f, 0.32f);
            g.transform.rotation = Quaternion.Euler(90, Random.Range(0, 360), 0);
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = Oro(r.sharedMaterial); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = g.AddComponent<Forja3DProyectil>();
            p.direccion = dir; p.velocidad = 10f; p.danio = 1; p.radio = 0.25f; p.vida = 3f; p.lanzador = lanzador; p.danioDevuelto = 1;
            return p;
        }

        static bool primeraRoca = true;
        public static Forja3DProyectil Roca(Vector3 desde, Vector3 dir, float radio)
        {
            if (primeraRoca) { primeraRoca = false; Debug.Log("[Forja3D] Primera roca rodante"); }
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "RocaRodante"; g.transform.position = desde + Vector3.up * radio; g.transform.localScale = Vector3.one * radio * 2f;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = Piedra(r.sharedMaterial);
            var p = g.AddComponent<Forja3DProyectil>();
            p.direccion = dir; p.velocidad = 4.2f; p.danio = 2; p.radio = radio; p.vida = 14f; p.rueda = true; p.danioDevuelto = 4;
            return p;
        }

        static Material oro, piedra;
        // Se parte del material por defecto del primitivo (Standard): no depende de Shader.Find en el APK
        static Material Oro(Material base_)
        {
            if (oro) return oro;
            oro = new Material(base_) { color = new Color(1f, 0.78f, 0.25f) };
            oro.SetFloat("_Metallic", 0.9f); oro.SetFloat("_Glossiness", 0.75f);
            oro.EnableKeyword("_EMISSION"); oro.SetColor("_EmissionColor", new Color(0.5f, 0.32f, 0.05f));
            return oro;
        }
        // Peso de los avaros: piedra gris con vetas de oro (textura de ruido generada al vuelo)
        static Material Piedra(Material base_)
        {
            if (piedra) return piedra;
            var t = new Texture2D(128, 128); var px = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f), v = Mathf.Abs(Mathf.PerlinNoise(x * 0.11f + 40, y * 0.03f + 7) - 0.5f);
                    var c = Color.Lerp(new Color(0.22f, 0.21f, 0.2f), new Color(0.42f, 0.4f, 0.37f), n);
                    px[y * 128 + x] = v < 0.035f ? new Color(0.95f, 0.72f, 0.2f) : c;
                }
            t.SetPixels(px); t.Apply();
            piedra = new Material(base_) { mainTexture = t, color = Color.white };
            piedra.SetFloat("_Glossiness", 0.25f);
            return piedra;
        }
    }

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
