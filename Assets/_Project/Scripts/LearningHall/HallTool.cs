using Autohand;
using UnityEngine;

namespace ShuQi.VR
{
    [RequireComponent(typeof(Grabbable), typeof(Rigidbody))]
    public sealed class HallTool : MonoBehaviour
    {
        public string toolName;
        public Transform home;
        public Renderer slotRenderer;
        public LearningHallController hall;
        public Grabbable Grab { get; private set; }
        public bool AtHome { get; private set; }
        public bool TouchedThisAttempt { get; private set; }
        public Quaternion PickRotation { get; private set; }
        public bool RotatedThisAttempt { get; private set; }
        public bool HandedThisAttempt { get; private set; }
        Rigidbody body;
        Hand handoverCandidate;
        float stableTime, rotationTime;
        bool placedNotified;
        Color slotColor;

        void Awake()
        {
            Grab=GetComponent<Grabbable>(); body=GetComponent<Rigidbody>();
            Grab.OnBeforeGrabEvent+=OnBeforeGrab; Grab.OnGrabEvent+=OnGrab; Grab.OnReleaseEvent+=OnRelease;
            if(slotRenderer) { slotColor=slotRenderer.material.color; }
        }
        void OnDestroy()
        {
            if(Grab) { Grab.OnBeforeGrabEvent-=OnBeforeGrab; Grab.OnGrabEvent-=OnGrab; Grab.OnReleaseEvent-=OnRelease; }
            if(slotRenderer) Destroy(slotRenderer.material);
        }
        void OnBeforeGrab(Hand hand, Grabbable grab)
        {
            handoverCandidate=null;
            foreach(var holder in grab.GetHeldBy()) if(holder!=hand) handoverCandidate=hand;
        }
        void OnGrab(Hand hand, Grabbable grab)
        {
            AtHome=false; stableTime=0; placedNotified=false;
            if(hall) hall.SetNotice("当前器械："+toolName+"。握持键松开即可释放；请放回同名归位区。");
            if(!hall || !hall.PracticeActive || hall.PracticeTool!=this) return;
            if(!TouchedThisAttempt) PickRotation=transform.rotation;
            TouchedThisAttempt=true;
            if(handoverCandidate==hand) HandedThisAttempt=true;
            handoverCandidate=null;
            hall.NotifyTool(this);
        }
        void OnRelease(Hand hand, Grabbable grab) { stableTime=0; }
        public void BeginAttempt()
        {
            TouchedThisAttempt=RotatedThisAttempt=HandedThisAttempt=false;
            handoverCandidate=null; placedNotified=false; stableTime=rotationTime=0;
        }
        public bool ResetTool()
        {
            if(!Grab) Grab=GetComponent<Grabbable>();
            if(Grab.IsHeld() || Grab.beingGrabbed) return false;
            if(!body) body=GetComponent<Rigidbody>();
            transform.SetPositionAndRotation(home.position,home.rotation);
            body.position=home.position; body.rotation=home.rotation;
            body.velocity=body.angularVelocity=Vector3.zero;
            AtHome=true; stableTime=0; placedNotified=true;
            return true;
        }
        void Update()
        {
            if(!home || HallRigUI.GameplayPaused) return;
            bool held=Grab.IsHeld() || Grab.beingGrabbed;
            if(!held && (transform.position.y<-.35f || Mathf.Abs(transform.position.x)>3 || transform.position.z< -2.8f || transform.position.z>3))
            { ResetTool(); if(hall) hall.SetNotice(toolName+"已自动找回；练习记录未清空。"); return; }
            if(held && hall && hall.PracticeActive && hall.PracticeTool==this && TouchedThisAttempt)
            {
                bool matches=Quaternion.Angle(transform.rotation,hall.rotationTarget.rotation)<30 && Quaternion.Angle(transform.rotation,PickRotation)>45;
                rotationTime=matches ? rotationTime+Time.deltaTime : 0;
                if(rotationTime>.35f && !RotatedThisAttempt) { RotatedThisAttempt=true; hall.NotifyTool(this); }
            }
            bool near=Near(home);
            bool resting=!held && body.velocity.magnitude<.15f && body.angularVelocity.magnitude<1;
            stableTime=resting?stableTime+Time.deltaTime:0;
            AtHome=near && stableTime>.4f;
            if(slotRenderer) slotRenderer.material.color=AtHome?new Color(.12f,.35f,.30f):held?new Color(.65f,.4f,.1f):slotColor;
            if(stableTime>.4f && !placedNotified && hall)
            {
                if(AtHome)
                {
                    placedNotified=true;
                    if(TouchedThisAttempt) hall.NotifyPlaced(this);
                    else hall.SetNotice(toolName+"已放回同名归位区。");
                }
                else foreach(var other in hall.tools) if(other!=this && Near(other.home))
                { placedNotified=true; hall.SetNotice("此处是"+other.toolName+"归位区，请将"+toolName+"放回同名区域。"); break; }
            }
        }
        bool Near(Transform target) => Vector2.Distance(new Vector2(transform.position.x,transform.position.z),new Vector2(target.position.x,target.position.z))<.11f && Mathf.Abs(transform.position.y-target.position.y)<.06f;
    }
}
