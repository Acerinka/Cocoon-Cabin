using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace CocoonPrototype
{
    [DefaultExecutionOrder(-950)]
    public sealed class CocoonXRDisplayQuality : MonoBehaviour
    {
        [SerializeField] private float eyeTextureResolutionScale = 1.75f;
        [SerializeField] private int antiAliasing = 8;
        [SerializeField] private int targetFrameRate = 90;
        [SerializeField] private float minimumLodBias = 2.2f;
        [SerializeField] private float minimumShadowDistance = 70f;

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private IEnumerator Start()
        {
            Apply();
            yield return null;
            Apply();
            yield return new WaitForSeconds(0.25f);
            Apply();
        }

        private void Apply()
        {
            XRSettings.eyeTextureResolutionScale = Mathf.Clamp(eyeTextureResolutionScale, 1f, 1.8f);
            QualitySettings.antiAliasing = Mathf.Clamp(antiAliasing, 0, 8);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, minimumLodBias);
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, minimumShadowDistance);
            QualitySettings.realtimeReflectionProbes = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null)
                {
                    continue;
                }

                cameras[i].allowHDR = true;
                cameras[i].allowMSAA = true;
            }
        }
    }
}
