using UnityEngine;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Core
{
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        public void Validate()
        {
            if (!settings) throw new System.InvalidOperationException("Bootstrap settings missing.");
            settings.ValidateOptions();
        }
        // Keep a black screen visible while the selection scene loads asynchronously.
        // Awake also covers Bootstrap assets generated before the camera was added.
        private void Awake() => EnsureCamera();
        public Camera EnsureCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>())
                    if (camera.isActiveAndEnabled && camera.targetDisplay == 0 && !camera.targetTexture)
                        return camera;
            var loadingCamera = new GameObject("Loading Camera").AddComponent<Camera>();
            loadingCamera.transform.SetParent(transform, false);
            loadingCamera.clearFlags = CameraClearFlags.SolidColor;
            loadingCamera.backgroundColor = Color.black;
            loadingCamera.cullingMask = 0;
            loadingCamera.targetDisplay = 0;
            return loadingCamera;
        }
        private void Start() { Validate(); AppFlowController.Create(settings).ShowSelection(); }
    }
}
