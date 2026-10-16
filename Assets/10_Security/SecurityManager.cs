using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Security
{
    /// <summary>
    /// 安全管理器：授权状态校验、卡密激活本地缓存、反调试检测与程序完整性自检入口。
    /// 本地实现为主，联网验证与远端加固在正式发布版本接入。
    /// </summary>
    public class SecurityManager : MonoSingleton<SecurityManager>
    {
        private const string RecordPrefsKey = "sps.license.record";

        [SerializeField] private bool requireLicense;
        [SerializeField] private AntiDebugResponse antiDebugResponse = AntiDebugResponse.Log;
        [SerializeField] private SecurityBaseline integrityBaseline;

        private ILicenseService _licenseService;

        public LicenseState LicenseState { get; private set; } = LicenseState.Unknown;

        public bool IsAuthorized => !requireLicense || LicenseState == LicenseState.Valid;

        /// <summary>最近一次完整性校验结果。</summary>
        public IntegrityReport LastIntegrityReport { get; private set; }

        protected override void OnSingletonAwake()
        {
            _licenseService = new LocalLicenseService();
            LoadRecord();
            ValidateLicense();
            RunStartupChecks();
        }

        public void SetLicenseService(ILicenseService service)
        {
            _licenseService = service ?? new LocalLicenseService();
        }

        public void ValidateLicense()
        {
            LicenseState = _licenseService.Validate(CachedRecord);

            if (!IsAuthorized)
            {
                Debug.LogWarning("[SecurityManager] License not authorized. Premium features restricted.");
            }
        }

        public bool ActivateLicense(string key)
        {
            var record = _licenseService.Activate(key);
            if (record == null)
            {
                LicenseState = LicenseState.Invalid;
                return false;
            }

            CachedRecord = record;
            SaveRecord();
            LicenseState = _licenseService.Validate(record);
            return LicenseState == LicenseState.Valid;
        }

        /// <summary>校验程序集与资源完整性（需求 13.5）。</summary>
        public bool VerifyIntegrity()
        {
            LastIntegrityReport = IntegrityChecker.VerifyAll(integrityBaseline);
            if (!LastIntegrityReport.Passed)
            {
                Debug.LogError($"[SecurityManager] Integrity check failed: {LastIntegrityReport.Detail}");
            }

            return LastIntegrityReport.Passed;
        }

        public void SetIntegrityBaseline(SecurityBaseline baseline)
        {
            integrityBaseline = baseline;
        }

        private LicenseRecord CachedRecord { get; set; }

        private void RunStartupChecks()
        {
            if (AntiDebug.DetectDebugger())
            {
                AntiDebug.ApplyResponse(antiDebugResponse);
            }

            if (!VerifyIntegrity())
            {
                // 需求 13.5：完整性不匹配时记录事件并终止会话。
                Debug.LogError("[SecurityManager] Session terminated due to integrity mismatch.");
#if !UNITY_EDITOR
                Application.Quit(0);
#endif
            }
        }

        private void LoadRecord()
        {
            if (!PlayerPrefs.HasKey(RecordPrefsKey))
            {
                return;
            }

            try
            {
                CachedRecord = JsonUtility.FromJson<LicenseRecord>(PlayerPrefs.GetString(RecordPrefsKey));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SecurityManager] License record load failed: {e.Message}");
                CachedRecord = null;
            }
        }

        private void SaveRecord()
        {
            if (CachedRecord == null)
            {
                return;
            }

            PlayerPrefs.SetString(RecordPrefsKey, JsonUtility.ToJson(CachedRecord));
            PlayerPrefs.Save();
        }
    }
}
