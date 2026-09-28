using System;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Presentation
{
    public sealed class NoteView : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform body;
        public bool HasBody => body;
        public void Validate(NoteKind kind)
        {
            if (!head || (kind == NoteKind.Hold && !body))
                throw new InvalidOperationException($"Missing head/body on {name}.");
        }
        public void SetMaterial(Material material)
        {
            head.GetComponent<Renderer>().sharedMaterial = material;
            if (body) body.GetComponent<Renderer>().sharedMaterial = material;
        }
        public void Render(NoteData note, NoteState state, double songTime, IScrollTimeline scroll,
            GameModeDefinition mode, GameplayPresentationSettings settings)
        {
            bool visible = state == NoteState.Pending || state == NoteState.Holding;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) return;
            float start = state == NoteState.Holding ? 0 : (float)scroll.DistanceBetween(songTime, note.StartTime);
            float tail = (float)scroll.DistanceBetween(songTime, note.EndTime);
            float x = mode.LaneX(note.Lane);
            head.position = new Vector3(x, settings.headHeight, start); head.localScale = settings.headSize;
            if (body) body.gameObject.SetActive(note.Kind == NoteKind.Hold);
            if (note.Kind == NoteKind.Hold)
            {
                float length = Mathf.Max(0, tail - start);
                body.position = new Vector3(x, settings.bodyElevation, start + length * .5f);
                body.localScale = new Vector3(settings.bodyWidth, settings.bodyHeight, Mathf.Max(settings.minimumBodyLength, length));
            }
        }
    }
}
