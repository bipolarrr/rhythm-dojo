using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    [DisallowMultipleComponent]
    public sealed class DojoClickFeedback : MonoBehaviour
    {
        private DojoPunchImpact impact;
        [SerializeField]
        private AudioSource clickSource;
        [SerializeField]
        private AudioClip clickSound;
        private int lastClickFrame = -1;
        private int clickVariation;

        private void Awake()
        {
            if (!clickSource || !clickSound)
            {
                throw new System.InvalidOperationException("Click feedback audio references missing.");
            }
        }

        public void Initialize(DojoPunchImpact impact)
        {
            this.impact = impact;
        }

        private void OnEnable()
        {
            RhythmDojo.Services.PresentationPreferences.Changed += ApplyVolume;
        }

        private void ApplyVolume()
        {
            clickSource.volume = .18f * RhythmDojo.Audio.DojoSoundPlayer.EffectsVolume;
        }

        private void Update()
        {
            var mouse = Mouse.current;

            if (
                mouse == null
                || !RhythmDojo.Input.ApplicationFocusState.IsFocused
                || Cursor.lockState == CursorLockMode.Locked
            )
            {
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var position = mouse.position.ReadValue();

            if ((position.x < 0 || position.y < 0) || position.x >= Screen.width)
            {
                return;
            }

            if (position.y >= Screen.height)
            {
                return;
            }

            PlayAt(position);
        }

        public void PlayAt(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (screenPosition.x < 0 || screenPosition.x >= Screen.width)
            {
                return;
            }

            if (screenPosition.y < 0 || screenPosition.y >= Screen.height)
            {
                return;
            }

            if (clickSound)
            {
                if (lastClickFrame == Time.frameCount)
                {
                    return;
                }
                lastClickFrame = Time.frameCount;
                clickSource.clip = clickSound;
                ApplyVolume();
                clickSource.pitch = 1f + Mathf.Sin(++clickVariation * 2.4f) * .025f;
                // A click owns one voice, including repeated input while the game is paused.
                clickSource.Play();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!RhythmDojo.Input.ApplicationFocusState.UsesPlatformEvents)
            {
                return;
            }

            if (!focused)
            {
                Clear();
            }
        }

        private void OnDisable()
        {
            RhythmDojo.Services.PresentationPreferences.Changed -= ApplyVolume;
            Clear();
        }

        private void Clear()
        {
            lastClickFrame = -1;
            if (clickSource)
            {
                clickSource.Stop();
            }

            if (impact)
            {
                impact.Clear();
            }
        }
    }
}
