using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShuQi.VR
{
    public sealed class TrainingSession : MonoBehaviour
    {
        public static TrainingSession Current { get; private set; }
        public TrainingRecord Record { get; private set; }
        public bool IsLoading { get; private set; }
        public string LoadingError { get; private set; }
        public static bool OpenReviewOnReturn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; OpenReviewOnReturn = false; }

        public static TrainingSession Begin(string mode)
        {
            if (mode != "引导学习" && mode != "自主练习") throw new ArgumentException("未知训练模式");
            if (Current) return Current;
            HallRecords.Load();
            var session = new GameObject("TrainingSession").AddComponent<TrainingSession>();
            session.Record = new TrainingRecord { id = Guid.NewGuid().ToString("N"), mode = mode, startedUtc = DateTime.UtcNow.ToString("o") };
            HallRecords.Records.Add(session.Record);
            session.AddEvent("S04", "选择模式并开始载入标准病例", "流程");
            return session;
        }

        void Awake()
        {
            if (Current && Current != this) { Destroy(gameObject); return; }
            Current = this; DontDestroyOnLoad(gameObject);
        }
        void OnDestroy() { if (Current == this) Current = null; }
        void Update()
        {
            if (Record != null && Record.status == "进行中" && !IsLoading && !HallRigUI.GameplayPaused && SceneManager.GetActiveScene().name != "学习大厅")
                Record.elapsedSeconds += Time.unscaledDeltaTime;
        }
        void OnApplicationPause(bool pause) { if (pause) HallRecords.SaveAll(); }
        void OnApplicationQuit() { HallRecords.SaveAll(); }

        public void AddEvent(string step, string message, string kind = "流程", string screenshot = null)
        {
            if (Record == null || string.IsNullOrWhiteSpace(step) || message == null) return;
            Record.events.Add(new TrainingEvent { step = step, message = message, kind = kind, seconds = Record.elapsedSeconds, screenshot = screenshot });
            HallRecords.SaveSessions();
        }

        public bool Complete()
        {
            if (Record == null || Record.status != "进行中") return false;
            Record.status = "已完成";
            Record.events.Add(new TrainingEvent { step = "S59", message = "手术流程控制器确认本次训练已完成", kind = "结束", seconds = Record.elapsedSeconds });
            if (HallRecords.SaveSessions()) return true;
            Record.status = "进行中"; Record.events.RemoveAt(Record.events.Count - 1);
            return false;
        }

        public void LoadProcedure() { if (!IsLoading) StartCoroutine(Load("小手术操作室", false)); }
        public void ReturnToHall() { if (!IsLoading) StartCoroutine(Load("学习大厅", true)); }

        IEnumerator Load(string scene, bool returning)
        {
            IsLoading = true; LoadingError = null;
            var rig = FindObjectOfType<HallRigUI>();
            if (rig) rig.BeginTransition();
            yield return new WaitForSecondsRealtime(.22f);
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                LoadingError = "无法载入“" + scene + "”，请检查 Build Settings。";
                IsLoading = false;
                if (rig) rig.EndTransition();
                yield break;
            }
            if (!HallRecords.SaveProgress())
            {
                LoadingError = HallRecords.LastError + "；请重试后再离开。";
                IsLoading = false; if (rig) rig.EndTransition(); yield break;
            }
            string previousStatus = Record.status;
            int previousEvents = Record.events.Count;
            if (returning && Record.status == "进行中")
            {
                Record.status = "未完成（返回大厅）";
                Record.events.Add(new TrainingEvent { step = "返回", message = "主动返回学习大厅；本次训练未被标记为完成", kind = "中断", seconds = Record.elapsedSeconds });
            }
            if (!HallRecords.SaveSessions())
            {
                LoadingError = HallRecords.LastError + "；请重试后再离开。";
                Record.status = previousStatus;
                Record.events.RemoveRange(previousEvents, Record.events.Count - previousEvents);
                IsLoading = false; if (rig) rig.EndTransition(); yield break;
            }
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single); }
            catch (Exception e) { LoadingError = "场景载入失败：" + e.Message; }
            if (operation == null)
            {
                Record.status = previousStatus;
                Record.events.RemoveRange(previousEvents, Record.events.Count - previousEvents);
                if (!HallRecords.SaveSessions()) LoadingError += "；" + HallRecords.LastError;
                IsLoading = false; if (rig) rig.EndTransition(); yield break;
            }
            while (!operation.isDone) yield return null;
            IsLoading = false;
            if (returning)
            {
                OpenReviewOnReturn = true;
                var hall = FindObjectOfType<LearningHallController>();
                if (hall) { hall.ShowReview(); OpenReviewOnReturn = false; }
                Current = null; Destroy(gameObject);
            }
            else AddEvent("S05", "已进入小手术操作室；等待正式手术步骤开始", "场景");
        }
    }
}
