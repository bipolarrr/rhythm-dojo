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

        private DojoEntranceView entrance;
        private CanvasGroup overlay;
        private CanvasGroup selectionGroup;
        private SongSelectionScreen selection;
        private Action recover;
        private Font font;
        private AudioSource audioSource;
        private AudioClip slide;
        private InputStateScope selectionInputScope;
        private RectTransform[] selectionPanels;
        private CanvasGroup[] panelGroups;
        private float[] priorAlphas;
        private Vector2[] priorPositions;
        private Vector3[] priorScales;
        private GameObject recoveryInput;
        private Button recoveryButton;
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

            var go = new GameObject("Dojo Entrance Transition", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = go.AddComponent<CanvasScaler>();
            var sourceScaler = source.GetComponentInParent<CanvasScaler>();
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            var blocker = go.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;

            var transition = go.AddComponent<DojoEntranceTransition>();
            transition.overlay = go.AddComponent<CanvasGroup>();
            transition.entrance = Instantiate(source, go.transform, false);
            transition.entrance.SetLogoSeparation(source.MenuLogoSeparation);
            transition.entrance.SetOpening(source.Opening);

            if (transition.entrance.trainingMenu)
            {
                transition.entrance.trainingMenu.gameObject.SetActive(false);
                Destroy(transition.entrance.trainingMenu.gameObject);
            }

            foreach (var button in transition.entrance.GetComponentsInChildren<Button>(true))
            {
                button.interactable = false;
                Destroy(button);
            }
            foreach (var graphic in transition.entrance.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            transition.entrance.SetApproach(0f);
            transition.font = font;
            transition.slide = slide;
            transition.recover = recover;
            transition.audioSource = go.AddComponent<AudioSource>();
            transition.audioSource.playOnAwake = false;
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
                group = screen.gameObject.AddComponent<CanvasGroup>();
            }

            Active.selectionGroup = group;
            Active.selectionInputScope.Block(group);
            Active.PrepareSelectionPanels();
            if (Active.recoveryInput)
            {
                Active.recoveryInput.SetActive(false);
                Destroy(Active.recoveryInput);
            }

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (Active.recoveryButton)
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
                    group = panel.gameObject.AddComponent<CanvasGroup>();
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
                var events = new GameObject("Recovery Input", typeof(EventSystem));
                events.transform.SetParent(transform, false);
                recoveryInput = events;
                var input = events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                input.AssignDefaultActions();
            }

            var go = new GameObject("Return To Title", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(620f, 130f);
            go.GetComponent<Image>().color = new Color(.15f, .07f, .04f, .98f);
            var button = go.GetComponent<Button>();
            recoveryButton = button;
            button.onClick.AddListener(ReturnToTitle);
            var label = new GameObject("Message", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var text = label.GetComponent<Text>();
            UiFontSettings.Apply(text, UiTextRole.Help);
            text.font = font;
            text.fontStyle = UiFontSettings.Style;
            text.text = "곡 선택 화면을 열지 못했습니다.\n클릭하여 타이틀로 돌아가기";
            text.fontSize = 25;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, .88f, .66f);
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(go);
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
