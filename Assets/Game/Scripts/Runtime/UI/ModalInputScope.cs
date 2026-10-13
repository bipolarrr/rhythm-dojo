using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public interface IModalInputParticipant { void ResumeAfterModal(); }

    public sealed class ModalInputScope : IDisposable
    {
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly GameObject focus;
        private readonly Scene scene;
        private bool disposed;
        public static ModalInputScope Open(Scene scene, Action open)
        {
            var scope = new ModalInputScope(scene);
            try { open(); return scope; }
            catch { scope.Dispose(); throw; }
        }
        public ModalInputScope(Scene scene)
        {
            this.scene = scene;
            focus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<Behaviour>())
                        if (component.enabled && (component is IModalInputParticipant || component is GraphicRaycaster ||
                            component is EventSystem || component is BaseInputModule))
                        { suspended.Add(component); component.enabled = false; }
            }
            catch { Dispose(); throw; }
        }
        public void Close(Func<AsyncOperation> close, Action completed)
        {
            // A failed unload leaves the modal and its background input lease intact for retry.
            var operation = close() ?? throw new InvalidOperationException("Cannot close modal scene.");
            operation.completed += _ => { completed?.Invoke(); Dispose(); };
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var component in suspended)
            {
                if (!component) continue;
                component.enabled = true;
                if (component is IModalInputParticipant participant) participant.ResumeAfterModal();
            }
            suspended.Clear();
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            if (EventSystem.current && focus && focus.activeInHierarchy) EventSystem.current.SetSelectedGameObject(focus);
        }
    }
}
