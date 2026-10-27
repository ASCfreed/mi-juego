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

        float ultimoGolpe = -99f;
        Animator anim;

        void Awake() { Vida = vidaMaxima; anim = GetComponentInChildren<Animator>(); }

        public void Danio(int cantidad = 1)
        {
            if (Muerto || Time.time - ultimoGolpe < invulnerable) return;
            ultimoGolpe = Time.time;
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
        }

        public void Curar(int cantidad) { if (!Muerto) Vida = Mathf.Min(vidaMaxima, Vida + cantidad); }
    }

    // Ayudas para que el Animator funcione aunque falten parámetros (según las animaciones generadas)
    public static class Forja3DAnim
    {
        static bool Tiene(Animator a, string n, AnimatorControllerParameterType t)
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
