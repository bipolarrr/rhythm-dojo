using UnityEngine;

namespace RhythmDojo.UI
{
    public enum UiTextRole
    {
        Body,
        Title,
        Help,
    }

    [DisallowMultipleComponent]
    public sealed class UiTextStyle : MonoBehaviour
    {
        public UiTextRole role = UiTextRole.Body;
        public Font customFont;
        public FontStyle customStyle = FontStyle.Normal;
        [SerializeField, HideInInspector] internal Font appliedFont;
    }
}
