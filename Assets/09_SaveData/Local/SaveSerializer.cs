using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StarPixelSurge.SaveData
{
    /// <summary>
    /// 存档序列化器：负责加密序列化、完整性校验与版本迁移。
    ///
    /// 密文为 AES-256-CBC（随机 IV 前置），密钥由固定应用密钥经 PBKDF2 派生，
    /// 因此同一玩家的存档可在不同设备与重装后迁移（需求 11 用户故事）。
    /// 校验和覆盖明文 JSON，用于识别截断或篡改。
    /// </summary>
    public static class SaveSerializer
    {
        /// <summary>当前存档结构版本。低于此版本的存档经迁移后加载，高于此版本拒绝加载。</summary>
        public const int CurrentSchemaVersion = 2;

        private const string AppSecret = "StarPixelSurge.SaveData.v1";
        private const int KdfIterations = 20000;
        private const int KeyBytes = 32;

        private static readonly byte[] Salt =
        {
            0x53, 0x74, 0x61, 0x72, 0x50, 0x69, 0x78, 0x65,
            0x6c, 0x53, 0x75, 0x72, 0x67, 0x65, 0x2e, 0x31
        };

        /// <summary>序列化为加密密文（Base64）。</summary>
        public static string Serialize(SaveDataModel data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            data.SchemaVersion = CurrentSchemaVersion;
            data.LastSaveUtcTicks = DateTime.UtcNow.Ticks;
            data.Checksum = ComputeChecksum(data);

            var json = JsonUtility.ToJson(data, false);
            return Encrypt(json);
        }

        /// <summary>解密并校验密文。任一环节失败返回 null，由调用方回退备份或初始新档。</summary>
        public static SaveDataModel Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            string json;
            try
            {
                json = Decrypt(payload);
            }
            catch (Exception)
            {
                return null;
            }

            return ParseAndValidate(json);
        }

        /// <summary>解析旧版明文存档（迁移路径）。</summary>
        public static SaveDataModel DeserializeLegacyPlaintext(string json)
        {
            return ParseAndValidate(json);
        }

        /// <summary>判断一段文本是否像明文 JSON 存档，用于识别待迁移的旧文件。</summary>
        public static bool LooksLikePlaintext(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            var trimmed = payload.TrimStart();
            return trimmed.Length > 0 && trimmed[0] == '{';
        }

        private static SaveDataModel ParseAndValidate(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            SaveDataModel data;
            try
            {
                data = JsonUtility.FromJson<SaveDataModel>(json);
            }
            catch (Exception)
            {
                return null;
            }

            if (data == null)
            {
                return null;
            }

            // 先按持久化形态校验完整性，再做版本迁移；迁移会补齐字段从而改变序列化结果。
            if (!VerifyChecksum(data))
            {
                return null;
            }

            return Migrate(data);
        }

        /// <summary>
        /// 按版本迁移存档。低于当前版本逐级补齐字段；高于当前版本视为不可识别并拒绝加载。
        /// </summary>
        private static SaveDataModel Migrate(SaveDataModel data)
        {
            if (data.SchemaVersion > CurrentSchemaVersion)
            {
                return null;
            }

            if (data.SchemaVersion <= 0)
            {
                data.SchemaVersion = 1;
            }

            if (data.SchemaVersion == 1)
            {
                // v1 → v2：引入最后保存时间戳，旧档无该字段时置零。
                data.LastSaveUtcTicks = 0;
                data.SchemaVersion = 2;
            }

            if (data.UnlockedClasses == null)
            {
                data.UnlockedClasses = new System.Collections.Generic.List<string>();
            }

            if (data.Codex == null)
            {
                data.Codex = new System.Collections.Generic.List<CodexEntry>();
            }

            if (data.Settings == null)
            {
                data.Settings = new SettingsRecord();
            }

            return data;
        }

        private static bool VerifyChecksum(SaveDataModel data)
        {
            if (string.IsNullOrEmpty(data.Checksum))
            {
                return false;
            }

            return string.Equals(ComputeChecksum(data), data.Checksum, StringComparison.Ordinal);
        }

        private static string ComputeChecksum(SaveDataModel data)
        {
            var original = data.Checksum;
            data.Checksum = string.Empty;
            var json = JsonUtility.ToJson(data, false);
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

        private static byte[] DeriveKey()
        {
            using (var kdf = new Rfc2898DeriveBytes(AppSecret, Salt, KdfIterations))
            {
                return kdf.GetBytes(KeyBytes);
            }
        }

        private static string Encrypt(string plain)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = DeriveKey();
                aes.GenerateIV();

                var plainBytes = Encoding.UTF8.GetBytes(plain);
                byte[] cipher;
                using (var encryptor = aes.CreateEncryptor())
                {
                    cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }

                var combined = new byte[aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, combined, aes.IV.Length, cipher.Length);
                return Convert.ToBase64String(combined);
            }
        }

        private static string Decrypt(string payload)
        {
            var combined = Convert.FromBase64String(payload);
            if (combined.Length <= 16)
            {
                return null;
            }

            var iv = new byte[16];
            Buffer.BlockCopy(combined, 0, iv, 0, 16);
            var cipher = new byte[combined.Length - 16];
            Buffer.BlockCopy(combined, 16, cipher, 0, cipher.Length);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = DeriveKey();
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                    return Encoding.UTF8.GetString(plain);
                }
            }
        }
    }
}
