using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

namespace CocoonPrototype
{
    [DefaultExecutionOrder(-1000)]
    public sealed class CocoonXRRuntimeLifecycle : MonoBehaviour
    {
        private bool startedSubsystems;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureLifecycleObject()
        {
            if (FindObjectOfType<CocoonXRRuntimeLifecycle>() != null)
            {
                return;
            }

            var lifecycleObject = new GameObject("Cocoon XR Runtime Lifecycle");
            DontDestroyOnLoad(lifecycleObject);
            lifecycleObject.AddComponent<CocoonXRRuntimeLifecycle>();
        }

        private IEnumerator Start()
        {
            XRManagerSettings manager = GetManager();
            if (manager == null)
            {
                CocoonDebugLog.Warn("XR", "XR manager not found; XR runtime lifecycle did not start.", this);
                yield break;
            }

            if (!manager.isInitializationComplete)
            {
                CocoonDebugLog.Info("XR", "Initializing XR loader.", this);
                yield return manager.InitializeLoader();
            }

            if (manager.isInitializationComplete && manager.activeLoader != null)
            {
                manager.StartSubsystems();
                startedSubsystems = true;
                CocoonDebugLog.Info("XR", "XR subsystems started with loader " + manager.activeLoader.name + ".", this);
            }
            else
            {
                CocoonDebugLog.Warn("XR", "Could not initialize an active loader. Check OpenXR runtime and Quest Link state.", this);
            }
        }

        private void OnDisable()
        {
            StopIfReady();
        }

        private void OnApplicationQuit()
        {
            StopIfReady();
        }

        private void OnDestroy()
        {
            XRManagerSettings manager = GetManager();
            if (manager == null || !manager.isInitializationComplete)
            {
                return;
            }

            StopIfReady();
            manager.DeinitializeLoader();
        }

        private void StopIfReady()
        {
            XRManagerSettings manager = GetManager();
            if (!startedSubsystems || manager == null || !manager.isInitializationComplete || manager.activeLoader == null)
            {
                return;
            }

            manager.StopSubsystems();
            startedSubsystems = false;
            CocoonDebugLog.Info("XR", "XR subsystems stopped.", this);
        }

        private static XRManagerSettings GetManager()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            return settings != null ? settings.Manager : null;
        }
    }
}
