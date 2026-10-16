# Requirements Document

Feature Name: star-pixel-surge
Updated: 2026-10-16

## Introduction

星界像素狂潮（Star Pixel Surge）是一款面向 Windows PC 的霓虹像素风肉鸽割草游戏。产品支持单机无尽模式与 2-4 人联机合作模式，核心诉求为高随机 Build 深度、全屏爆炸级视觉表现、低延迟可房主迁移的联机稳定性，以及可商业上线所需的性能优化、存档、卡密授权与防破解能力。

本文档基于项目立项与开发文档，将整体需求拆解为可验证的 EARS 需求条目，作为技术设计与实施验收依据。需求覆盖范围对应 V2.0 正式上线版本。

## Glossary

- **Game_Client**: 运行于玩家 Windows PC 的 Unity 游戏客户端进程
- **Host**: 联机对局中承担权威模拟与帧广播职责的玩家客户端
- **Frame_Sync_Service**: 联机帧同步网络模块，负责帧时间轴、输入采集与状态广播
- **Host_Migration_Service**: 房主迁移模块，负责房主失效检测与新房主选举
- **Skill_System**: 技能系统，负责技能生成、升级、词条赋值与组合技判定
- **Wave_System**: 波次系统，负责怪物生成、难度递增与 BOSS 刷新
- **Effect_System**: 特效系统，负责粒子、光影、震动、拖尾与屏幕后处理生成与回收
- **Object_Pool**: 对象池，负责可复用对象的申请、复用与归还
- **Build_Profile**: 单局内玩家的技能、词条、道具与增益集合
- **Affix**: 词条，附加于技能并改变技能数值或机制的随机属性
- **Combo_Skill**: 组合技，由两个指定技能同时满级触发的高表现技能
- **Save_Data**: 存档数据，包含解锁、图鉴、积分与设置
- **Security_Module**: 安全模块，负责完整性校验、反调试、反篡改与授权校验
- **License_Key**: 卡密，用于激活客户端授权状态的凭证
- **Graphics_Tier**: 特效分级配置，取值为低配、标准、极致三档
- **Player_Input**: 玩家操作输入，包含移动方向与技能触发指令
- **Frame_Index**: 帧序号，联机对局内单调递增的逻辑帧编号
- **Entity**: 对局内可被模拟的角色、怪物、召唤物或弹道对象
- **Neon_Pixel_Style**: 像素霓虹美术风格，32px 角色精度与 64px 特效精度规范

## Requirements

### Requirement 1: 工程与模块化架构

**User Story:** AS 开发与运维团队, I want 一套模块化、低耦合的 Unity 工程结构, so that 后续新增职业、地图、技能与联机玩法时改动范围可控。

#### Acceptance Criteria

1. The Game_Client SHALL organize source assets under the 13 top-level directories 01_Framework through 13_Editor.
2. The Game_Client SHALL expose global services GameManager, UIManager, SkillManager, WaveManager, NetManager, EffectManager, SaveManager, and SecurityManager as unique singletons.
3. WHEN a singleton is requested before initialization, the Game_Client SHALL create exactly one instance and return that instance for subsequent requests.
4. The Game_Client SHALL load tunable gameplay values for skills, classes, and waves from configuration data files.
5. WHEN a configuration value changes in the configuration data file, the Game_Client SHALL apply the changed value without requiring source code recompilation.

### Requirement 2: 像素渲染与分辨率适配

**User Story:** AS 玩家, I want 纯正且清晰的像素画面, so that 游戏视觉风格完整且在高分屏上无模糊与形变。

#### Acceptance Criteria

1. The Game_Client SHALL render sprites with texture filtering set to Point.
2. The Game_Client SHALL maintain the configured pixel-per-unit scale so that 32px 角色资源在游戏世界内按像素整数倍显示。
3. WHEN the display resolution equals 1080P, 2K, or 4K, the Game_Client SHALL scale the UI canvas to fill the screen without stretch distortion.
4. WHILE the display reports a high-DPI setting, the Game_Client SHALL apply the corresponding canvas scaler reference resolution.
5. The Game_Client SHALL apply a 2D emission shader and layered particle overlays to produce neon glow on dark backgrounds.

### Requirement 3: 镜头与打击反馈

**User Story:** AS 玩家, I want 强烈的打击反馈, so that 技能命中与大招释放具备爽快感。

#### Acceptance Criteria

1. WHEN a player-triggered ultimate skill activates, the Game_Client SHALL apply camera shake, color grading, exposure boost, and frame freeze within the same display frame.
2. WHEN a high-damage hit resolves, the Game_Client SHALL emit hit flash, trail, and screen shake proportional to the damage tier.
3. WHILE the active Graphics_Tier is 低配, the Game_Client SHALL disable screen shake and glow overlay for hit feedback.

### Requirement 4: 核心对局循环

**User Story:** AS 玩家, I want 一局完整且节奏递进的无尽生存体验, so that 每局游戏具备明确进度与持续挑战。

#### Acceptance Criteria

1. WHEN a match starts, the Game_Client SHALL initialize a single-life survival round with a duration target between 10 and 20 minutes.
2. WHILE a round is active, the Game_Client SHALL advance the round layer value as elapsed time increases.
3. WHEN a player accumulates the configured experience threshold, the Game_Client SHALL pause the round and present three random upgrade options.
4. WHEN a player selects one upgrade option, the Game_Client SHALL apply that upgrade to the Build_Profile and resume the round.
5. IF the player character health reaches zero, the Game_Client SHALL end the round and present the结算 screen with accumulated statistics.

### Requirement 5: 随机肉鸽机制

**User Story:** AS 玩家, I want 每局差异化的随机成长, so that 长期游玩保持新鲜度与 Build 多样性。

#### Acceptance Criteria

1. The Game_Client SHALL randomize the offered skill set, the Affix assignment, the drop items, the monster waves, and the round buffs per round.
2. WHEN an upgrade is offered, the Game_Client SHALL select options from the eligible pool using a weighted random distribution.
3. WHEN the same round is replayed by a player, the Game_Client SHALL produce a round seed independently of the previous round seed.

### Requirement 6: 技能系统

**User Story:** AS 玩家, I want 丰富且可成长的技能体系, so that 战斗表现与成长路径具备深度。

#### Acceptance Criteria

1. The Game_Client SHALL provide 25 distinct skills at V2.0.
2. WHEN a skill level increases, the Game_Client SHALL apply the configured per-level damage, range, and cooldown growth to that skill.
3. WHEN a skill is activated, the Game_Client SHALL obtain the projectile, summon, or area instance from the Object_Pool.
4. WHEN a pooled skill instance completes its lifetime, the Game_Client SHALL return that instance to the Object_Pool.
5. The Game_Client SHALL support at least 1000 concurrently active skill instances per match.
6. WHEN a skill is granted, the Skill_System SHALL assign Affixes from a pool of at least 20 Affix definitions.
7. WHEN two designated skills each reach maximum level, the Skill_System SHALL unlock the corresponding Combo_Skill.
8. WHEN a Combo_Skill unlocks, the Game_Client SHALL instantiate the dedicated full-screen effect and apply the linked mechanic.
9. WHILE a match runs in single-player mode, the Skill_System SHALL restrict the Combo_Skill set to the single-player eligible subset.

### Requirement 7: 怪物波次与 AI

**User Story:** AS 玩家, I want 高强度且层次清晰的怪物压力, so that 割草过程具备挑战与节奏变化。

#### Acceptance Criteria

1. WHEN a round begins, the Wave_System SHALL spawn monster groups according to the configured wave table.
2. The Wave_System SHALL classify monsters into 小怪, 精英, BOSS, and 天灾 tiers.
3. WHILE a round is active, the Wave_System SHALL increase monster health, movement speed, and spawn count as the layer value increases.
4. WHEN a monster is within aggro range of a player character, the monster SHALL path toward and engage the nearest player character.
5. WHEN a monster dies, the Game_Client SHALL release the monster instance and its associated effects to the Object_Pool.
6. The Game_Client SHALL support at least 1000 concurrently active monster instances per match.

### Requirement 8: 多人联机与帧同步

**User Story:** AS 玩家, I want 低延迟且稳定的多人合作, so that 组队割草过程保持画面一致且不中断。

#### Acceptance Criteria

1. The Frame_Sync_Service SHALL synchronize player positions, skill casts, monster spawns, and damage results across all clients by Frame_Index.
2. WHEN a player joins a room, the Room service SHALL assign that player to the room roster and mark the player as not ready.
3. WHEN all players in a room mark ready, the Room service SHALL lock the roster and start the match.
4. WHILE a match is active, the Frame_Sync_Service SHALL collect Player_Input from each client and broadcast the authoritative frame state from the Host.
5. IF packet loss occurs, the Frame_Sync_Service SHALL apply frame interpolation and latency smoothing to maintain continuous playback.
6. WHEN the Host becomes unreachable, the Host_Migration_Service SHALL elect a new Host within the configured election window.
7. WHEN a new Host is elected, the match SHALL continue from the last confirmed Frame_Index without restarting the round.
8. The Frame_Sync_Service SHALL support a roster of 2 to 4 players per room.
9. The Game_Client SHALL provide teammate revival and cross-player buff mechanics in multiplayer mode.

### Requirement 9: 性能与帧率优化

**User Story:** AS 玩家, I want 在高低配置电脑上均保持流畅帧率, so that 割草体验稳定且无卡顿。

#### Acceptance Criteria

1. The Game_Client SHALL reuse monsters, skills, and particles through the Object_Pool.
2. WHILE an Entity is outside a configured distance from every player character, the Game_Client SHALL reduce that Entity update frequency.
3. The Wave_System SHALL distribute monster spawns across consecutive frames when a single frame spawn count exceeds the configured burst limit.
4. The Game_Client SHALL maintain a target frame rate of 60 frames per second on the 标准 Graphics_Tier reference hardware.
5. The Game_Client SHALL cache and reuse string and collection allocations during per-frame gameplay updates.
6. The Game_Client SHALL load large assets asynchronously.

### Requirement 10: 特效分级适配

**User Story:** AS 玩家, I want 特效强度匹配我的电脑配置, so that 视觉表现与硬件性能取得平衡。

#### Acceptance Criteria

1. WHILE the Graphics_Tier is 低配, the Game_Client SHALL disable screen shake, reduce particle layers, and disable glow overlay.
2. WHILE the Graphics_Tier is 标准, the Game_Client SHALL enable the default effect set.
3. WHILE the Graphics_Tier is 极致, the Game_Client SHALL enable multi-layer particles, trails, exposure, and screen shake.
4. WHEN a player changes the Graphics_Tier in settings, the Game_Client SHALL apply the changed tier to subsequently spawned effects.
5. WHEN the Game_Client detects sustained frame rate below the configured threshold, the Game_Client SHALL notify the player of the recommended Graphics_Tier.

### Requirement 11: 存档系统

**User Story:** AS 玩家, I want 我的解锁与积分持久保存, so that 进度在重装或换机后得以保留。

#### Acceptance Criteria

1. The Save_Data SHALL persist unlocked classes, the codex, accumulated points, and settings.
2. WHEN the Game_Client exits normally, the SaveManager SHALL write the current Save_Data to local storage.
3. WHEN the Game_Client starts, the SaveManager SHALL read local Save_Data and restore game progress.
4. WHEN network access is available, the SaveManager SHALL synchronize Save_Data with the cloud store.
5. IF a local Save_Data read fails, the SaveManager SHALL restore the most recent valid backup and notify the player.

### Requirement 12: 卡密授权

**User Story:** AS 发行方, I want 可控的授权校验, so that 未授权客户端的高级功能受到限制。

#### Acceptance Criteria

1. WHEN the Game_Client starts, the Security_Module SHALL verify the License_Key state.
2. WHEN a License_Key is valid, the Security_Module SHALL unlock the full feature set.
3. WHEN a License_Key is absent or expired, the Security_Module SHALL present an activation dialog and restrict the premium feature set.
4. WHEN network access is available, the Security_Module SHALL validate the License_Key against the authorization service.
5. WHILE network access is unavailable, the Security_Module SHALL apply local License_Key caching with an expiry check.

### Requirement 13: 防破解与完整性加固

**User Story:** AS 发行方, I want 客户端具备多层防护, so that 代码、内存与资源受到保护且被篡改时能够响应。

#### Acceptance Criteria

1. The Security_Module SHALL apply C# code obfuscation and constant hiding to release builds.
2. The Security_Module SHALL encrypt packaged configuration tables, textures, and audio resources.
3. WHEN the Security_Module detects a debugger attachment, the Security_Module SHALL record the event and apply the configured protection response.
4. WHEN the Security_Module detects a runtime value tamper on protected gameplay values, the Security_Module SHALL restore the authoritative value from the security baseline.
5. WHEN the Security_Module detects resource or assembly integrity mismatch, the Security_Module SHALL terminate the session and record the event.

### Requirement 14: 音频与氛围

**User Story:** AS 玩家, I want 与战斗节奏联动的音效氛围, so that 沉浸感与打击感得到强化。

#### Acceptance Criteria

1. WHILE a round is active, the Game_Client SHALL switch BGM segments according to the current wave intensity.
2. WHEN a skill activates, a kill occurs, an upgrade is chosen, or a BOSS spawns, the Game_Client SHALL play the corresponding sound effect.
3. WHEN an ultimate skill activates, the Game_Client SHALL play the low-frequency ultimate sound synchronized with the screen shake.

### Requirement 15: UI 与界面适配

**User Story:** AS 玩家, I want 清晰且适配的界面, so that 在高分屏与不同分辨率下操作信息可得。

#### Acceptance Criteria

1. The UIManager SHALL manage界面层级、弹窗显示与隐藏, and maintain a single active modal at a time.
2. The Game_Client SHALL provide the HUD, main menu, settings, room,结算, codex, and activation interfaces.
3. WHILE a round is active, the HUD SHALL display health, experience, layer, and skill slots.
4. WHEN the UI canvas scales, the Game_Client SHALL keep UI anchors aligned without offset or stretch.

### Requirement 16: 地图与场景体系

**User Story:** AS 玩家, I want 多样化的随机场景, so that 战斗环境具备变化与探索价值。

#### Acceptance Criteria

1. The Game_Client SHALL provide five map themes: 荒漠战场, 霓虹都市, 虚空秘境, 熔岩炼狱, 星空荒原.
2. WHEN a round starts, the Game_Client SHALL generate the map layout from the selected theme and a round seed.
3. The Game_Client SHALL place dynamic weather, random traps, hidden chests, and scene disaster mechanics according to the theme configuration.

### Requirement 17: 职业体系

**User Story:** AS 玩家, I want 差异化职业与流派, so that 开局选择带来不同玩法体验。

#### Acceptance Criteria

1. The Game_Client SHALL provide 8 playable classes at V2.0: 雷电法师, 烈焰狂战, 虚空猎手, 霓虹游侠, 磐石守卫, 星辰祭司, 暗影刺客, 次元使者.
2. WHEN a player selects a class, the Game_Client SHALL apply that class base stats, starting skill set, and class-specific growth.
3. The Game_Client SHALL cover the output, melee, support, summon, and control roles across the 8 classes.

### Requirement 18: 组合技体系

**User Story:** AS 玩家, I want 高表现的组合技, so that Build 成型后获得强反馈。

#### Acceptance Criteria

1. The Game_Client SHALL provide 24 Combo_Skill definitions at V2.0.
2. WHEN a designated skill pair both reach maximum level, the Skill_System SHALL unlock the matching Combo_Skill and play its dedicated full-screen effect.

### Requirement 19: 打包与分发

**User Story:** AS 发行方, I want 标准化的 Windows 交付物, so that 玩家可获得可安装可运行的正式版本。

#### Acceptance Criteria

1. The Game_Client SHALL build as a Windows standalone executable for Win10 and Win11.
2. The build pipeline SHALL package the executable into a standard installer that creates a desktop shortcut, bundles no third-party software, and completes green installation.
3. The build pipeline SHALL encrypt packaged asset bundles before distribution.

### Requirement 20: 商业化内容

**User Story:** AS 发行方, I want 合规且不影响平衡的付费设计, so that 收入与玩家体验可以并存。

#### Acceptance Criteria

1. The Game_Client SHALL offer skins, effect skins, and UI themes as purchasable cosmetic content.
2. The Game_Client SHALL offer hidden classes and limited effect content as unlockable content.
3. The Game_Client SHALL provide the full core gameplay to every player without a purchase.
4. The Game_Client SHALL keep cosmetic content free of gameplay numeric advantage.

## References

[^1]: 星界像素狂潮项目立项与开发文档（用户提交，2026-10-16）
