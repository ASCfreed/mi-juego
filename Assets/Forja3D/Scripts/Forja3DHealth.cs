// Forja3D — vida para jugador y enemigos.
using System;
using UnityEngine;

namespace Forja3D
{
    public class Forja3DHealth : MonoBehaviour
    {
        public int vidaMaxima = 3;
        public bool esJugador;
        public float invulnerable = 0.6f;
        public int Vida { get; private set; }
        public bool Muerto => Vida <= 0;
        public event Action AlRecibirDanio, AlMorir;
        // Lo usa el jugador para bloquear/hacer parry: recibe (daño, atacante, imparable) y devuelve el daño final
        public Func<int, Transform, bool, int> Interceptar;
        public float InvulnerableHasta;   // esquiva: frames de invulnerabilidad
        public event System.Action AlRebotar; // el golpe no hizo daño (multiplicador < 1: Pluto hinchado de oro)
        public float multiplicadorDanio = 1f; // >1 = herida expuesta (p. ej. Cerbero con dos cabezas saciadas)
        public Transform UltimoAtacante { get; private set; }

        float ultimoGolpe = -99f;
        Animator anim;

        void Awake() { Vida = vidaMaxima; anim = GetComponentInChildren<Animator>(); }

        public bool Danio(int cantidad = 1, Transform fuente = null, bool imparable = false)
        {
            if (Muerto || Time.time - ultimoGolpe < invulnerable || Time.time < InvulnerableHasta) return false;
            if (Interceptar != null) cantidad = Interceptar(cantidad, fuente, imparable);
            if (cantidad <= 0) return false;
            if (!esJugador && multiplicadorDanio > 1f) cantidad = Mathf.CeilToInt(cantidad * multiplicadorDanio);
            else if (!esJugador && multiplicadorDanio < 1f) { cantidad = Mathf.FloorToInt(cantidad * multiplicadorDanio); if (cantidad <= 0) { AlRebotar?.Invoke(); return false; } }
            ultimoGolpe = Time.time;
            UltimoAtacante = fuente;
            Vida = Mathf.Max(0, Vida - cantidad);
            if (Muerto)
            {
                Forja3DAnim.Bool(anim, "Dead", true);
                AlMorir?.Invoke();
            }
            else
            {
                Forja3DAnim.Trigger(anim, "Hit");
                AlRecibirDanio?.Invoke();
            }
            return true;
        }

        public void Curar(int cantidad) { if (!Muerto) Vida = Mathf.Min(vidaMaxima, Vida + cantidad); }
    }

    // Ayudas para que el Animator funcione aunque falten parámetros (según las animaciones generadas)
    public static class Forja3DAnim
    {
        public static bool Tiene(Animator a, string n, AnimatorControllerParameterType t)
        {
            if (!a || !a.runtimeAnimatorController) return false;
            foreach (var p in a.parameters) if (p.name == n && p.type == t) return true;
            return false;
        }
        public static void Trigger(Animator a, string n) { if (Tiene(a, n, AnimatorControllerParameterType.Trigger)) a.SetTrigger(n); }
        public static void Bool(Animator a, string n, bool v) { if (Tiene(a, n, AnimatorControllerParameterType.Bool)) a.SetBool(n, v); }
        public static void Float(Animator a, string n, float v) { if (Tiene(a, n, AnimatorControllerParameterType.Float)) a.SetFloat(n, v); }
    }
}
