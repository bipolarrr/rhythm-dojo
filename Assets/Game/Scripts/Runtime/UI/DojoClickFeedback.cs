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
                clickSource.PlayOneShot(clickSound);
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
