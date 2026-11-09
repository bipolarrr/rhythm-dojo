using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class GameplayFeedbackBuilder
    {
        public static void Build(GameplayHitFeedback feedback, Transform parent)
        {
            var effectsRoot = new GameObject("Gameplay Impacts", typeof(RectTransform)).GetComponent<RectTransform>();
            effectsRoot.SetParent(parent, false);
            effectsRoot.anchorMin = Vector2.zero;
            effectsRoot.anchorMax = Vector2.one;
            effectsRoot.offsetMin = effectsRoot.offsetMax = Vector2.zero;
            SceneDependencyAssembler.Wire(feedback, "effectsRoot", effectsRoot);
            var holdTemplate = CreateGraphic("Hold Glow Template", effectsRoot);
            var holdPrefab = PrefabUtility.SaveAsPrefabAsset(holdTemplate.gameObject,
                "Assets/Game/Prefabs/HoldGlow.prefab").GetComponent<GameplayImpactGraphic>();
            Object.DestroyImmediate(holdTemplate.gameObject);
            SceneDependencyAssembler.Wire(feedback, "holdTemplate", holdPrefab);
            SceneDependencyAssembler.Wire(feedback, "perfectSound",
                SceneResources.Require<AudioClip>("Assets/Game/Resources/UI/DojoHitPerfect.wav"));
            SceneDependencyAssembler.Wire(feedback, "goodSound",
                SceneResources.Require<AudioClip>("Assets/Game/Resources/UI/DojoHitGood.wav"));
            var serialized = new SerializedObject(feedback);
            var bursts = serialized.FindProperty("bursts");
            bursts.arraySize = 24;
            for (int i = 0; i < bursts.arraySize; i++)
            {
                bursts.GetArrayElementAtIndex(i).objectReferenceValue = CreateGraphic("Hit Burst " + i, effectsRoot);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameplayImpactGraphic CreateGraphic(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GameplayImpactGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<GameplayImpactGraphic>();
            graphic.raycastTarget = false;
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(.5f, .5f);
            go.SetActive(false);
            return graphic;
        }
    }
}
