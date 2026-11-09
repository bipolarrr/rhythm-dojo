using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Owns only presentation. Scene loading and recovery are supplied by Core.
    public sealed class DojoEntranceTransition : MonoBehaviour
    {
        public const float DoorOpeningSeconds = 1.4f;
        public static DojoEntranceTransition Active { get; private set; }
        public bool Failed { get; private set; }

        [SerializeField]
        private DojoEntranceView entrance;
        [SerializeField]
        private CanvasGroup overlay;
        private CanvasGroup selectionGroup;
        private SongSelectionScreen selection;
        private Action recover;
        [SerializeField]
        private AudioSource audioSource;
        private AudioClip slide;
        private InputStateScope selectionInputScope;
        private RectTransform[] selectionPanels;
        private CanvasGroup[] panelGroups;
        private float[] priorAlphas;
        private Vector2[] priorPositions;
        private Vector3[] priorScales;
        [SerializeField]
        private GameObject recoveryInput;
        [SerializeField]
        private Button recoveryButton;
        [SerializeField]
        private Text recoveryMessage;
        private AsyncOperation sceneCleanup;
        [SerializeField]
        private float readyTimeout = 20f;

        public static DojoEntranceTransition Begin(
            DojoEntranceView source, AudioClip slide, Font font, Action recover)
        {
            if (Active)
            {
                throw new InvalidOperationException("A dojo entrance transition is already active.");
            }

            if (!source || !source.transitionTemplate)
            {
                throw new InvalidOperationException("Authored entrance transition prefab missing.");
            }

            // A transient prefab instance preserves presentation across scene unloading.
            var transition = Instantiate(source.transitionTemplate);
            transition.name = "Dojo Entrance Transition";
            DontDestroyOnLoad(transition.gameObject);
            transition.entrance.CopyPresentationStateFrom(source);
            transition.entrance.SetApproach(0f);
            transition.slide = slide;
            transition.recover = recover;
            transition.recoveryMessage.font = font;
            transition.recoveryButton.onClick.AddListener(transition.ReturnToTitle);
            Active = transition;
            transition.StartCoroutine(transition.Reveal());
            return transition;
        }

        public static void SelectionReady(SongSelectionScreen screen)
        {
            if (!Active)
            {
                return;
            }

            Active.selection = screen;
            if (Active.selectionInputScope != null)
            {
                return;
            }
            Active.selectionInputScope = new InputStateScope();
            Active.selectionInputScope.Suspend(screen);
            var group = screen.GetComponent<CanvasGroup>();
            if (!group)
            {
                throw new InvalidOperationException("Selection transition CanvasGroup missing.");
            }

            Active.selectionGroup = group;
            Active.selectionInputScope.Block(group);
            Active.PrepareSelectionPanels();
            if (Active.recoveryInput)
            {
                Active.recoveryInput.SetActive(false);
            }

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (Active.recoveryButton && Active.recoveryButton.gameObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(Active.recoveryButton.gameObject);
                }
            }
        }

        public void WaitForSceneCleanup(AsyncOperation operation)
        {
            sceneCleanup = operation;
        }

        private IEnumerator Reveal()
        {
            // Let deferred destruction of the copied menu and input components finish first.
            yield return null;

            // Scene activation, song discovery and UI construction can block the main thread.
            // Keep the cloned doorway still until that work has completed behind it.
            float deadline = Time.realtimeSinceStartup + readyTimeout;
            while (!selection || (sceneCleanup != null && !sceneCleanup.isDone))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    ShowFailure();
                    yield break;
                }

                yield return null;
            }

            // Settle layout/font rebuilds and scene cleanup before starting the motion clock.
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            float opening = entrance.Opening;
            if (slide && opening < 1f)
            {
                audioSource.PlayOneShot(slide, .65f);
            }

            float elapsed = 0f;
            while (elapsed < DoorOpeningSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                entrance.SetOpening(Mathf.Lerp(opening, 1f, Mathf.Clamp01(elapsed / DoorOpeningSeconds)));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 1.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float amount = Mathf.Clamp01(elapsed / .85f);
                entrance.SetApproach(amount);
                // Keep the doorway solid until the zoom has carried its frame offscreen.
                overlay.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, amount));
                SetSelectionReveal(Mathf.InverseLerp(.6f, 1.2f, elapsed));
                yield return null;
            }

            ReleaseSelection();
            Destroy(gameObject);
        }

        private void PrepareSelectionPanels()
        {
            var view = selection.GetComponent<SongSelectionView>();
            selectionPanels = Array.Empty<RectTransform>();
            if (view && view.entrancePanels != null)
            {
                selectionPanels = view.entrancePanels;
            }

            int count = selectionPanels.Length;
            panelGroups = new CanvasGroup[count];
            priorAlphas = new float[count];
            priorPositions = new Vector2[count];
            priorScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var panel = selectionPanels[i];
                if (!panel)
                {
                    continue;
                }

                priorPositions[i] = panel.anchoredPosition;
                priorScales[i] = panel.localScale;
                var group = panel.GetComponent<CanvasGroup>();
                if (!group)
                {
                    throw new InvalidOperationException("Selection panel CanvasGroup missing.");
                }

                panelGroups[i] = group;
                priorAlphas[i] = group.alpha;
            }

            SetSelectionReveal(0f);
        }

        private void SetSelectionReveal(float amount)
        {
            if (!selectionGroup)
            {
                return;
            }

            float eased = 1f - Mathf.Pow(1f - amount, 3f);
            for (int i = 0; i < selectionPanels.Length; i++)
            {
                if (!selectionPanels[i])
                {
                    continue;
                }

                selectionPanels[i].anchoredPosition =
                    priorPositions[i] + Vector2.down * (28f * (1f - eased));
                selectionPanels[i].localScale = priorScales[i] * Mathf.Lerp(1.025f, 1f, eased);
                panelGroups[i].alpha = priorAlphas[i] * Mathf.SmoothStep(0f, 1f, amount);
            }
        }

        public void Fail()
        {
            if (Failed)
            {
                return;
            }
            StopAllCoroutines();
            ShowFailure();
        }

        private void ShowFailure()
        {
            Failed = true;
            if (!EventSystem.current)
            {
                recoveryInput.SetActive(true);
            }

            recoveryButton.gameObject.SetActive(true);
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(recoveryButton.gameObject);
            }
        }

        private void ReturnToTitle()
        {
            recover?.Invoke();
            Destroy(gameObject);
        }

        public void Cancel()
        {
            StopAllCoroutines();
            ReleaseSelection();
            Destroy(gameObject);
        }

        private void ReleaseSelection()
        {
            if (overlay)
            {
                overlay.blocksRaycasts = false;
            }
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster)
            {
                raycaster.enabled = false;
            }
            SetSelectionReveal(1f);
            selectionInputScope?.Dispose();
            selectionInputScope = null;
        }

        private void OnDestroy()
        {
            ReleaseSelection();
            if (Active == this)
            {
                Active = null;
            }
        }
    }
}
