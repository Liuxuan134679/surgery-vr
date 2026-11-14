using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShuQi.VR
{
    [Serializable] public sealed class HallProgress
    {
        public int version = 1;
        public bool leftHanded;
        public float benchOffset;
        public int attempts, completions;
        public bool pickedUp, rotated, handedOver, placed;
    }

    [Serializable] public sealed class TrainingEvent
    {
        public string step, message, kind, screenshot;
        public float seconds;
    }

    [Serializable] public sealed class TrainingRecord
    {
        public string id, caseId = "CASE01", mode, startedUtc, status = "进行中";
        public float elapsedSeconds;
        public List<TrainingEvent> events = new List<TrainingEvent>();
    }

    [Serializable] sealed class TrainingArchive
    {
        public int version = 1;
        public List<TrainingRecord> records = new List<TrainingRecord>();
    }

    public static class HallRecords
    {
        public static HallProgress Progress { get; private set; } = new HallProgress();
        public static List<TrainingRecord> Records { get; private set; } = new List<TrainingRecord>();
        public static string LastError { get; private set; }
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "ShuQi");
        static bool loaded, damagedProgress, damagedArchive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = damagedProgress = damagedArchive = false;
            Progress = new HallProgress(); Records = new List<TrainingRecord>(); LastError = null;
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Progress = Read<HallProgress>("hall.json", ValidateProgress, out damagedProgress) ?? new HallProgress();
            var archive = Read<TrainingArchive>("sessions.json", ValidateArchive, out damagedArchive);
            Records = archive != null ? archive.records : new List<TrainingRecord>();
            foreach (var record in Records)
                if (record.status == "进行中") record.status = "中断（尚未完成）";
        }

        static T Read<T>(string name, Func<T, bool> valid, out bool damaged) where T : class
        {
            damaged = false;
            string file = Path.Combine(SaveDirectory, name);
            foreach (string candidate in new[] { file, file + ".tmp", file + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var value = JsonUtility.FromJson<T>(File.ReadAllText(candidate));
                    if (value == null || !valid(value)) throw new InvalidDataException("记录格式或字段无效");
                    return value;
                }
                catch (Exception e) { damaged = true; LastError = name + " 读取失败：" + e.Message; }
            }
            return null;
        }

        static bool ValidateProgress(HallProgress p) => p.version == 1 && p.attempts >= 0 && p.completions >= 0 && p.completions <= p.attempts && !float.IsNaN(p.benchOffset) && !float.IsInfinity(p.benchOffset) && Mathf.Abs(p.benchOffset) <= .251f;
        static bool ValidateArchive(TrainingArchive a)
        {
            if (a.version != 1 || a.records == null) return false;
            var ids = new HashSet<string>();
            foreach (var r in a.records)
            {
                if (r == null || string.IsNullOrEmpty(r.id) || !ids.Add(r.id) || r.caseId != "CASE01" || (r.mode != "引导学习" && r.mode != "自主练习") || r.events == null || r.elapsedSeconds < 0 || float.IsNaN(r.elapsedSeconds) || float.IsInfinity(r.elapsedSeconds)) return false;
                foreach (var e in r.events)
                    if (e == null || string.IsNullOrEmpty(e.step) || e.message == null || e.seconds < 0 || float.IsNaN(e.seconds) || float.IsInfinity(e.seconds)) return false;
            }
            return true;
        }

        // Keep the last valid file until the replacement has been written and read back successfully.
        static bool Write<T>(string name, T value, Func<T, bool> valid, bool damaged) where T : class
        {
            string file = Path.Combine(SaveDirectory, name), temp = file + ".tmp";
            try
            {
                if (!valid(value)) throw new InvalidDataException("拒绝保存无效记录");
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(temp, JsonUtility.ToJson(value, true));
                if (!valid(JsonUtility.FromJson<T>(File.ReadAllText(temp)))) throw new IOException("保存后校验失败");
                if (damaged && File.Exists(file)) File.Copy(file, file + ".corrupt-" + DateTime.UtcNow.Ticks, false);
                if (File.Exists(file))
                {
                    if (!damaged) File.Copy(file, file + ".bak", true);
                    File.Delete(file);
                }
                File.Move(temp, file);
                LastError = null;
                return true;
            }
            catch (Exception e) { LastError = name + " 保存失败：" + e.Message; return false; }
        }

        public static bool SaveProgress()
        {
            Load();
            bool ok = Write("hall.json", Progress, ValidateProgress, damagedProgress);
            if (ok) damagedProgress = false;
            return ok;
        }

        public static bool SaveSessions()
        {
            Load();
            bool ok = Write("sessions.json", new TrainingArchive { records = Records }, ValidateArchive, damagedArchive);
            if (ok) damagedArchive = false;
            return ok;
        }

        public static bool SaveAll()
        {
            bool hall = SaveProgress(); string hallError = LastError;
            bool sessions = SaveSessions();
            if (!hall) LastError = hallError;
            return hall && sessions;
        }
    }
}
