// Forja3D — registro de diagnóstico en el teléfono.
// Guarda los últimos errores y los pasos de carga en un archivo (persistentDataPath/forja3d_registro.txt).
// Si el juego se cierra de golpe, al volver a abrirlo la pantalla de título muestra qué estaba pasando,
// para poder corregirlo sin cable ni consola.
// EXPANSIÓN FUTURA: en PC/iOS funciona igual; para la tienda se puede enviar a un servicio de reportes.
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Forja3D
{
    public static class Forja3DRegistro
    {
        static readonly Queue<string> lineas = new Queue<string>();
        static string ruta;
        static readonly object cerrojo = new object();
        public static string SesionAnterior { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Iniciar()
        {
            ruta = Path.Combine(Application.persistentDataPath, "forja3d_registro.txt");
            try
            {
                // La sesión anterior se conserva para mostrarla en la pantalla de título
                if (File.Exists(ruta)) SesionAnterior = File.ReadAllText(ruta);
            }
            catch { }
            Application.logMessageReceivedThreaded += AlMensaje;
            SceneManager.sceneLoaded += (s, m) => Paso($"Escena cargada: {s.name} (memoria {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB)");
            Paso($"Inicio v{Application.version} · {SystemInfo.deviceModel} · RAM {SystemInfo.systemMemorySize} MB");
        }

        public static void Paso(string texto) => Agregar(texto);

        static void AlMensaje(string mensaje, string pila, LogType tipo)
        {
            if (tipo == LogType.Log && !mensaje.StartsWith("[Forja3D]")) return;
            if (tipo == LogType.Warning) return;
            string linea = tipo == LogType.Log ? mensaje : $"{tipo}: {mensaje}\n{Recortar(pila, 600)}";
            Agregar(linea);
        }

        static string Recortar(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n);

        static void Agregar(string linea)
        {
            lock (cerrojo)
            {
                lineas.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {linea}");
                while (lineas.Count > 40) lineas.Dequeue();
                try { File.WriteAllText(ruta, string.Join("\n", lineas)); } catch { }
            }
        }
    }
}
