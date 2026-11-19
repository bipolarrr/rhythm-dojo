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
            SceneDependencyAssembler.Wire(feedback, "atmosphere", parent.GetComponent<GameplayHudView>().atmosphere);
            IngamePresentationAssets.PrepareAudio();
            var inputSource = new GameObject("Strike Input Audio", typeof(AudioSource)).GetComponent<AudioSource>();
            inputSource.transform.SetParent(feedback.transform, false);
            inputSource.playOnAwake = false;
            inputSource.spatialBlend = 0;
            inputSource.priority = 100;
            SceneDependencyAssembler.Wire(feedback, "inputSource", inputSource);
            SceneDependencyAssembler.Wire(feedback, "inputSound",
                SceneResources.Require<AudioClip>("Assets/Game/Audio/Gameplay/StrikeWhoosh.wav"));
            SceneDependencyAssembler.Wire(feedback, "sounds",
                DojoSoundBuilder.Build(feedback.transform, feedback.GetComponent<AudioSource>(), inputSource));
            var effectsRoot = new GameObject("Gameplay Impacts", typeof(RectTransform)).GetComponent<RectTransform>();
            effectsRoot.SetParent(parent, false);
            effectsRoot.SetSiblingIndex(parent.GetComponent<GameplayHudView>().judgmentGroup.transform.GetSiblingIndex());
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
                SceneResources.Require<AudioClip>("Assets/Game/Audio/Gameplay/StrikeImpact.wav"));
            SceneDependencyAssembler.Wire(feedback, "goodSound",
                SceneResources.Require<AudioClip>("Assets/Game/Audio/Foley/HitGood1.wav"));
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
            SceneDependencyAssembler.Wire(graphic, "bristles",
                SceneResources.Require<Texture2D>("Assets/Game/Art/DojoGameplay/ImpactBristles.png"));
            graphic.raycastTarget = false;
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(.5f, .5f);
            go.SetActive(false);
            return graphic;
        }
    }
}
