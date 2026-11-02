using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public interface IModalInputParticipant
    {
        void ResumeAfterModal();
    }

    public sealed class ModalInputScope : IDisposable
    {
        private readonly Scene scene;
        private readonly InputStateScope state;
        private bool disposed;

        public static ModalInputScope Open(Scene scene, Action open)
        {
            var scope = new ModalInputScope(scene);
            try
            {
                open();
                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        public ModalInputScope(Scene scene)
        {
            this.scene = scene;
            state = new InputStateScope(scene);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                    {
                        switch (component)
                        {
                            case IModalInputParticipant _:
                            case GraphicRaycaster _:
                            case EventSystem _:
                            case BaseInputModule _:
                                state.Suspend(component);
                                break;
                        }
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Close(Func<AsyncOperation> close, Action completed)
        {
            // A failed unload keeps the background lease intact for retry.
            var operation = close() ?? throw new InvalidOperationException("Cannot close modal scene.");
            operation.completed += _ =>
            {
                try
                {
                    completed?.Invoke();
                }
                finally
                {
                    Dispose();
                }
            };
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            state.Dispose();
        }
    }
}
