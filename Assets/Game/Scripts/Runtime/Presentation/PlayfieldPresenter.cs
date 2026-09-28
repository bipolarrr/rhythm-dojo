using UnityEngine;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Presentation
{
    public sealed class PlayfieldPresenter : MonoBehaviour
    {
        [SerializeField] private Material highwayMaterial;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material judgmentMaterial;
        public void Validate()
        {
            if (!highwayMaterial || !railMaterial || !judgmentMaterial)
                throw new System.InvalidOperationException("Playfield materials missing.");
        }
        public void Build(GameModeDefinition mode, GameplayPresentationSettings settings)
        {
            Validate(); mode.Validate(); settings.Validate();
            if (settings.laneGap >= mode.LaneSpacing) throw new System.InvalidOperationException("Lane gap must be smaller than spacing.");
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            for (int i = 0; i < mode.LaneCount; i++)
                Box($"Lane {i}", new Vector3(mode.LaneX(i), -.15f, settings.highwayCenter),
                    new Vector3(mode.LaneSpacing - settings.laneGap, .2f, settings.highwayLength), highwayMaterial);
            for (int i = 0; i <= mode.LaneCount; i++)
                Box($"Rail {i}", new Vector3((i - mode.LaneCount * .5f) * mode.LaneSpacing, 0, settings.highwayCenter),
                    new Vector3(settings.railWidth, settings.railHeight, settings.highwayLength), railMaterial);
            Box("Judgement Line", new Vector3(0, .05f, 0),
                new Vector3(mode.LaneCount * mode.LaneSpacing, settings.judgmentLineHeight, settings.judgmentLineDepth), judgmentMaterial);
        }
        private void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(transform);
            go.transform.position = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); collider.enabled = false;
            if (UnityEngine.Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
