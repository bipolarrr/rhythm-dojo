using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class DojoEntranceView : MonoBehaviour
    {
        public DojoEntranceTransition transitionTemplate;
        public RectTransform startPrompt;
        public RectTransform trainingInterior;
        public const float InteriorScale = 1.08f;
        // Compatibility timing marker; contact itself is determined by the moving edges.
        public const float DoorContactProgress = .42f;

        public RectTransform leftDoor;
        public RectTransform rightDoor;
        public CanvasGroup frame;
        public CanvasGroup doorInk;
        public CanvasGroup interiorBackdrop;
        public RectTransform doorwayApproach;
        public CanvasGroup trainingMenu;
        public RectTransform logoClip;
        public DojoSlidingImage slidingPlaque;
        public DojoSlidingImage slidingLogo;
        public float MenuLogoSeparation { get; private set; }
        // The stationary logo joins the doors only when their split edges meet.
        public float LogoSeparation
        {
            get
            {
                float displacement = -leftDoor.anchoredPosition.x;
                float depth = 0f;
                if (slidingPlaque)
                {
                    depth = Mathf.Clamp(slidingPlaque.splitDepth, 0f, 12f);
                }
                float distance = displacement - MenuLogoSeparation + depth;
                if (distance <= 0f)
                {
                    return MenuLogoSeparation;
                }

                float catchupDistance = 36f + depth * 5f;
                if (distance >= catchupDistance)
                {
                    return displacement;
                }

                // Contact starts at the visible inner edge of the wooden cut face,
                // not the front artwork seam. Spread acceleration across that depth
                // so the logo does not jump to door speed in only two or three frames.
                float amount = distance / catchupDistance;
                float target = 1f - depth / catchupDistance;
                float eased = amount * amount * amount * ((10f * target - 4f)
                    + amount * ((7f - 15f * target) + amount * (6f * target - 3f)));
                return MenuLogoSeparation + catchupDistance * eased;
            }
        }
        public float Opening { get; private set; }

        public void CopyPresentationStateFrom(DojoEntranceView source)
        {
            startPrompt.gameObject.SetActive(source.startPrompt.gameObject.activeSelf);
            startPrompt.localScale = source.startPrompt.localScale;
            trainingInterior.localScale = source.trainingInterior.localScale;
            slidingLogo.rectTransform.localScale = source.slidingLogo.rectTransform.localScale;
            SetLogoSeparation(source.MenuLogoSeparation);
            SetOpening(source.Opening);
        }

        public void SetOpening(float amount)
        {
            Opening = Mathf.Clamp01(amount);
            float eased = SmootherStep(Opening);
            float width = ((RectTransform)leftDoor.parent).rect.width;
            // One uninterrupted opening curve. The logo joins at geometric contact
            // without changing the door's speed or restarting its animation.
            leftDoor.anchoredPosition = new Vector2(-width * .55f * eased, 0f);
            rightDoor.anchoredPosition = new Vector2(width * .55f * eased, 0f);
            frame.alpha = 1f;
            SetLogoSeparation(MenuLogoSeparation);
        }

        private static float SmootherStep(float amount)
        {
            return amount * amount * amount * (amount * (amount * 6f - 15f) + 10f);
        }

        public void SetLogoSeparation(float value)
        {
            MenuLogoSeparation = value;
            if (slidingPlaque)
            {
                slidingPlaque.SetSeparation(LogoSeparation);
            }

            if (slidingLogo)
            {
                slidingLogo.SetSeparation(LogoSeparation / Mathf.Max(.01f, slidingLogo.rectTransform.localScale.x));
            }
        }

        public void SetApproach(float amount)
        {
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            // Finish at the same framing used by the song selection backdrop.
            // Only the nearby doorway should sweep past the edges of the screen.
            float frameScale = 1f / (1f - .46f * eased);
            float backgroundScale = Mathf.Lerp(1f, InteriorScale, eased);
            transform.localScale = Vector3.one * backgroundScale;
            frame.transform.localScale = Vector3.one * (frameScale / backgroundScale);
        }

        public void SetEntrySequence(float amount)
        {
            if (!doorwayApproach)
            {
                throw new System.InvalidOperationException("Authored doorway approach missing.");
            }

            SetOpening(Mathf.InverseLerp(.14f, .62f, amount));
            if (doorInk)
            {
                float sweep = Mathf.InverseLerp(.18f, .40f, amount);
                doorInk.alpha = Mathf.Sin(sweep * Mathf.PI) * .55f;
                doorInk.transform.localScale = new Vector3(.5f + sweep * .8f, 1f, 1f);
            }
            float approach = DojoMotion.Push(Mathf.InverseLerp(.43f, 1f, amount));
            float scale = Mathf.Lerp(1f, 3.65f, approach);
            doorwayApproach.localScale = Vector3.one * scale;
            doorwayApproach.anchoredPosition = Vector2.up * (65f * (scale - 1f));

            // The physical frame stays opaque until its aperture surrounds the viewport.
            frame.alpha = 1f;
            if (amount >= 1f)
            {
                frame.alpha = 0f;
            }
            float knock = Mathf.InverseLerp(0f, .14f, amount);
            float recoil = Mathf.Sin(knock * Mathf.PI * 4f) * (1f - knock) * 2.5f;
            leftDoor.anchoredPosition += Vector2.right * recoil;
            rightDoor.anchoredPosition -= Vector2.right * recoil;
        }
    }
}
