using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    public sealed class TutorialScreen : MonoBehaviour
    {
        [SerializeField] private TutorialView view;
        private Action returnRequested;
        private float age;

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Tutorial view missing.");
            }
            view.Validate();
        }

        public void Initialize(Action onReturn)
        {
            Validate();
            view.backButton.onClick.RemoveListener(Return);
            returnRequested = onReturn ?? throw new ArgumentNullException(nameof(onReturn));
            view.backButton.onClick.AddListener(Return);
            age = 0;
            view.Reveal(0);
        }

        private void Update()
        {
            if (returnRequested == null)
            {
                return;
            }
            if (age < .45f)
            {
                age += Time.unscaledDeltaTime;
                view.Reveal(age / .45f);
            }
            bool cancel = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            cancel |= Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (cancel)
            {
                Return();
            }
        }

        private void Return()
        {
            returnRequested?.Invoke();
        }

        private void OnDestroy()
        {
            if (view && view.backButton)
            {
                view.backButton.onClick.RemoveListener(Return);
            }
            returnRequested = null;
        }
    }
}
