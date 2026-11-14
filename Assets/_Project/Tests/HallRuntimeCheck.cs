#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autohand;
using ShuQi.VR;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public sealed class HallRuntimeCheck : MonoBehaviour
{
    static readonly string Work=Path.GetFullPath("Temp/LearningHallChecks");
    const string Marker="Temp/HallRuntimeCheck.active";
    XRSimulatedController leftDevice,rightDevice;
    XRSimulatedHMD headDevice;
    XRSimulatedControllerState left,right;
    XRSimulatedHMDState head;
    HallRigUI rig;
    LearningHallController hall;
    readonly List<string> passed=new List<string>(), errors=new List<string>();
    HallProgress oldProgress;
    List<TrainingRecord> oldRecords;
    string stage="initialization";
    bool finished;
    float startedTime;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        if(File.Exists(Marker) && !FindObjectOfType<HallRuntimeCheck>()) new GameObject("Temporary hall runtime verification").AddComponent<HallRuntimeCheck>();
    }
    void Awake() { startedTime=Time.realtimeSinceStartup; DontDestroyOnLoad(gameObject); Directory.CreateDirectory(Work); File.WriteAllText(Work+"/runtime-progress.txt",""); File.WriteAllText(Work+"/runtime-errors.txt",""); File.WriteAllText(Work+"/runtime-result.txt","RUNNING"); Application.logMessageReceived+=OnLog; }
    void Update()
    {
        if(!finished && Time.realtimeSinceStartup-startedTime>150)
        {
            File.WriteAllText(Work+"/runtime-result.txt","FAIL timeout at "+stage);
            finished=true; EditorApplication.isPlaying=false;
        }
    }
    void OnLog(string message,string stack,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        errors.Add(stage+": "+message); File.WriteAllLines(Work+"/runtime-errors.txt",errors);
    }
    void Check(bool value,string message)
    {
        if(value) return;
        File.WriteAllText(Work+"/runtime-result.txt","FAIL "+stage+"\n"+message);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        throw new Exception(message);
    }
    void Pass(string name) { passed.Add(name); File.WriteAllLines(Work+"/runtime-progress.txt",passed); }
    void Capture(string name,bool showHands=false)
    {
        Canvas.ForceUpdateCanvases();
        var camera=new GameObject("Temporary UI verification camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0,1.65f,-.65f),Quaternion.identity);
        if(showHands) camera.transform.SetPositionAndRotation(rig.player.headCamera.transform.position,rig.player.headCamera.transform.rotation);
        camera.fieldOfView=78; camera.nearClipPlane=.04f; camera.farClipPlane=20;
        var target=new RenderTexture(1440,1000,24); var texture=new Texture2D(1440,1000,TextureFormat.RGB24,false); var previous=RenderTexture.active;
        var handRenderers=rig.player.handLeft.GetComponentsInChildren<Renderer>().Concat(rig.player.handRight.GetComponentsInChildren<Renderer>()).Where(r=>r.enabled).ToArray();
        try {
            if(!showHands) foreach(var renderer in handRenderers) renderer.enabled=false;
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1440,1000),0,0); texture.Apply(); File.WriteAllBytes(Work+"/ui-"+name+".png",texture.EncodeToPNG());
        }
        finally { foreach(var renderer in handRenderers) if(renderer) renderer.enabled=true; RenderTexture.active=previous; camera.targetTexture=null; target.Release(); Destroy(target); Destroy(texture); Destroy(camera.gameObject); }
    }
    Button Button(string name)=>hall.uiRoot.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
    void Queue() { InputSystem.QueueStateEvent(headDevice,head); InputSystem.QueueStateEvent(leftDevice,left); InputSystem.QueueStateEvent(rightDevice,right); }
    void Grip(bool isLeft,bool pressed)
    {
        if(isLeft) { left.grip=pressed?1:0; left=left.WithButton(ControllerButton.GripButton,pressed); }
        else { right.grip=pressed?1:0; right=right.WithButton(ControllerButton.GripButton,pressed); }
        Queue();
    }
    void Trigger(bool pressed,bool isLeft=false)
    {
        if(isLeft) { left.trigger=pressed?1:0; left=left.WithButton(ControllerButton.TriggerButton,pressed); }
        else { right.trigger=pressed?1:0; right=right.WithButton(ControllerButton.TriggerButton,pressed); }
        Queue();
    }
    void Pose(bool isLeft,Vector3 world,Quaternion rotation)
    {
        var controller=isLeft?rig.leftController:rig.rightController;
        Vector3 p=controller.parent.InverseTransformPoint(world); Quaternion r=Quaternion.Inverse(controller.parent.rotation)*rotation;
        if(isLeft) { left.devicePosition=p; left.deviceRotation=r; } else { right.devicePosition=p; right.deviceRotation=r; }
        Queue();
    }
    IEnumerator FindRig()
    {
        foreach(var simulator in FindObjectsOfType<XRDeviceSimulator>()) simulator.gameObject.SetActive(false);
        rig=FindObjectOfType<HallRigUI>(); hall=FindObjectOfType<LearningHallController>();
        Check(rig,"Rig UI missing");
        float offset=rig.player.trackingContainer.localPosition.y;
        head=new XRSimulatedHMDState(); head.Reset(); head.isTracked=true; head.trackingState=3;
        head.centerEyePosition=head.devicePosition=new Vector3(0,1.65f-offset,0);
        left=new XRSimulatedControllerState {isTracked=true,trackingState=3,devicePosition=new Vector3(-.3f,1.15f-offset,.35f),deviceRotation=Quaternion.identity};
        right=new XRSimulatedControllerState {isTracked=true,trackingState=3,devicePosition=new Vector3(.3f,1.15f-offset,.35f),deviceRotation=Quaternion.identity};
        Queue(); yield return new WaitForSeconds(.6f);
        Check(FindObjectsOfType<EventSystem>().Length==1,"Duplicate EventSystem after load");
    }
    IEnumerator Aim(Button target,bool isLeft=false)
    {
        Vector3 position=rig.player.headCamera.transform.position+new Vector3(isLeft?-.2f:.2f,-.25f,.4f);
        var controller=isLeft?rig.leftController:rig.rightController;
        Pose(isLeft,position,Quaternion.identity); yield return new WaitForSeconds(.2f);
        for(int i=0;i<5;i++)
        {
            var correction=Quaternion.FromToRotation(rig.pointer.transform.forward,target.transform.position-rig.pointer.transform.position);
            Pose(isLeft,position,correction*controller.rotation); yield return new WaitForSeconds(.15f);
        }
        Check(rig.pointer.currTarget && (rig.pointer.currTarget==target.gameObject || rig.pointer.currTarget.transform.IsChildOf(target.transform)),"UI ray did not hit "+target.name+" (hit "+(rig.pointer.currTarget?rig.pointer.currTarget.name:"nothing")+")");
    }
    IEnumerator CheckFingerRay(bool isLeft)
    {
        yield return new WaitForEndOfFrame();
        var finger=(isLeft?rig.player.handLeft:rig.player.handRight).fingers.Single(f=>f.fingerType==FingerEnum.index);
        var joints=new[]{finger.knuckleJoint,finger.middleJoint,finger.distalJoint};
        var rotations=joints.Select(j=>j.rotation).ToArray();
        Vector3 origin,direction;
        // Reproduce the previous pointing pose only in the fixture, then restore the live gesture.
        try {
            finger.poseData[(int)FingerPoseEnum.Open].SetFingerPose(finger);
            origin=finger.tip.position; direction=(finger.tip.position-finger.distalJoint.position).normalized;
        }
        finally { for(int i=0;i<joints.Length;i++) joints[i].rotation=rotations[i]; }
        var line=rig.pointer.GetComponent<LineRenderer>();
        float error=Vector3.Distance(rig.pointer.transform.position,origin);
        Check(rig.pointer.finger==finger && error<.001f,"UI origin moved from the original palm-relative anchor");
        Check(Vector3.Dot(rig.pointer.transform.forward,direction)>.999f,"UI direction changed with the finger gesture");
        Check(line.enabled && Vector3.Distance(line.GetPosition(0),origin)<.001f,"Visible ray does not start at the fixed anchor");
        Check(Vector3.Dot((line.GetPosition(line.positionCount-1)-line.GetPosition(0)).normalized,direction)>.999f,"Visible ray diverges from the fixed direction");
        Check(Vector3.Distance(line.GetPosition(line.positionCount-1),rig.pointer.hitPointMarker.transform.position)<.001f,"Visible ray stops before the UI cursor");
        File.AppendAllText(Work+"/fingertip-result.txt",(isLeft?"Left":"Right")+" anchor error="+error+"m, angle="+Vector3.Angle(rig.pointer.transform.forward,direction)+"deg\n");
    }
    IEnumerator Click(Button target,bool isLeft=false)
    {
        yield return Aim(target,isLeft); yield return CheckFingerRay(isLeft); int clicks=0; UnityEngine.Events.UnityAction count=()=>clicks++; target.onClick.AddListener(count);
        var finger=rig.pointer.finger;
        Vector3 relaxedTip=finger.hand.transform.InverseTransformPoint(finger.tip.position);
        Trigger(true,isLeft); yield return new WaitForSeconds(.25f); yield return CheckFingerRay(isLeft);
        float curl=Vector3.Distance(relaxedTip,finger.hand.transform.InverseTransformPoint(finger.tip.position));
        Check(curl>.005f,"UI pointer prevented the natural trigger-driven finger gesture");
        File.AppendAllText(Work+"/fingertip-result.txt",(isLeft?"Left":"Right")+" natural finger curl="+curl+"m\n");
        if(target.name=="ModeIndependent") Capture(isLeft?"natural-hand-left-pressed":"natural-hand-right-pressed",true);
        var action=(InputAction)typeof(HallRigUI).GetField(isLeft?"leftTrigger":"rightTrigger",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig);
        var data=FindObjectOfType<AutoInputModule>().GetData(0);
        File.WriteAllText(Work+"/ui-press-state.txt","target="+target.name+" action="+action.IsPressed()+" controls="+string.Join(",",action.controls.Select(c=>c.path))+" press="+(data?.pointerPress?data.pointerPress.name:"none")+" ray="+(data?.pointerCurrentRaycast.gameObject?data.pointerCurrentRaycast.gameObject.name:"none")+" pointer="+rig.pointer.enabled+" paused="+HallRigUI.GameplayPaused);
        Trigger(false,isLeft); yield return new WaitForSeconds(.18f);
        File.AppendAllText(Work+"/ui-press-state.txt","\nclicks="+clicks+" mode="+hall.Mode+" session="+(TrainingSession.Current?TrainingSession.Current.Record.mode:"none"));
        target.onClick.RemoveListener(count);
        Check(clicks==1,"A fingertip UI click did not execute exactly once");
    }
    IEnumerator GrabTool(HallTool tool,bool isLeft,bool moveTool=true)
    {
        var hand=isLeft?rig.player.handLeft:rig.player.handRight;
        if(moveTool)
        {
            tool.GetComponent<Rigidbody>().useGravity=false;
            tool.transform.SetPositionAndRotation(hand.palmTransform.position+hand.palmTransform.forward*.08f,Quaternion.LookRotation(hand.palmTransform.right,hand.palmTransform.up));
            tool.GetComponent<Rigidbody>().position=tool.transform.position; tool.GetComponent<Rigidbody>().rotation=tool.transform.rotation; tool.GetComponent<Rigidbody>().velocity=tool.GetComponent<Rigidbody>().angularVelocity=Vector3.zero;
            Physics.SyncTransforms(); yield return new WaitForFixedUpdate();
        }
        else
        {
            var follow=isLeft?rig.leftController:rig.rightController;
            Vector3 offset=follow.position-hand.palmTransform.position;
            Pose(isLeft,tool.transform.position-hand.palmTransform.forward*.04f+offset,follow.rotation);
            yield return new WaitForSeconds(.5f);
        }
        // The fixture teleports a tool; invalidate AutoHand's cached highlight before sending a real grip input.
        hand.highlighter.ClearHighlights(); hand.highlighter.UpdateHighlight(true,true);
        Grip(isLeft,true);
        float until=Time.realtimeSinceStartup+3;
        while((hand.holdingObj!=tool.Grab || !tool.Grab.GetHeldBy().Contains(hand)) && Time.realtimeSinceStartup<until) yield return null;
        File.WriteAllText(Work+"/grab-state.txt",stage+" hand="+hand.palmTransform.position+" tool="+tool.transform.position+" held="+(hand.holdingObj?hand.holdingObj.name:"none"));
        Check(hand.holdingObj==tool.Grab && tool.Grab.GetHeldBy().Contains(hand),"Controller grip did not grab "+tool.name);
    }
    IEnumerator Start()
    {
        HallRecords.Load(); oldProgress=JsonUtility.FromJson<HallProgress>(JsonUtility.ToJson(HallRecords.Progress)); oldRecords=new List<TrainingRecord>(HallRecords.Records);
        InputSystem.RegisterLayout<XRSimulatedHMD>(); InputSystem.RegisterLayout<XRSimulatedController>();
        headDevice=InputSystem.AddDevice<XRSimulatedHMD>(); leftDevice=InputSystem.AddDevice<XRSimulatedController>(); rightDevice=InputSystem.AddDevice<XRSimulatedController>();
        InputSystem.SetDeviceUsage(leftDevice,UnityEngine.InputSystem.CommonUsages.LeftHand); InputSystem.SetDeviceUsage(rightDevice,UnityEngine.InputSystem.CommonUsages.RightHand);
        HallRecords.Progress.leftHanded=false;
        File.WriteAllText(Work+"/fingertip-result.txt","");
        yield return FindRig();
        Capture("home"); hall.ShowCase(); Capture("case"); hall.ShowCalibration(); Capture("calibration"); hall.ShowHome();
        stage="UI mode selection";
        yield return Click(Button("ModeIndependent")); Check(hall.Mode=="自主练习","UI click did not select mode");
        yield return Click(Button("ModeGuided")); Check(hall.Mode=="引导学习","Mode mutual exclusion failed"); Pass(stage);
        Capture("fingertip-right",true);
        stage="UI cancellation";
        yield return Aim(Button("OpenCase")); Trigger(true); yield return new WaitForSeconds(.1f); rig.Pause();
        Trigger(false); yield return new WaitForSeconds(.15f); Check(hall.homePage.activeSelf,"Canceling a UI press triggered its click");
        rig.Resume(); yield return new WaitForSeconds(.25f); Check(!HallRigUI.GameplayPaused,"Pause did not recover after neutral input"); Pass(stage);
        stage="tracking loss release guard";
        right.isTracked=false; Queue(); yield return new WaitForSeconds(.1f); Check(HallRigUI.GameplayPaused,"Tracking loss did not pause");
        right.isTracked=true; right.grip=1; Queue(); yield return new WaitForSeconds(.1f); Check(HallRigUI.GameplayPaused,"Tracking recovery bypassed neutral grip guard");
        right.grip=0; Queue(); yield return new WaitForSeconds(.2f); Check(!HallRigUI.GameplayPaused,"Neutral tracking recovery failed"); Pass(stage);
        stage="teleport cancel and rearm";
        right=right.WithButton(ControllerButton.Primary2DAxisClick,true); Queue(); yield return new WaitForSeconds(.15f);
        Check(rig.teleporter.line.enabled && !rig.pointer.enabled,"Teleport did not take priority over UI");
        rig.Pause(); yield return new WaitForSeconds(.1f); right=right.WithButton(ControllerButton.Primary2DAxisClick,false); Queue();
        yield return new WaitForSeconds(.1f); Check(!rig.teleporter.line.enabled,"Pause left teleport aiming active");
        rig.Resume(); yield return new WaitForSeconds(.2f);
        right=right.WithButton(ControllerButton.Primary2DAxisClick,true); Queue(); yield return new WaitForSeconds(.15f);
        Check(rig.teleporter.line.enabled,"Teleport could not restart after pause");
        rig.teleporter.CancelTeleport(); right=right.WithButton(ControllerButton.Primary2DAxisClick,false); Queue(); yield return new WaitForSeconds(.15f); Pass(stage);
        stage="left fingertip UI preference"; hall.SetHandedness(true); yield return new WaitForSeconds(.1f);
        yield return Click(Button("ModeIndependent"),true); Check(hall.Mode=="自主练习","Left fingertip click failed"); Capture("fingertip-left",true);
        hall.SetHandedness(false); yield return new WaitForSeconds(.1f); yield return Click(Button("ModeGuided")); Pass(stage);
        stage="tool near grabs and follow";
        foreach(var tool in hall.tools)
        {
            yield return GrabTool(tool,false);
            Vector3 before=tool.transform.position; Pose(false,rig.rightController.position+new Vector3(.12f,.08f,0),rig.rightController.rotation); yield return new WaitForSeconds(.35f);
            Check(Vector3.Distance(before,tool.transform.position)>.04f,"Held tool did not follow");
            Grip(false,false); yield return new WaitForSeconds(.15f); Check(!tool.Grab.IsHeld(),"Release failed"); tool.GetComponent<Rigidbody>().useGravity=true; tool.ResetTool();
        }
        Pass(stage);
        stage="held tool pause and release recovery";
        var pauseTool=hall.tools[0]; yield return GrabTool(pauseTool,false);
        rig.Pause(); yield return new WaitForSeconds(.1f); Grip(false,false); yield return new WaitForSeconds(.1f);
        rig.Resume(); yield return new WaitForSeconds(.3f);
        Check(!pauseTool.Grab.IsHeld() && !rig.player.handRight.holdingObj,"Released grip stayed held after pause");
        yield return GrabTool(pauseTool,false); Grip(false,false); yield return new WaitForSeconds(.2f);
        pauseTool.GetComponent<Rigidbody>().useGravity=true; pauseTool.ResetTool(); Pass(stage);
        stage="practice sequence and duplicate counting"; hall.ShowPractice();
        int attempts=HallRecords.Progress.attempts, completions=HallRecords.Progress.completions;
        hall.StartPractice(); hall.StartPractice(); Check(HallRecords.Progress.attempts==attempts+1,"Repeated start counted twice");
        Capture("practice");
        var training=hall.PracticeTool; yield return GrabTool(training,false);
        float height=HallRecords.Progress.benchOffset, heightDelta=height>.2f?-.025f:.025f; hall.AdjustBench(heightDelta); Check(HallRecords.Progress.benchOffset==height,"Bench moved a held tool");
        Grip(false,false); yield return new WaitForSeconds(.2f);
        yield return GrabTool(training,true); Check(!training.HandedThisAttempt,"Dropping and later picking up counted as a handover");
        Grip(true,false); yield return new WaitForSeconds(.2f);
        yield return GrabTool(training,false);
        var delta=hall.rotationTarget.rotation*Quaternion.Inverse(training.transform.rotation);
        Pose(false,rig.rightController.position,delta*rig.rightController.rotation); yield return new WaitForSeconds(.8f);
        File.WriteAllText(Work+"/rotation-state.txt","rotation="+training.transform.eulerAngles+" target="+hall.rotationTarget.eulerAngles+" delta="+Quaternion.Angle(training.transform.rotation,hall.rotationTarget.rotation));
        Check(training.RotatedThisAttempt,"Rotation target was not recognized");
        yield return GrabTool(training,true,false); Check(training.HandedThisAttempt,"Single-handed tool did not swap hands");
        Grip(false,false); Grip(true,false); yield return new WaitForSeconds(.2f);
        var body=training.GetComponent<Rigidbody>(); body.useGravity=true; body.position=training.home.position; body.rotation=training.home.rotation; body.velocity=body.angularVelocity=Vector3.zero; Physics.SyncTransforms();
        yield return new WaitForSeconds(1.2f);
        Check(HallRecords.Progress.completions==completions+1,"Released matching tool did not complete practice");
        hall.NotifyPlaced(training); Check(HallRecords.Progress.completions==completions+1,"Placement completion counted twice");
        hall.ResetTools(); Check(HallRecords.Progress.attempts==attempts+1,"Reset modified attempt count"); Pass(stage);
        stage="height and reset"; hall.AdjustBench(heightDelta); Check(Mathf.Abs(HallRecords.Progress.benchOffset-height-heightDelta)<.0001f,"Unheld bench adjustment failed"); hall.AdjustBench(-heightDelta);
        training.transform.position=new Vector3(4,-1,0); body.position=training.transform.position; yield return new WaitForSeconds(.1f); Check(training.transform.position.x<3,"Out-of-bounds tool was not restored"); Pass(stage);
        stage="persistence validation";
        Check(HallRecords.SaveAll(),"Save failed: "+HallRecords.LastError); var progressFile=Path.Combine(HallRecords.SaveDirectory,"hall.json");
        Check(JsonUtility.FromJson<HallProgress>(File.ReadAllText(progressFile)).attempts==attempts+1,"Persisted count mismatch");
        int good=HallRecords.Progress.attempts; HallRecords.Progress.attempts=-1; Check(!HallRecords.SaveProgress(),"Invalid counts were saved"); HallRecords.Progress.attempts=good; Check(HallRecords.SaveProgress(),"Valid save retry failed"); Pass(stage);
        stage="direct surgery start and session transfer";
        hall.ShowHome(); hall.SelectMode("自主练习"); int records=HallRecords.Records.Count;
        hall.StartSurgery(); hall.StartSurgery(); Check(HallRecords.Records.Count==records+1,"Double start created duplicate sessions");
        while(SceneManager.GetActiveScene().name!="小手术操作室") yield return null;
        yield return FindRig(); Check(TrainingSession.Current && TrainingSession.Current.Record.mode=="自主练习" && TrainingSession.Current.Record.caseId=="CASE01","Mode/case did not transfer"); Pass(stage);
        stage="failed save retains active session";
        int savedAttempts=HallRecords.Progress.attempts; HallRecords.Progress.attempts=-1;
        rig.Pause(); TrainingSession.Current.ReturnToHall(); yield return new WaitForSeconds(.6f);
        Check(SceneManager.GetActiveScene().name=="小手术操作室" && !TrainingSession.Current.IsLoading && TrainingSession.Current.Record.status=="进行中","Failed save changed scene or ended session");
        Check(!string.IsNullOrEmpty(TrainingSession.Current.LoadingError) && rig.pauseStatus.text.Contains("保存失败"),"Failed save was not shown in pause UI");
        HallRecords.Progress.attempts=savedAttempts; Pass(stage);
        stage="return and truthful review";
        TrainingSession.Current.AddEvent("S05","自动验证：真实事件接口检查","验证"); TrainingSession.Current.ReturnToHall();
        while(SceneManager.GetActiveScene().name!="学习大厅") yield return null;
        yield return FindRig(); yield return new WaitForSeconds(.2f);
        Check(!TrainingSession.Current && hall.detailPage.activeSelf,"Return did not show review");
        Check(HallRecords.Records.Last().status=="未完成（返回大厅）","Incomplete run was reported as complete");
        Check(hall.detailText.text.Contains("真实事件接口检查"),"Review did not display actual event"); Pass(stage);
        Capture("review");
        stage="followup return and completion API";
        var complete=TrainingSession.Begin("引导学习"); complete.AddEvent("S59","自动验证：流程控制器提交完成","验证"); Check(complete.Complete(),"Completion API failed");
        yield return SceneManager.LoadSceneAsync("复诊室"); yield return FindRig(); complete.ReturnToHall();
        while(SceneManager.GetActiveScene().name!="学习大厅") yield return null;
        yield return FindRig(); yield return new WaitForSeconds(.2f); Check(HallRecords.Records.Last().status=="已完成","Completed status was lost"); Pass(stage);
        Restore(); hall.ResetTools(); hall.ShowHome();
        var errorsToKeep=errors.Where(e=>!e.Contains("XRManagerSettings.StopSubsystems called without an initialized manager")).ToArray();
        Check(errorsToKeep.Length==0,"Runtime logged errors: "+string.Join(" | ",errorsToKeep));
        finished=true;
        File.WriteAllText(Work+"/runtime-result.txt","PASS "+passed.Count+" checks\n"+string.Join("\n",passed));
        if(File.Exists(Marker)) File.Delete(Marker);
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    void Restore()
    {
        if(oldProgress==null) return;
        typeof(HallRecords).GetProperty("Progress").SetValue(null,oldProgress);
        HallRecords.Records.Clear(); HallRecords.Records.AddRange(oldRecords);
        HallRecords.SaveAll(); oldProgress=null;
    }
    void OnDestroy()
    {
        Application.logMessageReceived-=OnLog;
        Restore();
        foreach(var device in new InputDevice[]{leftDevice,rightDevice,headDevice}) if(device!=null && device.added) InputSystem.RemoveDevice(device);
        if(!finished && File.Exists(Marker)) File.Delete(Marker);
    }
}
#endif
