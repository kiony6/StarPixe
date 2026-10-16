using System;
using System.Collections.Generic;

namespace StarPixelSurge.SaveData
{
    [Serializable]
    public class CodexEntry
    {
        public string Id;
        public int SeenCount;
    }

    [Serializable]
    public class SettingsRecord
    {
        public int GraphicsTier = 1;
        public float MasterVolume = 1f;
        public float BgmVolume = 1f;
        public float SfxVolume = 1f;
    }

    /// <summary>
    /// 持久化存档模型。包含解锁、图鉴、积分与设置，并携带完整性校验值。
    /// </summary>
    [Serializable]
    public class SaveDataModel
    {
        public int SchemaVersion = SaveSerializer.CurrentSchemaVersion;
        public List<string> UnlockedClasses = new List<string>();
        public List<CodexEntry> Codex = new List<CodexEntry>();
        public long Points;
        public SettingsRecord Settings = new SettingsRecord();

        /// <summary>最后保存的 UTC 时间戳（Ticks），供云同步冲突判定使用。</summary>
        public long LastSaveUtcTicks;

        public string Checksum;
    }
}
