using System;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    // The host owns the game state/input gate. Completion runs only after the authored
    // overlay has exited and every held key/button has been released for a full frame.
    public sealed class DialogueScreen : MonoBehaviour
    {
        [SerializeField] private DialogueView view;
        private DialogueSession session;
        private Action completed;
        private float visibility;
        private bool closing;
        private int lastInputFrame;
        private int releasedFrame;

        public bool IsActive => session != null;
        public DialogueSession Session => session;

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Dialogue view missing.");
            }
            view.Validate();
        }

        public void Play(DialogueSequenceAsset sequence, Action onCompleted)
        {
            Validate();
            if (!sequence)
            {
                throw new ArgumentNullException(nameof(sequence));
            }
            var lines = sequence.CreateLines();
            Play(lines, onCompleted);
        }

        public void Play(RhythmDojo.Dialogue.DialogueLine[] lines, Action onCompleted)
        {
            Validate();
            Cancel();
            completed = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
            session = new DialogueSession(lines, new DialogueReadStore());
            closing = false;
            visibility = 0;
            lastInputFrame = Time.frameCount;
            releasedFrame = -1;
            view.root.SetActive(true);
            view.group.blocksRaycasts = true;
            view.advance.onClick.AddListener(Confirm);
            view.skip.onClick.AddListener(SkipRead);
            ClearSelection();
            ShowLine();
            view.Animate(0, 0);
            PlaySound(view.arrivalSound, .2f);
        }

        public void Cancel()
        {
            completed = null;
            session = null;
            closing = false;
            if (!view)
            {
                return;
            }
            view.advance.onClick.RemoveListener(Confirm);
            view.skip.onClick.RemoveListener(SkipRead);
            view.source.Stop();
            view.root.SetActive(false);
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            // Manual keyboard progression and pointer buttons share one frame guard.
            // No selected Button may also receive EventSystem Submit for the same key.
            ClearSelection();
            if (closing)
            {
                TickExit();
                return;
            }

            visibility = Mathf.MoveTowards(visibility, 1, Time.unscaledDeltaTime / .35f);
            view.Animate(visibility, Time.unscaledDeltaTime);
            view.advance.interactable = visibility >= 1;
            session.Tick(Time.unscaledDeltaTime);
            RefreshText();
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                SkipRead();
                return;
            }
            bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            confirm |= keyboard.numpadEnterKey.wasPressedThisFrame;
            if (confirm)
            {
                Confirm();
            }
        }

        private void Confirm()
        {
            if (!CanAcceptInput())
            {
                return;
            }
            int previous = session.Index;
            session.Confirm();
            AfterInput(previous);
        }

        private void SkipRead()
        {
            if (!CanAcceptInput() || !session.CanSkipRead)
            {
                return;
            }
            int previous = session.Index;
            session.SkipRead();
            AfterInput(previous);
        }

        private bool CanAcceptInput()
        {
            if (!IsActive || closing)
            {
                return false;
            }
            if (visibility < 1 || lastInputFrame == Time.frameCount)
            {
                return false;
            }
            lastInputFrame = Time.frameCount;
            return true;
        }

        private void AfterInput(int previous)
        {
            PlaySound(view.turnSound, .16f);
            if (session.IsComplete)
            {
                closing = true;
                view.advance.interactable = false;
                view.skip.interactable = false;
                view.hint.text = "";
                return;
            }
            if (previous != session.Index)
            {
                ShowLine();
            }
            RefreshText();
        }

        private void ShowLine()
        {
            view.ShowLine(session.Current, session.Index, session.Count);
            view.advance.interactable = visibility >= 1;
            RefreshText();
        }

        private void RefreshText()
        {
            view.body.text = session.VisibleText;
            view.skip.interactable = visibility >= 1 && session.CanSkipRead;
            view.hint.text = "Enter / Space / 클릭  ·  대사 펼치기";
            if (session.IsLineComplete)
            {
                view.hint.text = "Enter / Space / 클릭  ·  다음  ›";
            }
        }

        private void TickExit()
        {
            visibility = Mathf.MoveTowards(visibility, 0, Time.unscaledDeltaTime / .28f);
            view.Animate(visibility, Time.unscaledDeltaTime);
            if (visibility > 0 || HasHeldInput())
            {
                releasedFrame = -1;
                return;
            }
            if (releasedFrame < 0)
            {
                releasedFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount <= releasedFrame)
            {
                return;
            }

            var callback = completed;
            Cancel();
            callback?.Invoke();
        }

        private static bool HasHeldInput()
        {
            if (Keyboard.current?.anyKey.isPressed == true)
            {
                return true;
            }
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }
            return mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            view.source.volume = (PresentationPreferences.Current ?? PresentationPreferences.Load()).gameplayVolume;
            view.source.PlayOneShot(clip, volume);
        }

        private void OnDisable() => Cancel();
    }
}
