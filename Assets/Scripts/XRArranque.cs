using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace ParticulasRA
{
    /// <summary>
    /// Arranque de la RA con red de seguridad: pide el permiso de cámara y, si XR Plug-in Management
    /// no inició ARCore por sí solo, lo inicia a mano y recarga la escena para que los managers de
    /// AR Foundation encuentren los subsistemas. Expone un texto de diagnóstico para la interfaz.
    /// </summary>
    public class XRArranque : MonoBehaviour
    {
        static bool reintentado;

        public static string Diagnostico { get; private set; } = "XR: comprobando…";
        public static bool XRActivo { get; private set; }

        IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                float limite = Time.realtimeSinceStartup + 30f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && Time.realtimeSinceStartup < limite)
                    yield return null;
            }
#endif
            yield return null; // deja que XR Management termine su inicio automático

            if (Application.isEditor)
            {
                Diagnostico = "XR: editor (modo de prueba sin RA)";
                yield break;
            }

            var ajustes = XRGeneralSettings.Instance;
            if (ajustes == null || ajustes.Manager == null)
            {
                Diagnostico = "XR: el APK no trae la configuración de XR (ARCore no activado al compilar)";
                Debug.LogError("[Forja3D] " + Diagnostico);
                yield break;
            }

            if (ajustes.Manager.activeLoader == null)
            {
                Diagnostico = "XR: iniciando ARCore…";
                yield return ajustes.Manager.InitializeLoader();
                if (ajustes.Manager.activeLoader == null)
                {
                    Diagnostico = "XR: ARCore no pudo iniciarse (¿dispositivo compatible y Servicios de Google Play para RA instalados?)";
                    Debug.LogError("[Forja3D] " + Diagnostico);
                    yield break;
                }
                ajustes.Manager.StartSubsystems();
                if (!reintentado)
                {
                    // Los managers de AR Foundation ya buscaron sus subsistemas: recargar la escena los reengancha
                    reintentado = true;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    yield break;
                }
            }

            XRActivo = true;
            Diagnostico = $"XR: {ajustes.Manager.activeLoader.name} activo";
            Debug.Log("[Forja3D] " + Diagnostico);
        }

        /// <summary>Línea corta para la interfaz: estado de la sesión de RA + XR.</summary>
        public static string Linea() => $"[RA: {ARSession.state} · {Diagnostico}]";
    }
}
