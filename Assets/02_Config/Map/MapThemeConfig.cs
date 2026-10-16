using System;
using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 地图主题配置：瓦片、天气、陷阱与天灾机制。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/MapTheme", fileName = "MapTheme_")]
    public class MapThemeConfig : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public string TilePalettePath;
        public string WeatherId;
        public string DisasterId;
        public string[] TrapIds = Array.Empty<string>();
        public string[] ObstacleIds = Array.Empty<string>();
    }
}
