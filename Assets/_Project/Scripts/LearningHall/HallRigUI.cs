using System;
using Autohand;
using Autohand.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ShuQi.VR
{
    [DefaultExecutionOrder(-50)]
    public sealed class HallRigUI : MonoBehaviour
    {
        public FingertipUIPointer pointer;
        public Transform leftController, rightController;
        public AutoHandPlayer player;
        public Teleporter teleporter;
        public Canvas pauseCanvas;
        public Text pauseStatus;
        public CanvasGroup fade;
        public Button resumeButton, returnButton, quitButton;
        public static bool GameplayPaused { get; private set; }
        public bool ManualPause { get; private set; }
        public bool Transitioning { get; private set; }
        public bool TrackingSuspended => unsafeTracking || waitingForRelease;
        InputAction leftTrigger, rightTrigger, menu, leftGrip, rightGrip, teleportClick, move, turn;
        InputAction headTracked, leftTracked, rightTracked;
        Behaviour[] gameplayInputs;
        bool[] inputEnabled;
        bool appPaused, focusLost, unsafeTracking, wasBlocked, wasTrigger, pressed, menuDown, waitingForRelease, seenTracking, movingEnabled;
        bool lastLeft;
        string saveMessage;
        Finger leftIndex, rightIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { GameplayPaused = false; }

        static InputAction Action(string name, string binding, InputActionType type = InputActionType.Button)
        { var action = new InputAction(name, type, binding); action.Enable(); return action; }

        void Awake()
        {
            HallRecords.Load();
            leftIndex=Array.Find(player.handLeft.fingers, f=>f.fingerType==FingerEnum.index);
            rightIndex=Array.Find(player.handRight.fingers, f=>f.fingerType==FingerEnum.index);
            pointer.finger=HallRecords.Progress.leftHanded?leftIndex:rightIndex;
            leftTrigger=Action("Hall UI Left", "<XRController>{LeftHand}/triggerPressed");
            rightTrigger=Action("Hall UI Right", "<XRController>{RightHand}/triggerPressed");
            menu=Action("Hall Pause", "<XRController>{RightHand}/secondaryButton");
            leftGrip=Action("Hall Left Grip", "<XRController>{LeftHand}/grip", InputActionType.Value);
            rightGrip=Action("Hall Right Grip", "<XRController>{RightHand}/grip", InputActionType.Value);
            teleportClick=Action("Hall Teleport", "<XRController>{RightHand}/primary2DAxisClick");
            move=Action("Hall Move Neutral", "<XRController>{LeftHand}/primary2DAxis", InputActionType.Value);
            turn=Action("Hall Turn Neutral", "<XRController>{RightHand}/primary2DAxis", InputActionType.Value);
            headTracked=Action("Hall Head Tracked", "<XRHMD>/isTracked");
            leftTracked=Action("Hall Left Tracked", "<XRController>{LeftHand}/isTracked");
            rightTracked=Action("Hall Right Tracked", "<XRController>{RightHand}/isTracked");
            gameplayInputs = new Behaviour[] { player.GetComponent<OpenXRHandPlayerControllerLink>(), player.handLeft.GetComponent<OpenXRHandControllerLink>(), player.handRight.GetComponent<OpenXRHandControllerLink>(), teleporter.GetComponent<OpenXRTeleporterLink>() };
            inputEnabled = new bool[gameplayInputs.Length];
            for(int i=0;i<gameplayInputs.Length;i++) inputEnabled[i] = gameplayInputs[i] && gameplayInputs[i].enabled;
            movingEnabled=player.useMovement;
            resumeButton.onClick.AddListener(Resume);
            returnButton.onClick.AddListener(ReturnToHall);
            quitButton.onClick.AddListener(SaveAndQuit);
            pauseCanvas.gameObject.SetActive(false); fade.alpha=0;
        }

        void Update()
        {
            bool tracked=headTracked.IsPressed() && leftTracked.IsPressed() && rightTracked.IsPressed();
            if(tracked) seenTracking=true;
            unsafeTracking=appPaused || focusLost || ((seenTracking || XRSettings.isDeviceActive) && !tracked);
            if(unsafeTracking) waitingForRelease=true;
            bool menuPressed=menu.IsPressed();
            if(menuPressed && !menuDown && !unsafeTracking && !Transitioning) { if(ManualPause) Resume(); else Pause(); }
            menuDown=menuPressed;
            bool neutral=leftGrip.ReadValue<float>()<.15f && rightGrip.ReadValue<float>()<.15f && !leftTrigger.IsPressed() && !rightTrigger.IsPressed() && !teleportClick.IsPressed() && move.ReadValue<Vector2>().magnitude<.15f && turn.ReadValue<Vector2>().magnitude<.15f;
            if(waitingForRelease && neutral && !unsafeTracking) waitingForRelease=false;
            bool blocked=unsafeTracking || waitingForRelease || ManualPause || Transitioning;
            if(blocked && !wasBlocked) SetGameplayInput(false);
            if(!blocked && wasBlocked) SetGameplayInput(true);
            wasBlocked=blocked; GameplayPaused=blocked;
            bool showPause=unsafeTracking || waitingForRelease || ManualPause;
            if(showPause && !pauseCanvas.gameObject.activeSelf) PositionPauseCanvas();
            pauseCanvas.gameObject.SetActive(showPause);
            resumeButton.interactable=!unsafeTracking;
            pauseStatus.text=unsafeTracking ? "追踪或应用焦点已中断\n恢复佩戴后，松开握持键、扳机和摇杆继续。" : waitingForRelease ? "请松开握持键、扳机与摇杆，再继续操作。" : "操作已暂停\n右手 B 键或点击继续。\n" + saveMessage;
            if(ManualPause && !unsafeTracking && TrainingSession.Current) pauseStatus.text=TrainingSession.Current.Record.caseId+" · "+TrainingSession.Current.Record.mode+"\n"+pauseStatus.text;
            if(TrainingSession.Current && !string.IsNullOrEmpty(TrainingSession.Current.LoadingError)) pauseStatus.text+="\n"+TrainingSession.Current.LoadingError;

            bool left=HallRecords.Progress.leftHanded;
            pointer.finger=left?leftIndex:rightIndex;
            var activeHand=left ? player.handLeft : player.handRight;
            bool uiAllowed=!unsafeTracking && !waitingForRelease && !Transitioning && !activeHand.holdingObj && !activeHand.IsGrabbing() && !teleportClick.IsPressed();
            bool trigger=(left?leftTrigger:rightTrigger).IsPressed();
            if(left!=lastLeft || !uiAllowed)
            {
                CancelPointer();
                if(pointer.enabled) pointer.enabled=false;
                wasTrigger=trigger;
            }
            else
            {
                if(!pointer.enabled) { pointer.enabled=true; wasTrigger=trigger; }
                if(trigger && !wasTrigger) { pointer.Press(); pressed=true; }
                if(!trigger && wasTrigger && pressed) { pointer.Release(); pressed=false; }
                wasTrigger=trigger;
            }
            lastLeft=left;
            fade.alpha=Mathf.MoveTowards(fade.alpha,Transitioning?1:0,Time.unscaledDeltaTime*5);
        }

        void SetGameplayInput(bool enabled)
        {
            player.Move(Vector2.zero); player.Turn(0); player.body.velocity=Vector3.zero;
            teleporter.CancelTeleport();
            for(int i=0;i<gameplayInputs.Length;i++) if(gameplayInputs[i]) gameplayInputs[i].enabled=enabled && inputEnabled[i];
            player.useMovement=enabled && movingEnabled;
            CancelPointer();
        }
        void CancelPointer() { if(pointer) pointer.Cancel(); pressed=false; }
        void PositionPauseCanvas()
        {
            Vector3 forward=Vector3.ProjectOnPlane(player.headCamera.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            pauseCanvas.transform.position=player.headCamera.transform.position+forward*1.15f-Vector3.up*.08f;
            pauseCanvas.transform.rotation=Quaternion.LookRotation(forward,Vector3.up);
        }
        public void Pause() { ManualPause=true; saveMessage=null; CancelPointer(); PositionPauseCanvas(); }
        public void Resume() { ManualPause=false; waitingForRelease=true; CancelPointer(); }
        public void BeginTransition() { Transitioning=true; CancelPointer(); }
        public void EndTransition() { Transitioning=false; waitingForRelease=true; }
        public void ReturnToHall()
        {
            CancelPointer();
            if(TrainingSession.Current) TrainingSession.Current.ReturnToHall();
            else
            {
                var hall=FindObjectOfType<LearningHallController>();
                if(hall) { hall.ShowHome(); Resume(); }
                else if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="学习大厅") StartCoroutine(LoadHallWithoutSession());
            }
        }
        System.Collections.IEnumerator LoadHallWithoutSession()
        {
            if(!HallRecords.SaveAll()) { saveMessage=HallRecords.LastError; yield break; }
            BeginTransition(); yield return new WaitForSecondsRealtime(.22f);
            if(Application.CanStreamedLevelBeLoaded("学习大厅")) UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("学习大厅");
            else { EndTransition(); saveMessage="Build Settings 中缺少学习大厅。"; }
        }
        public void SaveAndQuit()
        {
            if(!HallRecords.SaveAll()) { saveMessage=HallRecords.LastError; return; }
            saveMessage="本地保存成功。";
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void OnApplicationFocus(bool focus) { if(!Application.isEditor) { focusLost=!focus; if(!focus) { CancelPointer(); waitingForRelease=true; } } }
        void OnApplicationPause(bool pause) { appPaused=pause; if(pause) { CancelPointer(); waitingForRelease=true; HallRecords.SaveAll(); } }
        void OnDisable() { CancelPointer(); GameplayPaused=false; }
        void OnDestroy()
        {
            foreach(var action in new[]{leftTrigger,rightTrigger,menu,leftGrip,rightGrip,teleportClick,move,turn,headTracked,leftTracked,rightTracked}) action?.Dispose();
        }
    }
}
