using UnityEngine;
using UnityEngine.EventSystems;

namespace RhythmDojo.Authoring
{
    public sealed class AudioTimelineResize : MonoBehaviour, IDragHandler
    {
        public ChartAudioTimeline Owner { get; set; }

        public void OnDrag(PointerEventData data)
        {
            var canvas = GetComponentInParent<Canvas>();
            Owner.Resize(data.delta.y / canvas.scaleFactor);
        }
    }
}
