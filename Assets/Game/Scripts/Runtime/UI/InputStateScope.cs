using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RhythmDojo.UI
{
    // Every owner participates even when a previous owner already disabled the component.
    public sealed class InputStateScope : IDisposable
    {
        private sealed class BehaviourState
        {
            public int owners;
            public bool enabled;
        }

        private sealed class GroupState
        {
            public int owners;
            public bool interactable;
            public bool blocksRaycasts;
        }

        private sealed class FocusState
        {
            public Scene scene;
            public EventSystem system;
            public GameObject focus;
            public bool released;
        }

        private static readonly Dictionary<Behaviour, BehaviourState> behaviours =
            new Dictionary<Behaviour, BehaviourState>();
        private static readonly Dictionary<CanvasGroup, GroupState> groups =
            new Dictionary<CanvasGroup, GroupState>();
        private static readonly List<FocusState> focusOwners = new List<FocusState>();
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<CanvasGroup> blocked = new List<CanvasGroup>();
        private readonly FocusState focus;
        private bool disposed;

        public InputStateScope()
        {
        }

        public InputStateScope(Scene scene)
        {
            focus = new FocusState { scene = scene, system = EventSystem.current };
            if (focus.system)
            {
                focus.focus = focus.system.currentSelectedGameObject;
            }
            focusOwners.Add(focus);
        }

        public void Suspend(Behaviour component)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InputStateScope));
            }
            if (!component || suspended.Contains(component))
            {
                return;
            }
            if (!behaviours.TryGetValue(component, out var state))
            {
                state = new BehaviourState { enabled = component.enabled };
                behaviours.Add(component, state);
            }
            state.owners++;
            suspended.Add(component);
            component.enabled = false;
        }

        public void Block(CanvasGroup group)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InputStateScope));
            }
            if (!group || blocked.Contains(group))
            {
                return;
            }
            if (!groups.TryGetValue(group, out var state))
            {
                state = new GroupState
                {
                    interactable = group.interactable,
                    blocksRaycasts = group.blocksRaycasts,
                };
                groups.Add(group, state);
            }
            state.owners++;
            blocked.Add(group);
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            var notifications = new List<IModalInputParticipant>();
            foreach (var component in suspended)
            {
                var state = behaviours[component];
                if (--state.owners != 0)
                {
                    continue;
                }
                behaviours.Remove(component);
                if (!component)
                {
                    continue;
                }
                component.enabled = state.enabled;
                if (component.isActiveAndEnabled && component is IModalInputParticipant participant)
                {
                    notifications.Add(participant);
                }
            }
            foreach (var group in blocked)
            {
                var state = groups[group];
                if (--state.owners != 0)
                {
                    continue;
                }
                groups.Remove(group);
                if (group)
                {
                    group.interactable = state.interactable;
                    group.blocksRaycasts = state.blocksRaycasts;
                }
            }
            suspended.Clear();
            blocked.Clear();

            var failures = new List<Exception>();
            foreach (var participant in notifications)
            {
                try
                {
                    participant.ResumeAfterModal();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
            RestoreFocus();
            if (failures.Count != 0)
            {
                throw new AggregateException("Input state was restored, but a resume notification failed.", failures);
            }
        }

        private void RestoreFocus()
        {
            if (focus == null)
            {
                return;
            }
            focus.released = true;
            FocusState restored = null;
            while (focusOwners.Count > 0)
            {
                var last = focusOwners[focusOwners.Count - 1];
                if (!last.released)
                {
                    break;
                }
                restored = last;
                focusOwners.RemoveAt(focusOwners.Count - 1);
            }
            if (restored == null)
            {
                return;
            }
            if (restored.scene.IsValid() && restored.scene.isLoaded)
            {
                SceneManager.SetActiveScene(restored.scene);
            }
            if (restored.system && restored.system.isActiveAndEnabled)
            {
                if (restored.focus && restored.focus.activeInHierarchy)
                {
                    restored.system.SetSelectedGameObject(restored.focus);
                }
                else
                {
                    restored.system.SetSelectedGameObject(null);
                }
            }
        }
    }
}
