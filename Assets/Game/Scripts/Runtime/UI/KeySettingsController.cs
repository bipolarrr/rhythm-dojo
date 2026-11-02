using System;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class KeySettingsController : IDisposable
    {
        private readonly SettingsView view;
        private readonly UnityEngine.Events.UnityAction[] actions;
        private int listeningLane = -1;
        private int startedFrame;
        private EventSystem eventSystem;
        private bool navigationEvents;
        private bool waitingForRelease;

        public KeySettingsController(SettingsView view)
        {
            this.view = view;
            actions = new UnityEngine.Events.UnityAction[view.keyBindingButtons.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                int lane = i;
                actions[i] = () => Begin(lane);
                view.keyBindingButtons[i].onClick.AddListener(actions[i]);
            }
            view.resetKeys.onClick.AddListener(Reset);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < view.keyBindingButtons.Length; i++)
            {
                view.keyBindingButtons[i].GetComponentInChildren<Text>().text = PlayKeyBindings.GetLabel(i);
            }
        }

        private void Begin(int lane)
        {
            Cancel();
            listeningLane = lane;
            startedFrame = Time.frameCount;
            eventSystem = EventSystem.current;
            if (eventSystem)
            {
                navigationEvents = eventSystem.sendNavigationEvents;
                eventSystem.sendNavigationEvents = false;
            }
            view.keyBindingButtons[lane].GetComponentInChildren<Text>().text = "입력 대기";
            view.keyBindingStatus.text = (lane + 1) + "번 레인에 사용할 키를 눌러 주세요. (Esc: 취소)";
        }

        public bool Tick()
        {
            if (waitingForRelease)
            {
                if (Keyboard.current == null || !Keyboard.current.anyKey.isPressed)
                {
                    Cancel();
                }
                return true;
            }
            if (listeningLane < 0)
            {
                return false;
            }
            if (!view.keySettingsPanel.activeInHierarchy)
            {
                Cancel();
                return true;
            }
            if (Keyboard.current == null || Time.frameCount == startedFrame)
            {
                return true;
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                FinishCapture();
                view.keyBindingStatus.text = "키 변경을 취소했습니다.";
                return true;
            }
            foreach (var key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame)
                {
                    continue;
                }
                bool saved = PlayKeyBindings.TrySetKey(listeningLane, key.keyCode, out var message);
                view.keyBindingStatus.text = message;
                if (saved)
                {
                    FinishCapture();
                }
                break;
            }
            return true;
        }

        private void FinishCapture()
        {
            listeningLane = -1;
            waitingForRelease = true;
            Refresh();
        }

        public void Cancel()
        {
            if (listeningLane < 0 && !waitingForRelease)
            {
                return;
            }
            listeningLane = -1;
            waitingForRelease = false;
            if (eventSystem)
            {
                eventSystem.sendNavigationEvents = navigationEvents;
            }
            eventSystem = null;
            Refresh();
        }

        private void Reset()
        {
            Cancel();
            PlayKeyBindings.Reset();
            Refresh();
            view.keyBindingStatus.text = "기본 D F J K로 복원했습니다.";
        }

        public void Dispose()
        {
            Cancel();
            for (int i = 0; i < actions.Length; i++)
            {
                view.keyBindingButtons[i].onClick.RemoveListener(actions[i]);
            }
            view.resetKeys.onClick.RemoveListener(Reset);
        }
    }
}
