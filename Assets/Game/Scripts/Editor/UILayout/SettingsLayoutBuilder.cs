using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    public static class SettingsLayoutBuilder
    {
        private static Font font;
        private static readonly Color Ink = new Color(.025f, .035f, .045f);
        private static readonly Color Card = new Color(.055f, .068f, .08f);
        private static readonly Color Red = new Color(.94f, .25f, .26f);
        private static readonly Color Cyan = new Color(.46f, .78f, .76f);

        public static SettingsView Build()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            if (!font) throw new InvalidOperationException("Settings Korean font missing.");
            var canvas = UiElements.Canvas("Settings"); var view = canvas.gameObject.AddComponent<SettingsView>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var wall = Panel("Dojo Wall", canvas.transform, Ink, 0, 0, 1, 1);
            var architecture = new GameObject("Dojo Architecture", typeof(RectTransform), typeof(DojoBackdropGraphic));
            architecture.transform.SetParent(wall, false); Bounds((RectTransform)architecture.transform,0,0,1,1);
            architecture.GetComponent<DojoBackdropGraphic>().raycastTarget = false;
            var header = Panel("Header", canvas.transform, new Color(.035f,.045f,.055f),0,.92f,1,1);
            Label("Brand", header, "RHYTHM DOJO  /  리듬 도장", .025f,0,.36f,1,25);
            Label("Section", header, "설정  /  SETTINGS", .50f,0,.78f,1,23);
            Panel("Header Accent", header, Red,.025f,0,.325f,.035f);
            Label("Title", canvas.transform, "수련 환경 설정", .08f,.82f,.70f,.90f,40);
            Label("Subtitle", canvas.transform, "나에게 맞는 오디오 응답으로 박자를 단련하세요.", .08f,.77f,.92f,.82f,22).color = new Color(.65f,.72f,.73f);

            var controls = Panel("Buffer Controls", canvas.transform, Card,.08f,.42f,.47f,.75f);
            Panel("Control Accent", controls, Red,0,.98f,1,1);
            Label("Buffer Label", controls, "01  /  오디오 버퍼", .045f,.75f,.95f,.94f,27);
            Label("Buffer Description", controls, "Unity DSP buffer size", .045f,.60f,.95f,.75f,20).color = new Color(.65f,.72f,.73f);
            view.bufferList = UiElements.Dropdown("DSP Buffer", controls, Vector2.zero, Vector2.zero);
            Bounds((RectTransform)view.bufferList.transform,.065f,.31f,.935f,.56f);
            view.bufferList.GetComponent<Image>().color = new Color(.11f,.14f,.16f);
            foreach (var text in view.bufferList.GetComponentsInChildren<Text>(true))
            { text.font=font; text.fontSize=22; text.color=Color.white; text.verticalOverflow=VerticalWrapMode.Overflow; }
            foreach (var image in view.bufferList.template.GetComponentsInChildren<Image>(true))
                image.color = new Color(.11f,.14f,.16f);
            view.bufferList.captionText.rectTransform.offsetMin = new Vector2(20,4);
            view.bufferList.captionText.rectTransform.offsetMax = new Vector2(-42,-4);
            var arrow = view.bufferList.transform.Find("Arrow");
            if (arrow) arrow.GetComponent<Image>().enabled = false;
            var chevron = Label("Expand Indicator", view.bufferList.transform, "▾", .90f,0,.99f,1,24);
            chevron.alignment = TextAnchor.MiddleCenter; chevron.color = Cyan;
            chevron.rectTransform.offsetMin = chevron.rectTransform.offsetMax = Vector2.zero;
            view.bufferList.template.sizeDelta = new Vector2(view.bufferList.template.sizeDelta.x, 260);
            var dropdownItem = view.bufferList.template.Find("Viewport/Content/Item") as RectTransform;
            if (dropdownItem) dropdownItem.sizeDelta = new Vector2(dropdownItem.sizeDelta.x, 48);
            var dropdownContent = view.bufferList.template.Find("Viewport/Content") as RectTransform;
            if (dropdownContent) dropdownContent.sizeDelta = new Vector2(dropdownContent.sizeDelta.x, 48);
            Label("Apply Hint", controls, "값을 선택한 뒤 ‘적용’을 누르세요.", .045f,.08f,.95f,.25f,19).color = new Color(.65f,.72f,.73f);

            var monitor = Panel("Audio Monitor",canvas.transform,Card,.50f,.42f,.92f,.75f);
            Panel("Monitor Accent",monitor,Cyan,0,.98f,1,1);
            Label("Monitor Label",monitor,"02  /  현재 오디오 상태",.045f,.75f,.95f,.94f,27);
            view.actualState = Label("Actual Audio State", monitor, "", .045f,.08f,.95f,.69f,25);
            view.actualState.color = new Color(.78f,.87f,.85f);
            var feedback = Panel("Status Panel",canvas.transform,new Color(.07f,.085f,.10f),.08f,.32f,.92f,.39f);
            Panel("Status Accent",feedback,Cyan,0,0,.003f,1);
            view.status = Label("Audio Status",feedback,"",.015f,0,.985f,1,19);
            var guidance = Panel("Practice Guidance",canvas.transform,Card,.08f,.13f,.92f,.29f);
            Label("Guidance Heading",guidance,"BUFFER GUIDE / 버퍼 안내",.015f,.65f,.98f,.95f,18).color = Cyan;
            Label("Help", guidance,
                "작은 버퍼는 응답이 빨라지지만 소리가 끊길 수 있습니다. Unity가 지원하는 크기로 조정될 수 있습니다.\n" +
                "Default는 앱 시작 시의 버퍼 크기를 복원합니다. 선택한 값은 앱이 실행되는 동안 유지됩니다.\n" +
                "이 설정은 Unity 믹서 버퍼에 적용되며, ASIO 드라이버의 버퍼를 변경하지 않습니다.",
                .015f,.05f,.98f,.66f,19).color = new Color(.65f,.72f,.73f);

            var footer = Panel("Footer",canvas.transform,Ink,0,0,1,.085f);
            Label("Training Philosophy",footer,"집중    /    반복    /    완주",.07f,.1f,.50f,.9f,19).color = new Color(.55f,.64f,.66f);
            view.apply = Button("Apply",footer,"적용",.62f,.16f,.76f,.84f,true);
            view.back = Button("Back",footer,"뒤로  →",.78f,.16f,.92f,.84f,false);
            var bufferNavigation = view.bufferList.navigation;
            bufferNavigation.mode = Navigation.Mode.Explicit; bufferNavigation.selectOnDown=view.apply;
            view.bufferList.navigation=bufferNavigation;
            var applyNavigation = view.apply.navigation;
            applyNavigation.mode=Navigation.Mode.Explicit; applyNavigation.selectOnUp=view.bufferList;
            applyNavigation.selectOnRight=view.back; view.apply.navigation=applyNavigation;
            var backNavigation = view.back.navigation;
            backNavigation.mode=Navigation.Mode.Explicit; backNavigation.selectOnUp=view.bufferList;
            backNavigation.selectOnLeft=view.apply; view.back.navigation=backNavigation;
            view.Validate(); return view;
        }

        private static RectTransform Panel(string name, Transform parent, Color color, float x0,float y0,float x1,float y1)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(SelectionGradientImage));
            go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false;
            var rect=(RectTransform)go.transform; Bounds(rect,x0,y0,x1,y1); return rect;
        }
        private static Text Label(string name,Transform parent,string caption,float x0,float y0,float x1,float y1,int size)
        {
            var text=UiElements.Label(name,parent,caption,Vector2.zero,Vector2.zero,size);
            text.font=font; text.alignment=TextAnchor.MiddleLeft; text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
            Bounds(text.rectTransform,x0,y0,x1,y1);
            text.rectTransform.offsetMin=new Vector2(12,4); text.rectTransform.offsetMax=new Vector2(-12,-4);
            return text;
        }
        private static Button Button(string name,Transform parent,string caption,float x0,float y0,float x1,float y1,bool primary)
        {
            var rect=Panel(name,parent,new Color(.09f,.115f,.13f),x0,y0,x1,y1);
            var image=rect.GetComponent<SelectionGradientImage>(); image.raycastTarget=true;
            if (primary) { image.color=Color.white; image.useGradient=true; image.leftColor=new Color(.82f,.15f,.19f); image.rightColor=new Color(.55f,.075f,.12f); }
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var text=Label("Caption",rect,caption,0,0,1,1,24); text.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        private static void Bounds(RectTransform rect,float x0,float y0,float x1,float y1)
        {
            rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1);
            rect.pivot=new Vector2(.5f,.5f); rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
