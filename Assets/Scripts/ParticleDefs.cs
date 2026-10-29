using System.Globalization;
using UnityEngine;

namespace ParticulasRA
{
    /// <summary>Tipos de partícula disponibles en la simulación.</summary>
    public enum PType { Em = 0, Ep, MuM, MuP, PiP, PiM, P, Pb, N, G }

    /// <summary>Ficha inmutable de una partícula: datos de simulación + datos reales para la ficha educativa.</summary>
    public sealed class PInfo
    {
        public readonly PType Type;
        public readonly string Id, Symbol, Name;
        public readonly float Mass;      // masa de simulación (reescalada; e = 1)
        public readonly int Charge;      // carga en unidades de e
        public readonly float Radius;    // radio visual en unidades de simulación (u)
        public readonly Color Color;
        public readonly float Tau;       // vida media de simulación en segundos (0 = estable)
        public readonly string RealMass, Spin, Family, Quarks, RealLife, Description;

        public PInfo(PType type, string id, string symbol, string name, float mass, int charge, float radius,
                     string hex, float tau, string realMass, string spin, string family, string quarks,
                     string realLife, string description)
        {
            Type = type; Id = id; Symbol = symbol; Name = name; Mass = mass; Charge = charge; Radius = radius;
            Color = ParticleDefs.Hex(hex); Tau = tau; RealMass = realMass; Spin = spin; Family = family;
            Quarks = quarks; RealLife = realLife; Description = description;
        }

        public bool Charged => Charge != 0;
        public bool Unstable => Tau > 0f;
    }

    /// <summary>Tabla de partículas (sección 4.2 del superprompt) y utilidades de formato.</summary>
    public static class ParticleDefs
    {
        public const int Count = 10;

        /// <summary>Velocidad de la luz en unidades de simulación por segundo (la cámara mide 2 u de lado).</summary>
        public const float C = 1.2f;

        public static readonly PInfo[] All =
        {
            new PInfo(PType.Em, "em", "e⁻", "Electrón", 1f, -1, 0.012f, "#4FD1FF", 0f,
                "0,511 MeV/c²", "½", "Leptón", "Fundamental", "Estable",
                "Ligero y negativo: en el campo magnético traza espirales muy cerradas."),
            new PInfo(PType.Ep, "ep", "e⁺", "Positrón", 1f, 1, 0.012f, "#FF5C8A", 0f,
                "0,511 MeV/c²", "½", "Antileptón", "Antipartícula del e⁻", "Estable",
                "Gira al revés que el electrón. Si toca uno, o se frena en el medio, se aniquila en dos fotones de 0,511 MeV."),
            new PInfo(PType.MuM, "mum", "μ⁻", "Muón", 4f, -1, 0.014f, "#7DFF8A", 3f,
                "105,7 MeV/c²", "½", "Leptón", "Fundamental", "2,2 μs",
                "Un «electrón pesado» que llega a la superficie en los rayos cósmicos. Decae en e⁻ + ν̄ₑ + ν_μ."),
            new PInfo(PType.MuP, "mup", "μ⁺", "Antimuón", 4f, 1, 0.014f, "#D9FF5C", 3f,
                "105,7 MeV/c²", "½", "Antileptón", "Fundamental", "2,2 μs",
                "Antipartícula del muón. Decae en e⁺ + νₑ + ν̄_μ."),
            new PInfo(PType.PiP, "pip", "π⁺", "Pion positivo", 5f, 1, 0.015f, "#FFB347", 1.6f,
                "139,6 MeV/c²", "0", "Mesón", "u d̄", "26 ns",
                "Un quark y un antiquark unidos por la fuerza fuerte. Decae en μ⁺ + ν_μ."),
            new PInfo(PType.PiM, "pim", "π⁻", "Pion negativo", 5f, -1, 0.015f, "#B98CFF", 1.6f,
                "139,6 MeV/c²", "0", "Mesón", "d ū", "26 ns",
                "Su traza se curva al revés que la del π⁺. Decae en μ⁻ + ν̄_μ."),
            new PInfo(PType.P, "p", "p", "Protón", 14f, 1, 0.021f, "#FF4B3E", 0f,
                "938,3 MeV/c²", "½", "Barión", "u u d", "Estable (>10³⁴ años)",
                "Pesado: su trayectoria apenas se curva. Sirve de núcleo para que un fotón cree un par e⁻e⁺."),
            new PInfo(PType.Pb, "pb", "p̄", "Antiprotón", 14f, -1, 0.021f, "#FF9EF0", 0f,
                "938,3 MeV/c²", "½", "Antibarión", "ū ū d̄", "Estable",
                "Al chocar con un protón se aniquila y libera ~1,9 GeV en piones y fotones."),
            new PInfo(PType.N, "n", "n", "Neutrón", 14f, 0, 0.021f, "#AAB4C4", 12f,
                "939,6 MeV/c²", "½", "Barión", "u d d", "≈ 880 s (libre)",
                "Sin carga: ignora el campo y va en línea recta. Libre, decae en p + e⁻ + ν̄ₑ (desintegración β)."),
            new PInfo(PType.G, "g", "γ", "Fotón", 0f, 0, 0.010f, "#FFF27A", 0f,
                "0", "1", "Bosón de gauge", "Fundamental", "Estable",
                "Luz: siempre viaja a c y el campo no la desvía. Con más de 1,022 MeV, cerca de un núcleo puede convertirse en un par e⁻e⁺."),
        };

        public static PInfo Get(PType t) => All[(int)t];

        public static bool IsNucleon(PType t) => t == PType.P || t == PType.N || t == PType.Pb;

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        public static string ChargeText(int q) => q > 0 ? "+1" : q < 0 ? "−1" : "0";

        /// <summary>Número con coma decimal (formato español) sin depender de la cultura del dispositivo.</summary>
        public static string Num(float v, string format = "0.0") =>
            v.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>Texto de la ficha educativa (admite rich text de uGUI).</summary>
        public static string Ficha(PType t)
        {
            var p = Get(t);
            return $"<b><color=#E7ECF6>{p.Symbol} · {p.Name}</color></b> — {p.Family}. Masa {p.RealMass}, carga {ChargeText(p.Charge)}, " +
                   $"espín {p.Spin}. Composición: {p.Quarks}. Vida media: {p.RealLife}.\n{p.Description}";
        }
    }
}
