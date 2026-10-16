using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 配置访问服务。运行时从 Resources 加载 ConfigDatabase 并建立只读索引。
    /// </summary>
    public class ConfigService : MonoSingleton<ConfigService>
    {
        [SerializeField] private string databaseResourcePath = "Config/ConfigDatabase";
        [SerializeField] private ConfigDatabase database;

        private readonly Dictionary<string, SkillConfig> _skills = new Dictionary<string, SkillConfig>();
        private readonly Dictionary<string, AffixConfig> _affixes = new Dictionary<string, AffixConfig>();
        private readonly Dictionary<string, ComboConfig> _combos = new Dictionary<string, ComboConfig>();
        private readonly Dictionary<string, ClassConfig> _classes = new Dictionary<string, ClassConfig>();
        private readonly Dictionary<int, WaveConfig> _waves = new Dictionary<int, WaveConfig>();
        private readonly Dictionary<string, MapThemeConfig> _maps = new Dictionary<string, MapThemeConfig>();

        public bool IsLoaded { get; private set; }

        public IReadOnlyDictionary<string, SkillConfig> Skills => _skills;

        public IReadOnlyDictionary<int, WaveConfig> Waves => _waves;

        protected override void OnSingletonAwake()
        {
            LoadAll();
        }

        public void LoadAll()
        {
            if (database == null)
            {
                database = Resources.Load<ConfigDatabase>(databaseResourcePath);
            }

            _skills.Clear();
            _affixes.Clear();
            _combos.Clear();
            _classes.Clear();
            _waves.Clear();
            _maps.Clear();

            if (database == null)
            {
                Debug.LogWarning($"[ConfigService] ConfigDatabase not found at Resources/{databaseResourcePath}. Using empty config.");
                IsLoaded = true;
                return;
            }

            Index(_skills, database.Skills, c => c.Id);
            Index(_affixes, database.Affixes, c => c.Id);
            Index(_combos, database.Combos, c => c.Id);
            Index(_classes, database.Classes, c => c.Id);
            Index(_maps, database.MapThemes, c => c.Id);

            foreach (var wave in database.Waves)
            {
                if (wave != null)
                {
                    _waves[wave.Layer] = wave;
                }
            }

            IsLoaded = true;
        }

        public SkillConfig GetSkill(string id) => Lookup(_skills, id);

        public AffixConfig GetAffix(string id) => Lookup(_affixes, id);

        public ComboConfig GetCombo(string id) => Lookup(_combos, id);

        public ClassConfig GetClass(string id) => Lookup(_classes, id);

        public MapThemeConfig GetMapTheme(string id) => Lookup(_maps, id);

        public WaveConfig GetWave(int layer) => _waves.TryGetValue(layer, out var wave) ? wave : null;

        public IEnumerable<SkillConfig> GetAllSkills() => _skills.Values;

        public IEnumerable<AffixConfig> GetAllAffixes() => _affixes.Values;

        public IEnumerable<ComboConfig> GetAllCombos() => _combos.Values;

        private static T Lookup<T>(Dictionary<string, T> map, string id) where T : Object
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return map.TryGetValue(id, out var value) ? value : null;
        }

        private static void Index<T>(Dictionary<string, T> map, T[] items, System.Func<T, string> keySelector)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var key = keySelector(item);
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning($"[ConfigService] Config with empty Id skipped: {item.name}");
                    continue;
                }

                map[key] = item;
            }
        }
    }
}
