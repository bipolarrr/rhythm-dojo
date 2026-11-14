using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // All artwork and moving instances are authored by TitleSkyLayoutBuilder.
    public sealed class TitleSkyView : MonoBehaviour
    {
        public RectTransform travel;
        public float descentDistance = 840f;
        public CanvasGroup logo;
        public CanvasGroup matte;
        public Button start;
        public RectTransform[] clouds;
        public Image[] birds;
        public Sprite[] birdFrames;
        public float Descent { get; private set; }
        private float elapsed;
        private Vector2[] cloudHomes;

        public void Validate()
        {
            if (!travel || !logo || !matte)
            {
                throw new InvalidOperationException("Title sky presentation missing.");
            }
            if (descentDistance <= 0f)
            {
                throw new InvalidOperationException("Title sky travel distance missing.");
            }
            if (!start || clouds == null || birds == null)
            {
                throw new InvalidOperationException("Title sky motion references missing.");
            }
            if (clouds.Length != 3 || birds.Length != 2)
            {
                throw new InvalidOperationException("Title sky requires three clouds and two birds.");
            }
            if (birdFrames == null || birdFrames.Length != 6)
            {
                throw new InvalidOperationException("Title bird wingbeat frames missing.");
            }
            foreach (var cloud in clouds)
            {
                if (!cloud)
                {
                    throw new InvalidOperationException("Title cloud reference missing.");
                }
            }
            foreach (var bird in birds)
            {
                if (!bird)
                {
                    throw new InvalidOperationException("Title bird reference missing.");
                }
            }
            foreach (var frame in birdFrames)
            {
                if (!frame)
                {
                    throw new InvalidOperationException("Title bird sprite missing.");
                }
            }
        }

        public void ResetIdle()
        {
            if (cloudHomes == null)
            {
                cloudHomes = new Vector2[clouds.Length];
                for (int i = 0; i < clouds.Length; i++)
                {
                    cloudHomes[i] = clouds[i].anchoredPosition;
                }
            }
            elapsed = 0f;
            SetDescent(0f);
            logo.alpha = 1f;
            start.interactable = true;
            AnimateSky();
        }

        public void SetDescent(float amount)
        {
            Descent = Mathf.Clamp01(amount);
            travel.anchoredPosition = Vector2.down * (descentDistance * (1f - Descent));
            // The matte fills only the existing aspect-ratio margins, behind the journey.
            matte.alpha = 1f - Descent;
        }

        private void Update()
        {
            if (Descent >= 1f)
            {
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            AnimateSky();
        }

        private void AnimateSky()
        {
            for (int i = 0; i < clouds.Length; i++)
            {
                var position = clouds[i].anchoredPosition;
                // Wrap only after the entire sprite has left the clipped composition.
                position.x = Mathf.Repeat(elapsed * (5f + i * 2f) + i * 530f + 150f, 1800f) - 900f;
                if (cloudHomes != null)
                {
                    position.y = cloudHomes[i].y + Descent * (45f + i * 60f);
                }
                clouds[i].anchoredPosition = position;
            }
            for (int i = 0; i < birds.Length; i++)
            {
                float phase = elapsed + i * 3.7f;
                birds[i].rectTransform.anchoredPosition = new Vector2(
                    Mathf.Repeat(phase * 30f + i * 270f + 410f, 1480f) - 740f,
                    160f + i * 55f + Mathf.Sin(phase * .8f) * 12f);
                birds[i].sprite = birdFrames[(int)(phase * 9f) % birdFrames.Length];
            }
        }
    }
}
