using System;
using System.Security.Cryptography;
using System.Text;

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
    /// 卡密本地缓存记录。保存激活时间与到期时间，供离线过期判断使用。
    /// <see cref="ExpiresUtcTicks"/> 为 0 表示永久有效。
    /// </summary>
    [Serializable]
    public class LicenseRecord
    {
        public string Key;
        public long ActivatedUtcTicks;
        public long ExpiresUtcTicks;

        public bool IsPerpetual => ExpiresUtcTicks <= 0;
    }

    /// <summary>
    /// 授权校验契约。本期由本地实现占位，后续替换为联网验证实现即可接入远端授权服务。
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>校验本地缓存记录：无记录返回 Unknown，过期返回 Expired，格式非法返回 Invalid。</summary>
        LicenseState Validate(LicenseRecord record);

        /// <summary>激活卡密。格式非法返回 null，否则返回带激活与到期时间的记录。</summary>
        LicenseRecord Activate(string key);
    }

    /// <summary>
    /// 本地授权校验实现：卡密格式自校验（含校验位）并基于本地缓存做有效期判断（需求 12.5）。
    /// 卡密形如 <c>SPS-XXXX-XXXX-XXXX-YYYY</c>，其中后四位为载荷的 SHA256 校验前缀。
    /// </summary>
    public class LocalLicenseService : ILicenseService
    {
        private const string Prefix = "SPS";
        private const int GroupLength = 4;
        private const int PayloadGroups = 3;

        private readonly long _durationTicks;

        /// <summary>创建本地授权服务。<paramref name="duration"/> 为空或非正数表示永久授权。</summary>
        public LocalLicenseService(TimeSpan? duration = null)
        {
            _durationTicks = duration.HasValue && duration.Value.Ticks > 0 ? duration.Value.Ticks : 0L;
        }

        public LicenseState Validate(LicenseRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.Key))
            {
                return LicenseState.Unknown;
            }

            if (!IsWellFormed(record.Key))
            {
                return LicenseState.Invalid;
            }

            if (!record.IsPerpetual && DateTime.UtcNow.Ticks >= record.ExpiresUtcTicks)
            {
                return LicenseState.Expired;
            }

            return LicenseState.Valid;
        }

        public LicenseRecord Activate(string key)
        {
            if (!IsWellFormed(key))
            {
                return null;
            }

            var now = DateTime.UtcNow.Ticks;
            return new LicenseRecord
            {
                Key = Normalize(key),
                ActivatedUtcTicks = now,
                ExpiresUtcTicks = _durationTicks > 0 ? now + _durationTicks : 0L
            };
        }

        /// <summary>校验卡密格式与校验位，并生成一份可用于测试的合法卡密。</summary>
        public static string BuildKey(string payload)
        {
            var normalized = (payload ?? string.Empty).ToUpperInvariant();
            var groups = new StringBuilder();
            for (var i = 0; i < PayloadGroups; i++)
            {
                groups.Append('-');
                for (var j = 0; j < GroupLength; j++)
                {
                    var index = i * GroupLength + j;
                    groups.Append(index < normalized.Length ? normalized[index] : '0');
                }
            }

            var body = groups.ToString().TrimStart('-').Replace("-", string.Empty);
            return $"{Prefix}-{groups.ToString().TrimStart('-')}-{Checksum(body)}";
        }

        private static bool IsWellFormed(string key)
        {
            var normalized = Normalize(key);
            var parts = normalized.Split('-');
            if (parts.Length != 1 + PayloadGroups + 1)
            {
                return false;
            }

            if (!string.Equals(parts[0], Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var body = new StringBuilder();
            for (var i = 1; i <= PayloadGroups; i++)
            {
                if (parts[i].Length != GroupLength)
                {
                    return false;
                }

                body.Append(parts[i]);
            }

            if (parts[parts.Length - 1].Length != GroupLength)
            {
                return false;
            }

            return string.Equals(parts[parts.Length - 1], Checksum(body.ToString()), StringComparison.Ordinal);
        }

        private static string Normalize(string key)
        {
            return (key ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
        }

        /// <summary>取载荷 SHA256 的前四位十六进制作为校验位。</summary>
        private static string Checksum(string body)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(body));
                return (hash[0].ToString("X2") + hash[1].ToString("X2")).Substring(0, GroupLength);
            }
        }
    }
}
