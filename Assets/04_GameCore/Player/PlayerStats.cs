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

        public float MaxHealth { get; private set; }

        public float MoveSpeed { get; private set; }

        public float DamageMultiplier { get; private set; }

        public float CritChance { get; private set; }

        public float PickupRange { get; private set; }

        public float CurrentHealth { get; private set; }

        public bool IsDead => CurrentHealth <= 0f;

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

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Broadcast();

            if (IsDead && GameManager.Instance != null)
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
        }

        private void Broadcast()
        {
            EventBus.Publish(new PlayerHealthChangedEvent(CurrentHealth, MaxHealth));
        }
    }
}
