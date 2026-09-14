using UnityEngine;

namespace CocoonPrototype
{
    public static class CocoonExperienceScale
    {
        public static float RoadmapScale => ResolveRoadmapScale();

        public static float ResolveRoadmapScale()
        {
            GameObject roadmap = GameObject.Find("ROADMAP");
            return roadmap != null ? ResolveRoadmapScale(roadmap.transform) : 1f;
        }

        public static float ResolveRoadmapScale(Transform roadmap)
        {
            if (roadmap == null)
            {
                return 1f;
            }

            Vector3 scale = roadmap.lossyScale;
            float horizontalScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f;
            if (horizontalScale < 0.02f || horizontalScale > 0.35f)
            {
                return 1f;
            }

            return horizontalScale;
        }

        public static float ResolveRigScale(Transform rigRoot)
        {
            if (rigRoot == null)
            {
                return 1f;
            }

            Vector3 scale = rigRoot.lossyScale;
            float horizontalScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f;
            return Mathf.Clamp(horizontalScale, 0.02f, 10f);
        }

        public static bool CalibrateCharacterController(Transform rigRoot)
        {
            if (rigRoot == null)
            {
                return false;
            }

            CharacterController controller = rigRoot.GetComponent<CharacterController>();
            if (controller == null)
            {
                return false;
            }

            Vector3 scale = rigRoot.lossyScale;
            float horizontalScale = ResolveRigScale(rigRoot);
            float verticalScale = Mathf.Clamp(Mathf.Abs(scale.y), 0.02f, 10f);
            float scaledHeight = controller.height * verticalScale;
            float scaledRadius = controller.radius * horizontalScale;
            float maxStepOffset = Mathf.Max(0.001f, scaledHeight + scaledRadius * 2f - 0.001f);
            float desiredStepOffset = Mathf.Min(0.3f * horizontalScale, maxStepOffset);
            float desiredSkinWidth = Mathf.Clamp(0.08f * horizontalScale, 0.003f, 0.08f);
            bool changed = false;

            if (Mathf.Abs(controller.stepOffset - desiredStepOffset) > 0.0001f)
            {
                controller.stepOffset = desiredStepOffset;
                changed = true;
            }

            if (Mathf.Abs(controller.skinWidth - desiredSkinWidth) > 0.0001f)
            {
                controller.skinWidth = desiredSkinWidth;
                changed = true;
            }

            return changed;
        }
    }
}
