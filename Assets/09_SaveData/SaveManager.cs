using System;
using System.IO;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.SaveData
{
    /// <summary>
    /// 存档管理器：本地加密读写、完整性校验、备份回退与云同步入口。
    ///
    /// 写入采用「临时文件 + 原子替换」策略，任意时刻中断都不会破坏上一份有效存档
    /// （设计正确性属性 8）。读取失败时回退到最近一份有效备份并提示玩家（需求 11.5）。
    /// </summary>
    public class SaveManager : MonoSingleton<SaveManager>
    {
        private const string FileName = "save.dat";
        private const string BackupSuffix = ".bak";
        private const string TempSuffix = ".tmp";
        private const string LegacyFileName = "save.json";

        [SerializeField] private bool enableCloudSync = false;

        private ICloudSaveService _cloudService = new LocalCloudSaveService();

        public SaveDataModel Data { get; private set; } = new SaveDataModel();

        /// <summary>最近一次加载是否回退了备份，供界面提示玩家。</summary>
        public bool LastLoadUsedBackup { get; private set; }

        private string SaveDirectory => Application.persistentDataPath;

        private string FilePath => Path.Combine(SaveDirectory, FileName);

        private string BackupPath => FilePath + BackupSuffix;

        private string TempPath => FilePath + TempSuffix;

        private string LegacyPath => Path.Combine(SaveDirectory, LegacyFileName);

        protected override void OnSingletonAwake()
        {
            Load();
        }

        public void SetCloudService(ICloudSaveService service)
        {
            _cloudService = service ?? new LocalCloudSaveService();
        }

        public void Load()
        {
            LastLoadUsedBackup = false;

            var primary = ReadAndParse(FilePath);
            if (primary != null)
            {
                Data = primary;
                return;
            }

            var backup = ReadAndParse(BackupPath);
            if (backup != null)
            {
                Data = backup;
                LastLoadUsedBackup = true;
                Debug.LogWarning("[SaveManager] Primary save invalid, restored from backup.");
                return;
            }

            var legacy = LoadLegacy();
            if (legacy != null)
            {
                Data = legacy;
                Debug.Log("[SaveManager] Migrated legacy plaintext save to encrypted format.");
                Save();
                return;
            }

            Data = new SaveDataModel();
        }

        public void Save()
        {
            try
            {
                var payload = SaveSerializer.Serialize(Data);
                File.WriteAllText(TempPath, payload);
                CommitTemp();

                if (enableCloudSync)
                {
                    SyncCloud();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        /// <summary>原子落盘：优先替换并留备份，平台不支持替换时回退为拷贝 + 移动。</summary>
        private void CommitTemp()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    File.Replace(TempPath, FilePath, BackupPath);
                    return;
                }
                catch (Exception)
                {
                    File.Copy(FilePath, BackupPath, true);
                }
            }

            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(TempPath, FilePath);
        }

        /// <summary>用备份覆盖当前存档并重新加载（需求 11.5 手动恢复入口）。</summary>
        public bool RestoreBackup()
        {
            var backup = ReadAndParse(BackupPath);
            if (backup == null)
            {
                return false;
            }

            Data = backup;
            Save();
            return true;
        }

        /// <summary>网络可用时把当前存档同步至云端（本期云服务为本地占位实现）。</summary>
        public void SyncCloud()
        {
            if (_cloudService == null || !_cloudService.IsAvailable)
            {
                return;
            }

            _cloudService.Upload(Data);
        }

        public void ResetToDefault()
        {
            Data = new SaveDataModel();
        }

        /// <summary>添加一条最高分记录，按分数降序排列，保留前 10 条。</summary>
        public void AddHighScore(long score, int wave, string classId)
        {
            Data.HighScores.Add(new HighScoreEntry
            {
                PlayerName = Data.PlayerName,
                Score = score,
                Wave = wave,
                ClassId = classId,
                PlayTime = DateTime.UtcNow
            });

            Data.HighScores.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (Data.HighScores.Count > 10)
            {
                Data.HighScores.RemoveRange(10, Data.HighScores.Count - 10);
            }

            Save();
        }

        /// <summary>更新玩家昵称。</summary>
        public void SetPlayerName(string name)
        {
            Data.PlayerName = string.IsNullOrEmpty(name) ? "Player" : name;
            Save();
        }

        private SaveDataModel ReadAndParse(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return SaveSerializer.Deserialize(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Read failed ({path}): {e.Message}");
                return null;
            }
        }

        private SaveDataModel LoadLegacy()
        {
            try
            {
                if (!File.Exists(LegacyPath))
                {
                    return null;
                }

                var text = File.ReadAllText(LegacyPath);
                return SaveSerializer.LooksLikePlaintext(text)
                    ? SaveSerializer.DeserializeLegacyPlaintext(text)
                    : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Legacy load failed: {e.Message}");
                return null;
            }
        }
    }
}
