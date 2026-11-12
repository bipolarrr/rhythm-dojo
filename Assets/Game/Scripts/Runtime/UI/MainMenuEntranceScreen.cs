using System;
using System.Collections;
using RhythmDojo.Core;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmDojo.UI
{
    // The title owns only the doorway. The room visible through it belongs to ModeSelection.
    public sealed class MainMenuEntranceScreen : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        [SerializeField] private AudioSource effects;
        [SerializeField, Min(1f)] private float beatsPerMinute = 120f;
        [SerializeField, Range(0f, .3f)] private float titleBounceAmount = 0f;
        [SerializeField, Range(0f, .3f)] private float promptBounceAmount = 0f;
        [SerializeField, Range(.1f, 1f)] private float bounceDurationInBeats = .65f;
        private MainMenuCompositionRoot composition;
        private MainMenuCompositionRoot modeSelection;
        private IMainMenuBeatClock beatClock;
        private Vector3 titleScale;
        private Vector3 promptScale;
        private double beatStartTime;
        private float knockTime;
        private bool menuExpanded;
        private bool transitioning;
        private bool doorSoundPlayed;
        private float expansion;

        public void Validate()
        {
            if (!view || !effects)
            {
                throw new InvalidOperationException("Main menu entrance composition missing.");
            }
            view.Validate();
        }

        public void Initialize(MainMenuCompositionRoot composition, IMainMenuBeatClock beatClock)
        {
            Validate();
            this.composition = composition;
            this.beatClock = beatClock;
            titleScale = view.logoArtwork.localScale;
            promptScale = view.startPrompt.rectTransform.localScale;
            beatStartTime = AudioSettings.dspTime;
            view.logo.onClick.AddListener(ToggleMenu);
            view.entranceTrigger.onClick.AddListener(ToggleMenu);
            modeSelection = MainMenuCompositionRoot.InScene(
                SceneManager.GetSceneByPath(MainMenuActions.ModeSelectionScenePath));
            composition.TakeOwnershipFrom(modeSelection);
            if (modeSelection)
            {
                expansion = 1f;
                view.backdrop.enabled = false;
                view.SetMenuInteractable(false);
                ApplyExpansion();
                StartCoroutine(Close());
            }
            else
            {
                ApplyExpansion();
            }
        }

        private void Update()
        {
            bool cancel = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            cancel = cancel || Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (cancel && transitioning)
            {
                menuExpanded = false;
            }
            if (knockTime > 0f)
            {
                knockTime = Mathf.Max(0f, knockTime - Time.unscaledDeltaTime);
                float angle = Mathf.Sin(knockTime * 65f) * knockTime * 10f;
                view.logoArtwork.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            double beats;
            if (beatClock != null)
            {
                beats = beatClock.BeatPosition;
            }
            else
            {
                beats = Math.Max(0, AudioSettings.dspTime - beatStartTime) * Math.Max(1f, beatsPerMinute) / 60d;
            }
            float phase = (float)(beats - Math.Floor(beats));
            float progress = Mathf.Clamp01(phase / Mathf.Clamp(bounceDurationInBeats, .1f, 1f));
            float bounce = 1f - Mathf.SmoothStep(0f, 1f, progress);
            if (beats < 0)
            {
                bounce = 0;
            }
            view.logoArtwork.localScale = titleScale * (1f + bounce * titleBounceAmount);
            view.startPrompt.rectTransform.localScale = promptScale * (1f + bounce * promptBounceAmount);
        }

        private void ToggleMenu()
        {
            if (transitioning)
            {
                return;
            }
            menuExpanded = true;
            transitioning = true;
            doorSoundPlayed = false;
            view.SetMenuInteractable(false);
            view.logoArtwork.localScale = titleScale;
            StartCoroutine(Open());
        }

        private IEnumerator Open()
        {
            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(MainMenuActions.ModeSelectionScenePath, LoadSceneMode.Additive);
                if (operation == null)
                {
                    throw new InvalidOperationException("Cannot load ModeSelection.");
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
            if (operation == null)
            {
                RestoreEntrance();
                yield break;
            }
            yield return operation;
            modeSelection = MainMenuCompositionRoot.InScene(
                SceneManager.GetSceneByPath(MainMenuActions.ModeSelectionScenePath));
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (modeSelection && !modeSelection.IsInitialized)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    break;
                }
                yield return null;
            }
            if (!modeSelection || !modeSelection.IsInitialized)
            {
                Debug.LogError("ModeSelection did not initialize.", this);
                var scene = SceneManager.GetSceneByPath(MainMenuActions.ModeSelectionScenePath);
                if (scene.IsValid() && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
                RestoreEntrance();
                yield break;
            }

            view.backdrop.enabled = false;
            knockTime = .18f;
            if (view.knock)
            {
                effects.PlayOneShot(view.knock, .6f);
            }
            while (menuExpanded && expansion < 1f)
            {
                expansion = Mathf.MoveTowards(expansion, 1f, Time.unscaledDeltaTime / MainMenuScreen.MenuExpansionSeconds);
                if (!doorSoundPlayed && expansion >= .14f)
                {
                    doorSoundPlayed = true;
                    if (view.doorSlide)
                    {
                        effects.PlayOneShot(view.doorSlide, .42f);
                    }
                }
                ApplyExpansion();
                yield return null;
            }
            if (!menuExpanded)
            {
                yield return Close();
                yield break;
            }

            SceneManager.SetActiveScene(modeSelection.gameObject.scene);
            modeSelection.TakeOwnershipFrom(composition);
            var flow = FindFirstObjectByType<AppFlowController>();
            flow.PrepareSelection();
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        private IEnumerator Close()
        {
            transitioning = true;
            menuExpanded = false;
            modeSelection.View.modeSelection.CancelMotion();
            modeSelection.View.modeSelection.InputEnabled = false;
            while (expansion > 0f)
            {
                expansion = Mathf.MoveTowards(expansion, 0f, Time.unscaledDeltaTime / MainMenuScreen.MenuExpansionSeconds);
                ApplyExpansion();
                yield return null;
            }
            view.backdrop.enabled = true;
            SceneManager.SetActiveScene(gameObject.scene);
            yield return SceneManager.UnloadSceneAsync(modeSelection.gameObject.scene);
            modeSelection = null;
            RestoreEntrance();
        }

        private void ApplyExpansion()
        {
            view.SetExpansion(expansion);
            if (modeSelection)
            {
                modeSelection.View.SetExpansion(expansion);
            }
        }

        private void RestoreEntrance()
        {
            transitioning = false;
            menuExpanded = false;
            expansion = 0;
            view.backdrop.enabled = true;
            ApplyExpansion();
            view.SetMenuInteractable(true);
            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.logo.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (view && view.logo)
            {
                view.logo.onClick.RemoveListener(ToggleMenu);
                view.entranceTrigger.onClick.RemoveListener(ToggleMenu);
            }
        }
    }
}
