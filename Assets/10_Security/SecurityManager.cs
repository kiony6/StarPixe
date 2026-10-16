using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Security
{
    /// <summary>
    /// 安全管理器：授权状态校验、卡密激活、程序完整性自检入口。
    /// 本地实现为主，联网验证与反调试加固在正式发布版本接入。
    /// </summary>
    public class SecurityManager : MonoSingleton<SecurityManager>
    {
        [SerializeField] private bool requireLicense;
        [SerializeField] private string cachedLicenseKey;
        [SerializeField] private string authorizationEndpoint;

        private ILicenseService _licenseService = new LocalLicenseService();

        public LicenseState LicenseState { get; private set; } = LicenseState.Unknown;

        public bool IsAuthorized => !requireLicense || LicenseState == LicenseState.Valid;

        protected override void OnSingletonAwake()
        {
            ValidateLicense();
        }

        public void SetLicenseService(ILicenseService service)
        {
            _licenseService = service ?? new LocalLicenseService();
        }

        public void ValidateLicense()
        {
            LicenseState = _licenseService.Validate(cachedLicenseKey);

            if (!IsAuthorized)
            {
                Debug.LogWarning("[SecurityManager] License not authorized. Premium features restricted.");
            }
        }

        public bool ActivateLicense(string key)
        {
            LicenseState = _licenseService.Activate(key);
            if (LicenseState == LicenseState.Valid)
            {
                cachedLicenseKey = key;
            }

            return LicenseState == LicenseState.Valid;
        }

        public bool VerifyIntegrity()
        {
            // 预留：比对程序集与资源哈希，正式版本接入发布快照校验。
            return true;
        }
    }
}
