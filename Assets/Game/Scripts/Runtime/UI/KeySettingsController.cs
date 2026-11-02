using System;
using RhythmDojo.Gameplay;
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
        private readonly GameModeDefinition mode;
        private readonly string modeId;
        private readonly bool legacy;

        public KeySettingsController(SettingsView view)
            : this(view, null, null)
        {
        }

        public KeySettingsController(SettingsView view, GameModeDefinition mode, string modeId)
        {
            this.view = view;
            this.mode = mode;
            this.modeId = modeId;
            legacy = modeId == null && !mode;
            if (!legacy)
            {
                BuildRows();
            }
            actions = new UnityEngine.Events.UnityAction[view.keyBindingButtons.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                int lane = i;
                actions[i] = () => Begin(lane);
                view.keyBindingButtons[i].onClick.AddListener(actions[i]);
            }
            view.resetKeys.onClick.AddListener(Reset);
            view.resetKeys.interactable = legacy || mode;
            view.keyBindingStatus.text = "변경한 키는 이 모드에 자동 저장됩니다.";
            if (!legacy && !mode)
            {
                view.keyBindingStatus.text = "선택한 채보의 모드 정보를 사용할 수 없습니다.";
            }
            Refresh();
        }

        private void BuildRows()
        {
            if (!view.keyRowTemplate || !view.keyRowsContainer)
            {
                throw new InvalidOperationException("Key settings row template missing. Rebuild Settings.");
            }
            foreach (var button in view.keyBindingButtons)
            {
                var row = button.GetComponentInParent<KeyBindingRowView>();
                if (row)
                {
                    row.gameObject.SetActive(false);
                    if (UnityEngine.Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(row.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(row.gameObject);
                    }
                }
            }
            int count = 0;
            if (mode)
            {
                count = mode.LaneCount;
            }
            view.keyBindingButtons = new Button[count];
            float height = ((RectTransform)view.keyRowTemplate.transform).sizeDelta.y;
            for (int i = 0; i < count; i++)
            {
                var row = UnityEngine.Object.Instantiate(view.keyRowTemplate, view.keyRowsContainer);
                row.gameObject.SetActive(true);
                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(0, -i * height);
                row.caption.text = (i + 1) + " · " + mode.GetLane(i).displayName;
                row.button.interactable = PlayKeyBindings.CanEdit(mode, i);
                view.keyBindingButtons[i] = row.button;
            }
            view.keyRowsContainer.sizeDelta = new Vector2(0, count * height);
            view.keyRowsScroll.verticalNormalizedPosition = 1;
        }

        private void Refresh()
        {
            for (int i = 0; i < view.keyBindingButtons.Length; i++)
            {
                string label;
                if (legacy)
                {
                    label = PlayKeyBindings.GetLabel(i);
                }
                else
                {
                    label = PlayKeyBindings.GetLabel(mode, modeId, i);
                }
                view.keyBindingButtons[i].GetComponentInChildren<Text>().text = label;
            }
        }

        private void Begin(int lane)
        {
            if (!view.keyBindingButtons[lane].interactable)
            {
                return;
            }
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
                bool saved;
                string message;
                if (legacy)
                {
                    saved = PlayKeyBindings.TrySetKey(listeningLane, key.keyCode, out message);
                }
                else
                {
                    saved = PlayKeyBindings.TrySetKey(mode, modeId, listeningLane, key.keyCode, out message);
                }
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
            if (legacy)
            {
                PlayKeyBindings.Reset();
            }
            else
            {
                PlayKeyBindings.Reset(modeId);
            }
            Refresh();
            view.keyBindingStatus.text = "이 모드의 기본 키로 복원했습니다.";
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
