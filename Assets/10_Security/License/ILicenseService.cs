namespace StarPixelSurge.Security
{
    public enum LicenseState
    {
        Unknown = 0,
        Valid = 1,
        Invalid = 2,
        Expired = 3
    }

    /// <summary>
    /// 授权校验契约。本期由本地实现占位，后续替换为联网验证实现即可接入远端授权服务。
    /// </summary>
    public interface ILicenseService
    {
        LicenseState Validate(string cachedKey);

        LicenseState Activate(string key);
    }

    /// <summary>
    /// 本地授权校验实现：基于本地缓存卡密做存在性与有效期判断。
    /// </summary>
    public class LocalLicenseService : ILicenseService
    {
        public LicenseState Validate(string cachedKey)
        {
            return string.IsNullOrEmpty(cachedKey) ? LicenseState.Unknown : LicenseState.Valid;
        }

        public LicenseState Activate(string key)
        {
            return string.IsNullOrEmpty(key) ? LicenseState.Invalid : LicenseState.Valid;
        }
    }
}
