using System;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 玩家数值容器：基础值 + 加法/乘法加成，统一重算并广播变化。
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base")]
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float baseDamageMultiplier = 1f;
        [SerializeField] private float baseCritChance = 0f;
        [SerializeField] private float basePickupRange = 1.5f;

        private float _bonusHealth;
        private float _bonusMoveSpeed;
        private float _bonusDamage;
        private float _bonusCrit;
        private float _bonusPickup;

        private float _multHealth = 1f;
        private float _multMoveSpeed = 1f;
        private float _multDamage = 1f;

        private float _multSkillRange = 1f;
        private float _multSkillCooldown = 1f;
        private int _bonusProjectiles;

        public float MaxHealth { get; private set; }

        public float MoveSpeed { get; private set; }

        public float DamageMultiplier { get; private set; }

        public float CritChance { get; private set; }

        public float PickupRange { get; private set; }

        /// <summary>技能范围倍率，作用于弹道射程。</summary>
        public float SkillRangeMultiplier { get; private set; } = 1f;

        /// <summary>技能冷却倍率，越小冷却越短。</summary>
        public float SkillCooldownMultiplier { get; private set; } = 1f;

        /// <summary>额外弹道数量。</summary>
        public int BonusProjectiles { get; private set; }

        public float CurrentHealth { get; private set; }

        public bool IsDead => CurrentHealth <= 0f;

        private bool _matchFatal = true;

        /// <summary>
        /// 该玩家的死亡是否结束整局。本地玩家为 true；
        /// 联机中其他玩家的分身设为 false，避免远端玩家死亡误判全队失败。
        /// </summary>
        public void SetMatchFatal(bool fatal)
        {
            _matchFatal = fatal;
        }

        private void Awake()
        {
            if (MaxHealth <= 0f)
            {
                Recalculate();
                CurrentHealth = MaxHealth;
            }
        }

        public void Initialize(ClassConfig classConfig)
        {
            if (classConfig != null)
            {
                baseMaxHealth = classConfig.BaseHealth;
                baseMoveSpeed = classConfig.BaseMoveSpeed;
                baseDamageMultiplier = classConfig.BaseDamageMultiplier;
            }

            _bonusHealth = 0f;
            _bonusMoveSpeed = 0f;
            _bonusDamage = 0f;
            _bonusCrit = 0f;
            _bonusPickup = 0f;
            _multHealth = 1f;
            _multMoveSpeed = 1f;
            _multDamage = 1f;
            _multSkillRange = 1f;
            _multSkillCooldown = 1f;
            _bonusProjectiles = 0;

            Recalculate();
            CurrentHealth = MaxHealth;
            Broadcast();
        }

        public void AddMaxHealth(float amount)
        {
            _bonusHealth += amount;
            Recalculate();
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            Broadcast();
        }

        public void AddMoveSpeed(float amount)
        {
            _bonusMoveSpeed += amount;
            Recalculate();
        }

        public void AddDamageMultiplier(float amount)
        {
            _bonusDamage += amount;
            Recalculate();
        }

        public void AddCritChance(float amount)
        {
            _bonusCrit += amount;
            Recalculate();
        }

        public void AddPickupRange(float amount)
        {
            _bonusPickup += amount;
            Recalculate();
        }

        public void MultiplyMaxHealth(float factor)
        {
            _multHealth *= factor;
            Recalculate();
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth);
            Broadcast();
        }

        public void MultiplyMoveSpeed(float factor)
        {
            _multMoveSpeed *= Mathf.Max(0.01f, factor);
            Recalculate();
        }

        public void MultiplyDamage(float factor)
        {
            _multDamage *= Mathf.Max(0.01f, factor);
            Recalculate();
        }

        public void MultiplySkillRange(float factor)
        {
            _multSkillRange *= Mathf.Max(0.01f, factor);
            Recalculate();
        }

        public void MultiplySkillCooldown(float factor)
        {
            _multSkillCooldown *= Mathf.Clamp(factor, 0.1f, 5f);
            Recalculate();
        }

        public void AddProjectiles(int amount)
        {
            _bonusProjectiles += amount;
            Recalculate();
        }

        /// <summary>按词条定义应用加成，供宝箱掉落物拾取时调用。</summary>
        public void ApplyAffix(AffixConfig affix)
        {
            if (affix == null)
            {
                return;
            }

            switch (affix.StatKey)
            {
                case "damage":
                    if (affix.IsMultiplicative)
                    {
                        MultiplyDamage(1f + affix.Value);
                    }
                    else
                    {
                        AddDamageMultiplier(affix.Value);
                    }

                    break;
                case "moveSpeed":
                    if (affix.IsMultiplicative)
                    {
                        MultiplyMoveSpeed(1f + affix.Value);
                    }
                    else
                    {
                        AddMoveSpeed(affix.Value);
                    }

                    break;
                case "critChance":
                    AddCritChance(affix.Value);
                    break;
                case "range":
                    MultiplySkillRange(1f + affix.Value);
                    break;
                case "cooldown":
                    MultiplySkillCooldown(1f + affix.Value);
                    break;
                case "projectileCount":
                    AddProjectiles(Mathf.RoundToInt(affix.Value));
                    break;
            }
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Broadcast();

            if (IsDead && _matchFatal && GameManager.Instance != null)
            {
                GameManager.Instance.CompleteMatch();
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            Broadcast();
        }

        public float RollDamage(float baseDamage)
        {
            var damage = baseDamage * DamageMultiplier;
            if (CritChance > 0f && UnityEngine.Random.value < CritChance)
            {
                damage *= 2f;
            }

            return damage;
        }

        private void Recalculate()
        {
            MaxHealth = (baseMaxHealth + _bonusHealth) * _multHealth;
            MoveSpeed = (baseMoveSpeed + _bonusMoveSpeed) * _multMoveSpeed;
            DamageMultiplier = (baseDamageMultiplier + _bonusDamage) * _multDamage;
            CritChance = Mathf.Clamp01(baseCritChance + _bonusCrit);
            PickupRange = basePickupRange + _bonusPickup;
            SkillRangeMultiplier = _multSkillRange;
            SkillCooldownMultiplier = _multSkillCooldown;
            BonusProjectiles = Mathf.Max(0, _bonusProjectiles);
        }

        private void Broadcast()
        {
            EventBus.Publish(new PlayerHealthChangedEvent(CurrentHealth, MaxHealth));
        }
    }
}
