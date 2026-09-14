using UnityEngine;

namespace CocoonPrototype
{
    public sealed class CocoonSafePickupZone : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float radius = 1.25f;
        [SerializeField] private float pulseSpeed = 3.5f;

        private Renderer[] renderers;
        private Collider[] colliders;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private bool lastInside;
        private bool hasLoggedInside;
        private bool zoneActive = true;

        public bool IsPlayerInside { get; private set; }
        public Vector3 Center => transform.position;

        public void Configure(Transform headTransform, Transform visuals, float detectionRadius)
        {
            head = headTransform;
            visualRoot = visuals;
            radius = detectionRadius;
            ApplyExperienceScaleToVisuals();
            CacheRenderers();
            CacheColliders();
        }

        private void Awake()
        {
            ApplyExperienceScaleToVisuals();
            CacheRenderers();
            CacheColliders();
        }

        private void Update()
        {
            if (!zoneActive)
            {
                IsPlayerInside = false;
                return;
            }

            if (head != null)
            {
                Vector3 flatHead = head.position;
                flatHead.y = transform.position.y;
                float effectiveRadius = GetEffectiveRadius();
                IsPlayerInside = Vector3.Distance(flatHead, transform.position) <= effectiveRadius;
                if (!hasLoggedInside || IsPlayerInside != lastInside)
                {
                    float distance = Vector3.Distance(flatHead, transform.position);
                    CocoonDebugLog.Info("Pickup", "Safe zone " + (IsPlayerInside ? "entered" : "exited") + ". distance=" + distance.ToString("0.00") + "m radius=" + effectiveRadius.ToString("0.00") + "m.", this);
                    lastInside = IsPlayerInside;
                    hasLoggedInside = true;
                }
            }

            float pulse = 0.55f + Mathf.Sin(Time.time * pulseSpeed) * 0.18f;
            if (renderers == null)
            {
                CacheRenderers();
            }

            if (renderers == null)
            {
                return;
            }

            Color color = IsPlayerInside ? new Color(0.15f, 1f, 0.58f) : new Color(0.02f, 0.74f, 0.6f);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Material material = renderers[i].material;
                Color current = GetMaterialColor(material, color);
                Color baseColor = Color.Lerp(current, color, Time.deltaTime * 6f);
                ApplyMaterialColor(material, baseColor, color * (IsPlayerInside ? 2.3f : pulse));
            }
        }

        public void ProjectFromStopPoint(Transform stopPoint)
        {
            if (stopPoint == null)
            {
                return;
            }

            float experienceScale = GetExperienceScale();
            ApplyExperienceScaleToVisuals();
            Vector3 projectedCenter = stopPoint.position + stopPoint.right * (2.55f * experienceScale) + stopPoint.forward * (0.08f * experienceScale);
            projectedCenter.y = transform.position.y;
            transform.position = projectedCenter;
            transform.rotation = Quaternion.LookRotation(stopPoint.forward, Vector3.up);
            CocoonDebugLog.Info("Pickup", "Projected safe pickup zone from taxi light at " + FormatPosition(projectedCenter) + ".", this);
        }

        public void SetZoneActive(bool active)
        {
            if (zoneActive == active)
            {
                return;
            }

            ApplyExperienceScaleToVisuals();
            zoneActive = active;
            IsPlayerInside = false;
            hasLoggedInside = false;

            if (renderers == null)
            {
                CacheRenderers();
            }

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].gameObject.SetActive(active);
                    }
                }
            }

            if (colliders == null)
            {
                CacheColliders();
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = active;
                    }
                }
            }

            CocoonDebugLog.Info("Pickup", "Projected safe pickup zone " + (active ? "shown" : "hidden") + ".", this);
        }

        private static Color GetMaterialColor(Material material, Color fallbackColor)
        {
            if (material == null)
            {
                return fallbackColor;
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            if (material.HasProperty(ColorId))
            {
                return material.GetColor(ColorId);
            }

            return fallbackColor;
        }

        private static void ApplyMaterialColor(Material material, Color baseColor, Color emissionColor)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, baseColor);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, baseColor);
            }

            if (material.HasProperty(EmissionColorId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, emissionColor);
            }
        }

        private void CacheRenderers()
        {
            if (visualRoot != null)
            {
                renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void CacheColliders()
        {
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private float GetEffectiveRadius()
        {
            return radius * GetExperienceScale();
        }

        private float GetExperienceScale()
        {
            return CocoonExperienceScale.RoadmapScale;
        }

        private void ApplyExperienceScaleToVisuals()
        {
            Transform scaledRoot = visualRoot != null ? visualRoot : transform;
            if (scaledRoot == null)
            {
                return;
            }

            float experienceScale = GetExperienceScale();
            Vector3 targetScale = Vector3.one * experienceScale;
            if (Vector3.Distance(scaledRoot.localScale, targetScale) > 0.0001f)
            {
                scaledRoot.localScale = targetScale;
            }
        }

        private static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("0.00") + ", " + position.y.ToString("0.00") + ", " + position.z.ToString("0.00") + ")";
        }
    }
}
