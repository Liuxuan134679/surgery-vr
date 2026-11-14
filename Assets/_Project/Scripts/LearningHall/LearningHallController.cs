using System;
using System.Text;
using Autohand;
using UnityEngine;
using UnityEngine.UI;

namespace ShuQi.VR
{
    public sealed class LearningHallController : MonoBehaviour
    {
        public Transform uiRoot, bench, spawn, trainingStand, rotationTarget;
        public GameObject homePage, detailPage, calibrationPage, practicePage, reviewActions;
        public Text notice, detailTitle, detailText, practiceStatus, settingsStatus;
        public ScrollRect detailScroll;
        public HallTool[] tools;
        public HallTool PracticeTool;
        public bool PracticeActive { get; private set; }
        public string Mode { get; private set; } = "引导学习";
        public bool IsLoading => TrainingSession.Current && TrainingSession.Current.IsLoading;
        HallRigUI rig;
        Vector3 benchBase;
        int reportIndex;
        bool attemptCompleted;
        string statusMessage;
        Button guided, independent, startButton;

        Button Button(string name)
        {
            foreach(var b in uiRoot.GetComponentsInChildren<Button>(true)) if(b.name==name) return b;
            throw new InvalidOperationException("大厅按钮缺失："+name);
        }
        void Bind(string name, UnityEngine.Events.UnityAction action) { Button(name).onClick.AddListener(action); }
        void Awake()
        {
            HallRecords.Load(); rig=FindObjectOfType<HallRigUI>();
            benchBase=bench.position;
            ApplyBenchOffset();
            guided=Button("ModeGuided"); independent=Button("ModeIndependent"); startButton=Button("StartSurgery");
            Bind("ModeGuided",()=>SelectMode("引导学习")); Bind("ModeIndependent",()=>SelectMode("自主练习"));
            Bind("StartSurgery",StartSurgery); Bind("OpenCase",ShowCase); Bind("OpenCalibration",ShowCalibration); Bind("OpenPractice",ShowPractice); Bind("OpenReview",ShowReview);
            Bind("Save",Save); Bind("Quit",()=>{rig.Pause(); rig.SaveAndQuit();});
            Bind("DetailBack",ShowHome); Bind("CalibrationBack",ShowHome); Bind("PracticeBack",ShowHome);
            Bind("BenchUp",()=>AdjustBench(.025f)); Bind("BenchDown",()=>AdjustBench(-.025f));
            Bind("LeftHanded",()=>SetHandedness(true)); Bind("RightHanded",()=>SetHandedness(false));
            Bind("Recenter",()=>Reposition(spawn)); Bind("TrainingStand",()=>Reposition(trainingStand));
            Bind("StartPractice",StartPractice); Bind("ResetTools",ResetTools);
            Bind("PracticeToSurgery",StartSurgery);
            Bind("PreviousReport",()=>ChangeReport(-1)); Bind("NextReport",()=>ChangeReport(1));
            Bind("ReviewPractice",ShowPractice); Bind("RetryFullCase",StartSurgery);
            Bind("ScrollUp",()=>detailScroll.verticalNormalizedPosition=Mathf.Clamp01(detailScroll.verticalNormalizedPosition+.3f));
            Bind("ScrollDown",()=>detailScroll.verticalNormalizedPosition=Mathf.Clamp01(detailScroll.verticalNormalizedPosition-.3f));
            rotationTarget.gameObject.SetActive(false);
            SetNotice(string.IsNullOrEmpty(HallRecords.LastError)?"自由选择入口；无需先完成练习即可开始手术。":HallRecords.LastError);
            SelectMode(Mode); ShowHome();
        }
        void Start()
        {
            if(TrainingSession.OpenReviewOnReturn) { TrainingSession.OpenReviewOnReturn=false; ShowReview(); }
        }
        void Update()
        {
            startButton.interactable=!IsLoading;
            if(TrainingSession.Current && !string.IsNullOrEmpty(TrainingSession.Current.LoadingError)) SetNotice(TrainingSession.Current.LoadingError);
            if(PracticeActive) UpdatePracticeText();
        }
        public void SetNotice(string message) { statusMessage=message; if(notice) notice.text=message; }
        void Page(GameObject page)
        {
            homePage.SetActive(page==homePage); detailPage.SetActive(page==detailPage); calibrationPage.SetActive(page==calibrationPage); practicePage.SetActive(page==practicePage);
            if(page!=practicePage && PracticeActive) { PracticeActive=false; rotationTarget.gameObject.SetActive(false); HallRecords.SaveProgress(); }
        }
        public void ShowHome() { Page(homePage); }
        public void SelectMode(string mode)
        {
            if(TrainingSession.Current) { SetNotice("本次已锁定为"+TrainingSession.Current.Record.mode+"；点击开始可重试载入。"); return; }
            if(mode!="引导学习" && mode!="自主练习") return;
            Mode=mode;
            guided.image.color=mode=="引导学习"?new Color(.03f,.47f,.48f):new Color(.11f,.23f,.28f);
            independent.image.color=mode=="自主练习"?new Color(.03f,.47f,.48f):new Color(.11f,.23f,.28f);
        }
        public void StartSurgery()
        {
            if(IsLoading) return;
            if(!HallRecords.SaveProgress()) { SetNotice(HallRecords.LastError); return; }
            PracticeActive=false; rotationTarget.gameObject.SetActive(false);
            TrainingSession.Begin(Mode).LoadProcedure(); SetNotice("正在保存并载入小手术操作室…");
        }
        public void ShowCase()
        {
            Page(detailPage); reviewActions.SetActive(false); detailTitle.text="S01 认识病例与训练目标";
            SetDetail("标准病例 CASE01\n成人，上背部单发浅表脂肪瘤；已完成术前评估，适合局麻切除的教学病例。\n\n你将扮演学习中的术者，与虚拟护士配合，从术前准备一直体验到术后复诊。\n\n全过程概览\n01 准备与核查 → 02 切除与标本处理 → 03 关闭创口 → 04 术后照护与复诊\n\n引导学习：提供逐步提示。\n自主练习：保留求助和安全提示，减少主动引导。\n\n手柄操作\n握持键：近距抓取 / 松开释放；扳机：点击 UI。\n左摇杆移动；右摇杆左右转向；右摇杆按住瞄准瞬移、松开落地。\n右手 B 键：暂停 / 继续。\n\n本页为待医学审核的教学简介，不替代真实临床培训。\n查看介绍和完成大厅练习均不作为进入手术的条件。");
        }
        void SetDetail(string text)
        {
            detailText.text=text; Canvas.ForceUpdateCanvases();
            var rect=detailText.rectTransform;
            rect.sizeDelta=new Vector2(rect.sizeDelta.x,Mathf.Max(620,detailText.preferredHeight+30));
            detailScroll.verticalNormalizedPosition=1;
        }
        public void ShowCalibration() { Page(calibrationPage); RefreshSettings(); }
        void RefreshSettings()
        {
            settingsStatus.text="S02 站位与舒适度（可跳过）\n\n台高调整："+(HallRecords.Progress.benchOffset*100).ToString("+0.0;-0.0;0.0")+" cm\n惯用手 / UI 指针："+(HallRecords.Progress.leftHanded?"左手":"右手")+"\n\n调整台高前请放下全部器械。\n“到训练位”会把虚拟站位移到训练台前；\n“重定位”返回大厅入口并面向主面板。\n\n请在 Quest 系统内完成真实边界设置。\n台高和抓取姿态仍需佩戴 Quest 3 核对。";
        }
        bool AnyHeld()
        {
            if(rig && (rig.player.handLeft.holdingObj || rig.player.handRight.holdingObj || rig.player.handLeft.IsGrabbing() || rig.player.handRight.IsGrabbing())) return true;
            foreach(var tool in tools) if(tool.Grab && (tool.Grab.IsHeld() || tool.Grab.beingGrabbed)) return true;
            return false;
        }
        public void AdjustBench(float delta)
        {
            if(AnyHeld()) { SetNotice("请先放下所有器械，再调整台高。"); return; }
            HallRecords.Progress.benchOffset=Mathf.Clamp(HallRecords.Progress.benchOffset+delta,-.25f,.25f);
            ApplyBenchOffset(); foreach(var tool in tools) tool.ResetTool();
            RefreshSettings(); Save();
        }
        void ApplyBenchOffset() { bench.position=benchBase+Vector3.up*HallRecords.Progress.benchOffset; }
        public void SetHandedness(bool left) { HallRecords.Progress.leftHanded=left; RefreshSettings(); Save(); }
        public void Reposition(Transform marker)
        {
            if(AnyHeld()) { SetNotice("请先放下器械，再重定位。"); return; }
            rig.player.SetPosition(marker.position,marker.rotation); rig.teleporter.CancelTeleport(); SetNotice("已到达"+(marker==trainingStand?"训练站位":"大厅入口")+"。");
        }
        public void ShowPractice() { Page(practicePage); UpdatePracticeText(); }
        public void StartPractice()
        {
            if(PracticeActive && !attemptCompleted) { SetNotice("本轮正在进行；可复位器械继续练习。"); return; }
            if(AnyHeld()) { SetNotice("请先放下器械，再开始一轮练习。"); return; }
            foreach(var tool in tools) { tool.ResetTool(); tool.BeginAttempt(); }
            HallRecords.Progress.attempts++; attemptCompleted=false; PracticeActive=true;
            rotationTarget.rotation=Quaternion.Euler(0,0,90)*PracticeTool.home.rotation;
            rotationTarget.gameObject.SetActive(true);
            Save(); UpdatePracticeText();
        }
        public void ResetTools()
        {
            bool all=true; foreach(var tool in tools) if(!tool.ResetTool()) all=false;
            rig.player.handLeft.highlighter.ClearHighlights(); rig.player.handRight.highlighter.ClearHighlights();
            SetNotice(all?"未持有器械已归位；本轮进度和历史记录保留。":"已复位未持有器械；手中器械保持不动。");
        }
        public void NotifyTool(HallTool tool)
        {
            if(!PracticeActive || tool!=PracticeTool || HallRigUI.GameplayPaused) return;
            var p=HallRecords.Progress;
            p.pickedUp|=tool.TouchedThisAttempt; p.rotated|=tool.RotatedThisAttempt; p.handedOver|=tool.HandedThisAttempt;
            Save(); UpdatePracticeText();
        }
        public void NotifyPlaced(HallTool tool)
        {
            if(!PracticeActive || tool!=PracticeTool || HallRigUI.GameplayPaused) return;
            bool complete=tool.TouchedThisAttempt && tool.RotatedThisAttempt && tool.HandedThisAttempt;
            if(complete && !attemptCompleted)
            {
                attemptCompleted=true; HallRecords.Progress.placed=true; HallRecords.Progress.completions++;
                rotationTarget.gameObject.SetActive(false); Save(); SetNotice("本轮基础练习完成。可以重试，也可以直接开始手术。");
            }
            else if(!complete) SetNotice("已放回；本轮还有旋转或换手动作未完成，可继续拿起练习。");
            UpdatePracticeText();
        }
        void UpdatePracticeText()
        {
            var p=HallRecords.Progress;
            string Mark(bool ok)=>ok?"✓":"○";
            practiceStatus.text="S03 基础练习（不计入手术成绩）\n\n示教器械："+PracticeTool.toolName+"\n"+
                (PracticeActive?Mark(PracticeTool.TouchedThisAttempt)+" 近距拿起器械\n"+Mark(PracticeTool.RotatedThisAttempt)+" 转向悬浮示意轮廓，保持片刻\n"+Mark(PracticeTool.HandedThisAttempt)+" 从一只手换到另一只手\n"+Mark(attemptCompleted)+" 松手放回同名归位区\n":"点击“开始 / 重试”记录一轮基础练习。\n也可直接自由拿取台上器械。\n")+
                "\n历史尝试："+p.attempts+" 次　完成："+p.completions+" 次\n"+(attemptCompleted?"本轮已完成，可重试或进入手术。":"可随时回主页或开始手术，不设解锁门槛。");
        }
        public void ShowReview()
        {
            HallRecords.Load(); Page(detailPage); reviewActions.SetActive(true); reportIndex=Mathf.Max(0,HallRecords.Records.Count-1); RenderReview();
        }
        void ChangeReport(int delta) { reportIndex=Mathf.Clamp(reportIndex+delta,0,Mathf.Max(0,HallRecords.Records.Count-1)); RenderReview(); }
        void RenderReview()
        {
            detailTitle.text="S60 操作复盘 / 本地记录";
            if(HallRecords.Records.Count==0)
            {
                SetDetail("暂无正式训练记录。\n\n你可以返回主页开始手术，或到训练台自由练习。\n大厅基础练习只记录完成情况与尝试次数，不计正式手术成绩。\n\n正式操作端尚未提供步骤或错误数据时，本页不会生成固定成绩或虚构回放。");
                return;
            }
            var r=HallRecords.Records[reportIndex];
            var text=new StringBuilder();
            text.AppendLine("记录 "+(reportIndex+1)+" / "+HallRecords.Records.Count+"　"+r.id);
            text.AppendLine(r.caseId+"　"+r.mode+"　"+r.status);
            text.AppendLine("开始（UTC）："+r.startedUtc+"\n有效训练时间："+r.elapsedSeconds.ToString("F1")+" 秒\n");
            text.AppendLine("过程事件（来自实际流程接口）");
            foreach(var e in r.events) text.AppendLine(e.seconds.ToString("F1")+"s　"+e.step+" ["+e.kind+"] "+e.message);
            text.AppendLine("\n当前不生成临床能力分数。截图/动作轨迹回放与指定手术阶段重练，需对应手术阶段的数据与恢复逻辑就绪。\n\n可返回大厅练习，或按当前所选模式重开完整病例；原记录保留。");
            SetDetail(text.ToString());
        }
        public void Save() { SetNotice(HallRecords.SaveAll()?"已保存到本机；大厅练习与正式记录分别保存。":HallRecords.LastError); }
        void OnApplicationPause(bool pause) { if(pause) HallRecords.SaveAll(); }
        void OnApplicationQuit() { HallRecords.SaveAll(); }
    }
}
