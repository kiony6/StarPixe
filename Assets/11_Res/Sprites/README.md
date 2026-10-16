# 美术资产管线（Sprites）

本目录说明像素霓虹风格游戏《星界像素狂潮》的贴图资产约定与导入流程。
代码侧由 `StarPixelSurge.Framework.SpriteCatalog` 负责加载，缺失时回退到
`RuntimeSprite` 程序化占位，因此接入真实美术不改变任何游戏逻辑。

## 目录约定

贴图必须放在 Unity 的 `Resources` 目录下，路径规则为：

```
Assets/11_Res/Resources/Sprites/<category>/<id>.png
```

`<category>` 与 `<id>` 对应代码中的加载调用：

| category      | 用途         | id 示例                              |
| ------------- | ------------ | ------------------------------------ |
| `monsters`    | 怪物与首领   | `grunt` `runner` `swarmer` `boss`    |
| `projectiles` | 弹道         | 技能 `id`，如 `lightning_orb`        |
| `loot`        | 掉落物       | 词缀 `id`，如 `affix_vitality`       |
| `pickups`     | 拾取物       | 预留                                 |
| `ui`          | 界面图标     | 预留                                 |

- `id` 必须与配置中的标识完全一致（大小写敏感）。
- 加载后渲染器颜色统一置为白色，贴图请按**完整彩色美术**绘制，不要只画白色蒙版。
- 分辨率建议为 16 / 32 / 64 的整数倍，渲染时按 `pixelsPerUnit` 换算缩放。

## 导入设置

像素美术需要关闭压缩与过滤，统一使用点采样。两种方式任选其一：

1. 选中贴图后在 Inspector 手动设置；
2. 运行菜单 `StarPixelSurge/Assets/Apply Sprite Import Settings`，批量应用；
   或将新贴图放入 `Resources/Sprites` 后由 `SpriteImportPostprocessor` 自动应用。

统一参数：

| 设置项             | 值                        |
| ------------------ | ------------------------- |
| Texture Type       | Sprite (2D and UI)        |
| Sprite Mode        | Single                    |
| Pixels Per Unit    | 32                        |
| Filter Mode        | Point (no filter)         |
| Compression        | None                      |
| Wrap Mode          | Clamp                     |
| Alpha Is Transparency | true                   |

## 新增一张贴图

1. 导出 PNG，命名为目标 `id`（例如 `monsters/boss.png`）。
2. 放入对应 `Assets/11_Res/Resources/Sprites/<category>/` 目录。
3. 运行 `StarPixelSurge/Assets/Apply Sprite Import Settings`（或依赖自动处理器）。
4. 进入游戏确认外观；若贴图未生效，用 `SpriteCatalog.Clear()` 清缓存后重载。
