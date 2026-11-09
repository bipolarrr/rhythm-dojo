using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace RhythmDojo.EditorTools
{
    public static class DojoTransitionAssetBuilder
    {
        public static void Build(DojoEntranceView source)
        {
            var go = new GameObject("Dojo Entrance Transition", typeof(RectTransform), typeof(Canvas));
            go.SetActive(false);
            try
            {
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
                var overlay = go.AddComponent<CanvasGroup>();
                var entrance = Object.Instantiate(source, go.transform, false);
                entrance.transitionTemplate = null;
                if (entrance.trainingMenu)
                {
                    Object.DestroyImmediate(entrance.trainingMenu.gameObject);
                }

                foreach (var button in entrance.GetComponentsInChildren<Button>(true))
                {
                    Object.DestroyImmediate(button);
                }

                foreach (var graphic in entrance.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }

                var audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                var events = new GameObject("Recovery Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(go.transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                events.SetActive(false);
                var buttonObject = new GameObject("Return To Title", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(go.transform, false);
                var rect = (RectTransform)buttonObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(620f, 130f);
                buttonObject.GetComponent<Image>().color = new Color(.15f, .07f, .04f, .98f);
                var label = new GameObject("Message", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(buttonObject.transform, false);
                var text = label.GetComponent<Text>();
                UiFontSettings.Apply(text, UiTextRole.Help);
                text.fontStyle = UiFontSettings.Style;
                text.text = "곡 선택 화면을 열지 못했습니다.\n클릭하여 타이틀로 돌아가기";
                text.fontSize = 25;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f, .88f, .66f);
                text.raycastTarget = false;
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                buttonObject.SetActive(false);
                SceneDependencyAssembler.Wire(transition, "entrance", entrance);
                SceneDependencyAssembler.Wire(transition, "overlay", overlay);
                SceneDependencyAssembler.Wire(transition, "audioSource", audioSource);
                SceneDependencyAssembler.Wire(transition, "recoveryInput", events);
                SceneDependencyAssembler.Wire(transition, "recoveryButton", buttonObject.GetComponent<Button>());
                SceneDependencyAssembler.Wire(transition, "recoveryMessage", text);
                go.SetActive(true);
                source.transitionTemplate = PrefabUtility.SaveAsPrefabAsset(go,
                    "Assets/Game/Prefabs/DojoEntranceTransition.prefab").GetComponent<DojoEntranceTransition>();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
