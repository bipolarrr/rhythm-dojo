using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    [DisallowMultipleComponent]
    public sealed class DojoClickFeedback : MonoBehaviour
    {
        private DojoPunchImpact impact;
        private AudioSource clickSource;
        private AudioClip clickSound;

        private void Awake()
        {
            clickSound = Resources.Load<AudioClip>("UI/DojoClick");
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.loop = false;
            clickSource.spatialBlend = 0;
            clickSource.volume = .32f;
            clickSource.priority = 128;
            clickSource.ignoreListenerPause = true;
        }

        public void Initialize(DojoPunchImpact impact)
        {
            this.impact = impact;
        }

        private void Update()
        {
            var mouse = Mouse.current;

            if (
                mouse == null
                || !UnityEngine.Application.isFocused
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

            if (impact)
            {
                impact.PlayAt(screenPosition);
            }

            if (clickSound)
            {
                clickSource.PlayOneShot(clickSound);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Clear();
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Clear()
        {
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
