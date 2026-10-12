using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    public static class GameplayHudLayoutBuilder
    {
        private static Font font;
        private static readonly Color Cream = new Color(.94f, .93f, .85f);
        private static readonly Color Muted = new Color(.56f, .65f, .67f);
        private static readonly Color Cyan = new Color(.46f, .84f, .81f);
        private static readonly Color Red = new Color(.96f, .29f, .31f);

        public static GameplayHudView Build()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            if (!font) throw new InvalidOperationException("Gameplay Korean font missing.");
            var canvas = UiElements.Canvas("HUD"); var view = canvas.gameObject.AddComponent<GameplayHudView>();
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var status = UiElements.Label("Session Status", canvas.transform, "", Vector2.zero, Vector2.zero);
            view.status = status; status.enabled = false;
            var header = Panel("Training Header", canvas.transform, new Color(.025f,.035f,.045f,.96f), 0,.925f,1,1);
            Label("Dojo Brand", header, "RHYTHM DOJO  /  리듬 도장", .03f,0,.55f,1,18);
            Label("Training Caption", header, "집중  ·  반복  ·  완주", .72f,0,.97f,1,15).alignment = TextAnchor.MiddleRight;
            Panel("Header Red Line", header, Red,.03f,0,.25f,.035f);

            var song = Card("Training Information", canvas.transform,.03f,.58f,.26f,.88f,Cyan);
            Label("Song Caption", song,"지금 수련 중인 곡",.07f,.82f,.93f,.97f,14).color = Muted;
            view.songTitle = Label("Song Title",song,"",.07f,.48f,.93f,.80f,25);
            view.songTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.songDetails = Label("Song Details",song,"",.07f,.24f,.93f,.47f,14);
            view.songDetails.color = Muted;
            view.sessionState = Label("Training State",song,"",.07f,.04f,.93f,.23f,16);
            view.sessionState.color = Cyan;
            var record = Card("Judgment Record",canvas.transform,.03f,.29f,.26f,.55f,Muted);
            Label("Record Caption",record,"판정 기록",.07f,.78f,.93f,.96f,14).color = Muted;
            view.counts = Label("Judgment Counts",record,"",.07f,.30f,.93f,.76f,18);
            view.accuracy = Label("Accuracy",record,"",.07f,.04f,.93f,.28f,17);
            view.accuracy.color = Cyan;

            var feedback = Card("Rhythm Feedback",canvas.transform,.74f,.22f,.97f,.88f,Red);
            Label("Combo Caption",feedback,"C O M B O",.06f,.85f,.94f,.97f,16).alignment=TextAnchor.MiddleCenter;
            view.combo = Label("Combo Value",feedback,"0",.04f,.56f,.96f,.87f,76);
            view.combo.alignment=TextAnchor.MiddleCenter;
            view.bestCombo = Label("Best Combo",feedback,"BEST  0",.06f,.49f,.94f,.57f,13);
            view.bestCombo.alignment=TextAnchor.MiddleCenter; view.bestCombo.color=Muted;
            Panel("Feedback Divider",feedback,new Color(.20f,.27f,.29f),.12f,.44f,.88f,.444f);
            var judgmentRoot = new GameObject("Judgment Feedback",typeof(RectTransform),typeof(CanvasGroup));
            judgmentRoot.transform.SetParent(feedback,false); Bounds((RectTransform)judgmentRoot.transform,0,.04f,1,.30f);
            view.judgmentGroup=judgmentRoot.GetComponent<CanvasGroup>();
            view.judgmentGroup.blocksRaycasts=false; view.judgmentGroup.interactable=false;
            view.judgment = Label("Judgment Grade",judgmentRoot.transform,"READY",.02f,.43f,.98f,.96f,31);
            view.judgment.alignment=TextAnchor.MiddleCenter; view.judgment.color=Cyan;
            view.timing = Label("Judgment Timing",judgmentRoot.transform,"박자에 집중하세요",.03f,0,.97f,.45f,15);
            view.timing.alignment=TextAnchor.MiddleCenter; view.timing.color=Muted;

            var track = Panel("Resolved Track",canvas.transform,new Color(.10f,.15f,.17f),.03f,.22f,.26f,.225f);
            view.progressFill=Panel("Resolved Fill",track,Cyan,0,0,0,1).GetComponent<Image>();
            view.progress=Label("Resolved Notes",canvas.transform,"",.03f,.165f,.26f,.215f,13); view.progress.color=Muted;
            Label("Restart Hint",canvas.transform,"SPACE  시작 / 다시 도전",.03f,.075f,.28f,.115f,14).color=Muted;
            Label("Return Hint",canvas.transform,"ESC  곡 선택으로",.03f,.025f,.28f,.065f,14).color=Muted;
            var holdHint=Label("Hold Hint",canvas.transform,"긴 노트는 끝까지 누르세요",.74f,.025f,.97f,.075f,14);
            holdHint.color=Muted; holdHint.alignment=TextAnchor.MiddleRight;

            var laneRoot = new GameObject("Lane Feedback", typeof(RectTransform)).GetComponent<RectTransform>();
            laneRoot.SetParent(canvas.transform, false); laneRoot.anchorMin = Vector2.zero; laneRoot.anchorMax = Vector2.one;
            laneRoot.offsetMin = laneRoot.offsetMax = Vector2.zero;
            view.lanes = canvas.gameObject.AddComponent<HudLaneLayout>(); view.lanes.laneRoot = laneRoot;
            view.returnButton = UiElements.Button("Song List", canvas.transform, "곡 선택으로  /  ESC", Vector2.zero, Vector2.zero);
            Bounds((RectTransform)view.returnButton.transform,.74f,.12f,.97f,.19f);
            view.returnButton.GetComponent<Image>().color=new Color(.17f,.07f,.09f);
            var outline=view.returnButton.gameObject.AddComponent<Outline>(); outline.effectColor=Red; outline.effectDistance=new Vector2(1,-1);
            var caption=view.returnButton.GetComponentInChildren<Text>(); caption.font=font; caption.fontSize=16; caption.color=Cream;
            caption.verticalOverflow=VerticalWrapMode.Overflow;
            return view;
        }
        private static RectTransform Card(string name,Transform parent,float x0,float y0,float x1,float y1,Color accent)
        {
            var rect=Panel(name,parent,new Color(.035f,.05f,.065f,.92f),x0,y0,x1,y1);
            Panel(name+" Accent",rect,accent,0,.992f,1,1); return rect;
        }
        private static RectTransform Panel(string name,Transform parent,Color color,float x0,float y0,float x1,float y1)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false;
            var rect=(RectTransform)go.transform; Bounds(rect,x0,y0,x1,y1); return rect;
        }
        private static Text Label(string name,Transform parent,string text,float x0,float y0,float x1,float y1,int size)
        {
            var label=UiElements.Label(name,parent,text,Vector2.zero,Vector2.zero,size);
            label.font=font; label.color=Cream; label.alignment=TextAnchor.MiddleLeft;
            label.verticalOverflow=VerticalWrapMode.Overflow; Bounds(label.rectTransform,x0,y0,x1,y1); return label;
        }
        private static void Bounds(RectTransform rect,float x0,float y0,float x1,float y1)
        {
            rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1); rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
