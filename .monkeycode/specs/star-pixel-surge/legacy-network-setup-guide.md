# StarPixelSurge 联网环境配置指南

> **归档说明**：本文来自已废弃的 Netcode 模板工程
> `D:\UnityProjects\StarPixelSurge\StarPixelSurge`（UOS/Netcode 模板生成，2026-10-16 创建，同日删除）。
> 该路线把脚本放在 `Assets/Scripts/Network`，与本仓库现行的 `Assets/05_Network` 实现无关，仅作记录。

## 已完成的配置

### 包安装
| 包 | 版本 | 状态 |
|---|---|---|
| Netcode for GameObjects | 1.15.1 | ✅ |
| Multiplayer Services（含 Relay + Lobby） | 2.2.3 | ✅ |
| Vivox Voice Chat | 16.11.0 | ✅ |

### 脚本框架（Assets/Scripts/Network/）
```
Scripts/
├── Network/
│   ├── StarPixelSurge.Network.asmdef
│   ├── NetworkBootstrap.cs          ← 启动入口，挂到场景即可
│   ├── Core/
│   │   └── AuthenticationManager.cs ← Unity Services 匿名登录
│   ├── Relay/
│   │   └── RelayManager.cs          ← 中继服务器联机
│   ├── Lobby/
│   │   └── LobbyManager.cs          ← 房间大厅
│   └── Voice/
│       └── VivoxVoiceManager.cs     ← 小队语音
└── UI/
    └── NetworkTestUI.cs            ← 测试 UI（UI Toolkit）
```

---

## 你需要在 Unity Editor 中完成的步骤

### 第 1 步：绑定 Unity Cloud 项目（必须）

Relay / Lobby / Vivox 都需要绑定云项目才能使用：

1. 打开 Unity Editor，加载 StarPixelSurge 项目
2. 顶部菜单 → **Edit → Project Settings → Services**
3. 点击 **"Connect to Unity Cloud"**
4. 登录你的 Unity 账号
5. 创建一个新的项目（或选择已有项目），组织选你的个人组织
6. 绑定后，在 Services 窗口确认以下服务已启用：
   - **Relay** → 启用
   - **Lobby** → 启用
   - **Vivox** → 启用（首次启用可能需要同意条款）

> 如果没有 Unity 账号，先去 https://id.unity.com 注册一个（免费）。

### 第 2 步：在场景中放置 NetworkBootstrap

1. 打开 `Assets/Scenes/SampleScene.unity`
2. 在 Hierarchy 中右键 → **Create Empty**，命名为 `NetworkManagerSystem`
3. 选中它，Inspector → **Add Component** → 添加 `NetworkBootstrap`
4. NetworkBootstrap 会自动创建：
   - NetworkManager（含 UnityTransport）
   - AuthenticationManager
   - RelayManager
   - LobbyManager
   - VivoxVoiceManager

### 第 3 步：快速测试（可选）

在场景中创建一个空物体，挂上 `NetworkTestUI`（需要配合 UIDocument 和 UXML）。
或者直接在代码中调用：

```csharp
// 主机
await NetworkBootstrap.Instance.HostGameAsync();

// 客户端（输入房间码）
await NetworkBootstrap.Instance.JoinGameAsync("ABCDE");
```

---

## 联机流程说明

```
主机流程：
  匿名登录 → 创建 Relay 分配 → 获取加入码
  → 创建 Lobby（存入 Relay 加入码）
  → 加入语音频道（用 Lobby ID）
  → NetworkManager.StartHost()

客户端流程：
  匿名登录 → 输入房间码加入 Lobby
  → 从 Lobby 数据读取 Relay 加入码
  → 加入 Relay 分配
  → 加入语音频道（用 Lobby ID）
  → NetworkManager.StartClient()
```

## 注意事项

- **开发阶段**使用匿名登录即可，无需接入真实账号系统
- **Relay 区域**默认自动选择最近节点，国内玩家通常走亚太区
- **Vivox 语音**首次启用需要在 Cloud 控制台同意服务条款
- 测试联机时，用 **Build Settings → Build** 打出一个客户端，和 Editor 里的主机对连
