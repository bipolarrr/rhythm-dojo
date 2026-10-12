using System;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;

namespace RhythmDojo.Presentation
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameplayHitFeedback : MonoBehaviour
    {
        private const int PoolSize = 24;
        private static readonly Color Perfect = new Color(.46f,.91f,.85f);
        private static readonly Color Good = new Color(1,.77f,.42f);
        private static readonly Color Miss = new Color(.96f,.29f,.31f);
        private IGameplayReadModel model;
        private GameModeDefinition mode;
        private Camera targetCamera;
        private Canvas canvas;
        private Vector3 cameraOrigin;
        private GameplayImpactGraphic[] bursts;
        private GameplayImpactGraphic[] holdGlows;
        private int[] holdNotes;
        private int nextBurst;
        private int lastSoundFrame = -1;
        private AudioSource source;
        private AudioClip perfectSound;
        private AudioClip goodSound;
        private double kickAt = -10;
        private float kickStrength;
        private RectTransform effectsRoot;

        public void Initialize(IGameplayReadModel readModel, GameModeDefinition gameMode, Camera camera, Canvas hudCanvas)
        {
            Unbind();
            if (readModel == null || !gameMode || !camera || !hudCanvas || readModel.LaneCount != gameMode.LaneCount)
                throw new InvalidOperationException("Hit feedback dependencies missing or lane counts differ.");
            model = readModel; mode = gameMode; targetCamera = camera; canvas = hudCanvas; cameraOrigin = camera.transform.position;
            source = GetComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            source.volume = .25f; source.priority = 96;
            perfectSound = Resources.Load<AudioClip>("UI/DojoHitPerfect"); goodSound = Resources.Load<AudioClip>("UI/DojoHitGood");
            if (!perfectSound || !goodSound) throw new InvalidOperationException("Dojo hit sounds missing.");
            if (effectsRoot) { effectsRoot.gameObject.SetActive(false); Destroy(effectsRoot.gameObject); }
            effectsRoot = new GameObject("Gameplay Impacts",typeof(RectTransform)).GetComponent<RectTransform>();
            effectsRoot.SetParent(canvas.transform,false); effectsRoot.anchorMin=Vector2.zero; effectsRoot.anchorMax=Vector2.one;
            effectsRoot.offsetMin=effectsRoot.offsetMax=Vector2.zero;
            bursts=new GameplayImpactGraphic[PoolSize]; holdGlows=new GameplayImpactGraphic[mode.LaneCount];
            holdNotes=new int[mode.LaneCount];
            for(int i=0;i<PoolSize;i++) bursts[i]=CreateGraphic("Hit Burst "+i);
            for(int i=0;i<mode.LaneCount;i++) {holdGlows[i]=CreateGraphic("Hold Glow "+i);holdGlows[i].Lane=i;}
            model.Judged+=OnJudged; model.HoldStarted+=OnHoldStarted; model.ResetOccurred+=ResetFeedback;
            ResetFeedback();
        }
        private GameplayImpactGraphic CreateGraphic(string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(GameplayImpactGraphic));go.transform.SetParent(effectsRoot,false);
            var graphic=go.GetComponent<GameplayImpactGraphic>();graphic.raycastTarget=false;
            graphic.rectTransform.anchorMin=graphic.rectTransform.anchorMax=new Vector2(.5f,.5f);
            go.SetActive(false);return graphic;
        }
        private void OnHoldStarted(HoldStartedEvent result)
        {
            holdNotes[result.Head.Lane]=result.Head.NoteIndex;
            holdGlows[result.Head.Lane].Sustain(result.Head.Grade==Judgment.Perfect ? Perfect : Good);
            Impact(result.Head,1);
        }
        private void OnJudged(JudgmentEvent result)
        {
            if(result.Kind==NoteKind.Hold) {holdNotes[result.Lane]=-1;holdGlows[result.Lane].gameObject.SetActive(false);}
            Impact(result,result.Kind==NoteKind.Hold ? .7f : 1);
        }
        private void Impact(JudgmentEvent result,float strength)
        {
            var graphic=bursts[nextBurst];nextBurst=(nextBurst+1)%PoolSize;graphic.Lane=result.Lane;
            graphic.Play(result.Grade==Judgment.Perfect ? Perfect : result.Grade==Judgment.Good ? Good : Miss,
                result.Grade==Judgment.Miss ? .35f : strength,result.Grade==Judgment.Miss);
            Position(graphic);
            if(result.Grade==Judgment.Miss) return;
            kickAt=Time.unscaledTimeAsDouble;kickStrength=Mathf.Min(.025f,.025f*strength);
            // A chord gets one attack sound so six simultaneous lanes cannot multiply the volume.
            if(lastSoundFrame==Time.frameCount) return;
            lastSoundFrame=Time.frameCount;
            source.PlayOneShot(result.Grade==Judgment.Perfect ? perfectSound : goodSound,strength);
        }
        private void LateUpdate()
        {
            if(model==null) return;
            float age=(float)(Time.unscaledTimeAsDouble-kickAt);
            float envelope=Mathf.Clamp01(1-age/.10f);
            targetCamera.transform.position=cameraOrigin+Vector3.up*(kickStrength*envelope*envelope*Mathf.Sin(age/.10f*Mathf.PI*2));
            foreach(var graphic in bursts) if(graphic.gameObject.activeSelf) Position(graphic);
            for(int i=0;i<holdGlows.Length;i++)
            {
                bool holding=holdNotes[i]>=0 && model.GetNoteState(holdNotes[i])==NoteState.Holding;
                if(!holding) {if(holdGlows[i])holdGlows[i].gameObject.SetActive(false);holdNotes[i]=-1;}
                else Position(holdGlows[i]);
            }
        }
        private void Position(GameplayImpactGraphic graphic)
        {
            var rect=(RectTransform)canvas.transform;
            var world=new Vector3(mode.LaneX(graphic.Lane),.12f,0);
            var screen=targetCamera.WorldToScreenPoint(world);
            var uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,uiCamera,out var point);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,targetCamera.WorldToScreenPoint(world+Vector3.right*mode.LaneSpacing),uiCamera,out var edge);
            graphic.rectTransform.anchoredPosition=point;
            graphic.rectTransform.sizeDelta=new Vector2(Mathf.Abs(edge.x-point.x)*.9f,140);
        }
        private void ResetFeedback()
        {
            nextBurst=0;lastSoundFrame=-1;kickAt=-10;kickStrength=0;
            if(targetCamera)targetCamera.transform.position=cameraOrigin;
            if(source)source.Stop();
            if(bursts!=null)foreach(var graphic in bursts)if(graphic)graphic.gameObject.SetActive(false);
            if(holdGlows!=null)for(int i=0;i<holdGlows.Length;i++){if(holdGlows[i])holdGlows[i].gameObject.SetActive(false);holdNotes[i]=-1;}
        }
        public void Unbind()
        {
            if(model!=null) {model.Judged-=OnJudged;model.HoldStarted-=OnHoldStarted;model.ResetOccurred-=ResetFeedback;}
            ResetFeedback();model=null;
        }
        private void OnDisable()=>Unbind();
        private void OnDestroy()=>Unbind();
    }
}
