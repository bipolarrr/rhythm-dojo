using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    [Serializable]
    public sealed class JourneyScenery
    {
        public Transform item;
        public Vector3 position;
        public Vector3 rotation;
        public float sway;
        public float drift;
    }

    // Fixed scenery, atmospheric pools and projection cameras are authored in the scene.
    // The director owns time; these views never read input, run a clock or change gameplay.
    public abstract class TrainingJourneyView : MonoBehaviour
    {
        public Camera sceneryCamera;
        public CanvasGroup picture;
        public Renderer[] surfaces;
        public JourneyScenery[] scenery;
        public JourneyScenery[] mist;

        private MaterialPropertyBlock properties;
        public float VisualTime { get; private set; }

        public virtual void Validate()
        {
            if (!sceneryCamera || !picture)
            {
                throw new InvalidOperationException("Training journey projection is not authored.");
            }
            if (surfaces == null || surfaces.Length == 0)
            {
                throw new InvalidOperationException("Training journey surfaces are missing.");
            }
            foreach (var surface in surfaces)
            {
                if (!surface || !surface.sharedMaterial)
                {
                    throw new InvalidOperationException("Training journey surface or material is missing.");
                }
            }
            ValidateScenery(scenery);
            ValidateScenery(mist);
        }

        private static void ValidateScenery(JourneyScenery[] items)
        {
            if (items == null || items.Length == 0)
            {
                throw new InvalidOperationException("Training journey ambient composition is missing.");
            }
            foreach (var item in items)
            {
                if (item == null || !item.item)
                {
                    throw new InvalidOperationException("Training journey scenery reference is missing.");
                }
            }
        }

        public virtual void ResetPresentation()
        {
            Validate();
            VisualTime = 0;
            StepEnvironment(0, 0, 0);
        }

        public void SetVisible(bool visible, float alpha = 1)
        {
            gameObject.SetActive(visible);
            picture.gameObject.SetActive(visible);
            picture.alpha = Mathf.Clamp01(alpha);
        }

        protected void StepEnvironment(float delta, float progress, float energy)
        {
            VisualTime += delta;
            for (int i = 0; i < scenery.Length; i++)
            {
                var item = scenery[i];
                float angle = Mathf.Sin(VisualTime * (.67f + i % 4 * .13f) + i * 1.7f);
                item.item.localRotation = Quaternion.Euler(item.rotation +
                    Vector3.forward * (angle * item.sway * (1 + energy * .4f)));
            }
            for (int i = 0; i < mist.Length; i++)
            {
                var item = mist[i];
                item.item.localPosition = item.position + new Vector3(
                    Mathf.Sin(VisualTime * .11f + i * 2.1f) * item.drift,
                    Mathf.Sin(VisualTime * .23f + i) * .22f,
                    Mathf.Sin(VisualTime * .09f + i) * 3);
            }

            properties ??= new MaterialPropertyBlock();
            properties.SetFloat("_VisualTime", VisualTime);
            properties.SetFloat("_Progress", progress);
            properties.SetFloat("_Energy", energy);
            foreach (var surface in surfaces)
            {
                surface.SetPropertyBlock(properties);
            }
            float fit = Mathf.Min(1, (16f / 9) / sceneryCamera.aspect);
            sceneryCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(29 * Mathf.Deg2Rad) * fit) * Mathf.Rad2Deg;
        }
    }
}
