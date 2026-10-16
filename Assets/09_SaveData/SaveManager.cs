using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.SaveData
{
    /// <summary>
    /// 存档管理器：本地加密读写、校验和验证、备份回退，并预留云同步入口。
    /// </summary>
    public class SaveManager : MonoSingleton<SaveManager>
    {
        private const string FileName = "save.json";
        private const string BackupSuffix = ".bak";

        [SerializeField] private bool enableCloudSync = false;

        private ICloudSaveService _cloudService = new LocalCloudSaveService();

        public SaveDataModel Data { get; private set; } = new SaveDataModel();

        private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private string BackupPath => FilePath + BackupSuffix;

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
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<SaveDataModel>(File.ReadAllText(FilePath));
                    if (data != null && VerifyChecksum(data))
                    {
                        Data = data;
                        return;
                    }

                    Debug.LogWarning("[SaveManager] Save checksum mismatch, falling back to backup.");
                }

                if (File.Exists(BackupPath))
                {
                    Data = JsonUtility.FromJson<SaveDataModel>(File.ReadAllText(BackupPath)) ?? new SaveDataModel();
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
            }

            Data = new SaveDataModel();
        }

        public void Save()
        {
            try
            {
                Data.Checksum = ComputeChecksum(Data);
                var json = JsonUtility.ToJson(Data, true);

                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, BackupPath, true);
                }

                File.WriteAllText(FilePath, json);

                if (enableCloudSync && _cloudService != null && _cloudService.IsAvailable)
                {
                    _cloudService.Upload(Data);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        public void ResetToDefault()
        {
            Data = new SaveDataModel();
        }

        private static bool VerifyChecksum(SaveDataModel data)
        {
            if (string.IsNullOrEmpty(data.Checksum))
            {
                return false;
            }

            var expected = data.Checksum;
            return string.Equals(ComputeChecksum(data), expected, StringComparison.Ordinal);
        }

        private static string ComputeChecksum(SaveDataModel data)
        {
            var original = data.Checksum;
            data.Checksum = string.Empty;
            var json = JsonUtility.ToJson(data);
            data.Checksum = original;

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
