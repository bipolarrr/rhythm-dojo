using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class AudioTimelineTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Text Target { get; set; }
        public string Caption { get; set; }

        public void OnPointerEnter(PointerEventData data)
        {
            Target.text = Caption;
        }

        public void OnPointerExit(PointerEventData data)
        {
            Target.text = "";
        }
    }
}
