using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class WorkspaceMenuPopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject panel;
        public Button trigger;
        public WorkspaceView owner;
        private bool pinned;

        private void Awake()
        {
            trigger.onClick.AddListener(() =>
            {
                if (pinned)
                {
                    owner.CloseMenus();
                    return;
                }

                owner.OpenMenu(this);
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!owner.HasOpenMenu)
            {
                return;
            }

            owner.OpenMenu(this);
        }

        public void Open()
        {
            pinned = true;
            panel.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
        }

        public void Close()
        {
            pinned = false;
            panel.SetActive(false);
        }
    }
}
