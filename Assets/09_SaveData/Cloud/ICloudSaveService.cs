namespace StarPixelSurge.SaveData
{
    /// <summary>
    /// 云端存档同步契约。本期由本地实现占位，后续替换为远端实现即可接入联网同步。
    /// </summary>
    public interface ICloudSaveService
    {
        bool IsAvailable { get; }

        SaveDataModel Download();

        void Upload(SaveDataModel data);
    }

    /// <summary>
    /// 本地占位实现：云同步不可用，数据仅保存在本地。
    /// </summary>
    public class LocalCloudSaveService : ICloudSaveService
    {
        public bool IsAvailable => false;

        public SaveDataModel Download()
        {
            return null;
        }

        public void Upload(SaveDataModel data)
        {
        }
    }
}
