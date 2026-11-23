# Muse Desktop + GooseAPI 联调操作手册

## 一、环境准备

### 1.1 必需组件

| 组件 | 版本 | 说明 |
|------|------|------|
| .NET 8 SDK | 8.0.x | 编译和运行前后端 |
| SQL Server | 2019+ | GooseAPI 数据库，支持 LocalDB / Express / 完整版 |
| WebView2 Runtime | Evergreen | Muse 桌面依赖，Windows 10/11 通常已自带 |

检查 WebView2：
```powershell
# 如果输出版本号，说明已安装
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv
```

未安装时从官网下载：[https://developer.microsoft.com/microsoft-edge/webview2/](https://developer.microsoft.com/microsoft-edge/webview2/)

### 1.2 数据库准备

GooseAPI 使用 **Windows 身份验证（Integrated Security）** 连接 SQL Server。

**方案 A：SQL Server Express / LocalDB（推荐开发用）**
```powershell
# 检查是否安装了 LocalDB
sqllocaldb i
# 创建实例（如果没有）
sqllocaldb c "MSSQLLocalDB"
sqllocaldb s "MSSQLLocalDB"
```

若使用 LocalDB，需修改 `GooseAPI/src/Goose.Api/appsettings.json` 中的连接字符串：
```json
"ConnectionStrings": {
  "GooseDb": "Server=(localdb)\\MSSQLLocalDB;Database=GooseDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```

**方案 B：完整 SQL Server 实例**
```json
"ConnectionStrings": {
  "GooseDb": "Server=localhost;Database=GooseDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```

> 第一次启动时，EF Core 会自动创建 `GooseDb` 数据库并应用迁移，无需手动建库。

---

## 二、启动 GooseAPI 后端

### 2.1 编译并运行

```powershell
cd D:\Labs\muse-desktop-main\GooseAPI\src\Goose.Api
dotnet run
```

成功启动后控制台输出：
```
info: Goose.Api[0]
      GooseDb schema is up to date
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://127.0.0.1:5080
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### 2.2 验证 API

另开一个终端：

```powershell
# 健康检查
curl http://127.0.0.1:5080/api/health
# 应返回 JSON，databaseConnected 为 true

# 查看 Swagger 文档
start http://127.0.0.1:5080/swagger
```

### 2.3 后端配置清单

| 文件 | 关键配置 |
|------|----------|
| `appsettings.json` | `Kestrel:Endpoints:Http:Url` → API 监听地址 |
| `appsettings.json` | `ConnectionStrings:GooseDb` → 数据库连接字符串 |
| `appsettings.json` | `Database:AutoMigrate` → 自动迁移（默认 true） |

> 如果监听地址不是 `127.0.0.1:5080`，桌面端的 `appsettings.json` 必须同步修改。

---

## 三、配置 Muse 桌面端连接后端

### 3.1 开发模式（源码运行）

修改 `src/Muse.Desktop/appsettings.json`：

```json
{
  "GooseApi": {
    "BaseUrl": "http://127.0.0.1:5080/",
    "TimeoutSeconds": 10,
    "HealthPollSeconds": 30
  }
}
```

参数说明：
- `BaseUrl`：GooseAPI 根地址，**必须与后端实际监听地址完全一致**
- `TimeoutSeconds`：HTTP 请求超时（秒）
- `HealthPollSeconds`：状态条轮询间隔（秒）

编译运行：
```powershell
cd D:\Labs\muse-desktop-main
dotnet run --project src/Muse.Desktop/Muse.Desktop.csproj
```

### 3.2 发布模式（dist/win-x64 运行）

发布产物 `dist/win-x64/appsettings.json` 已包含默认配置：

```json
{
  "GooseApi": {
    "BaseUrl": "http://127.0.0.1:5080/",
    "TimeoutSeconds": 10,
    "HealthPollSeconds": 30
  }
}
```

如需修改 API 地址，直接编辑 `dist/win-x64/appsettings.json` 即可，无需重新编译。

---

## 四、启动 Muse 桌面端

### 4.1 启动顺序

**必须先启动 GooseAPI，再启动 Muse。** 桌面端启动时会立即向后端发送健康检查请求。

```powershell
# 终端 1：启动后端
cd GooseAPI\src\Goose.Api
dotnet run

# 终端 2：启动桌面端（开发模式）
dotnet run --project src\Muse.Desktop\Muse.Desktop.csproj

# 或启动已发布版本
dist\win-x64\Muse.exe
```

### 4.2 验证连接状态

桌面端主窗口底部状态条显示：

| 状态 | 颜色 | 含义 |
|------|------|------|
| Goose API: connected | 绿色 | 后端在线，数据库正常 |
| Goose API: degraded | 橙色 | 后端在线，但数据库异常 |
| Goose API: offline | 灰色 | 后端未启动或网络不通 |
| Goose API: checking | 灰色 | 首次检测中 |

### 4.3 验证会话功能

点击状态条右侧的 **Sessions** 按钮，打开会话管理窗口：

1. **New** → 创建新会话
2. 在左侧列表点击会话 → 右侧显示消息历史
3. 选择 Role（user/assistant/system），输入内容，点击 **Send** → 追加消息
4. **Delete** → 删除当前选中的会话

所有操作实时同步到 GooseAPI → SQL Server。

---

## 五、常见问题排查

### 5.1 状态条一直显示 "offline"

1. **检查 GooseAPI 是否已启动**
   ```powershell
   curl http://127.0.0.1:5080/api/health
   ```
   如果返回 `Connection refused`，说明后端未启动。

2. **检查地址是否匹配**
   - 后端 `appsettings.json` 中的 `Kestrel:Endpoints:Http:Url`
   - 桌面端 `appsettings.json` 中的 `GooseApi:BaseUrl`
   - 两者必须完全一致（包括 `http` vs `https`、端口号）

3. **防火墙拦截**
   如果 API 监听在 `0.0.0.0` 或特定网卡，检查 Windows Defender 防火墙是否放行该端口。

### 5.2 数据库连接失败

后端日志中出现：
```
GooseDb migration failed
```

排查步骤：
1. SQL Server 服务是否运行：`services.msc` → 找到 SQL Server 实例 → 确保状态为"正在运行"
2. 连接字符串是否正确：实例名、是否使用 Windows 身份验证
3. 当前 Windows 用户是否有权限创建数据库
4. 如果是 LocalDB，确保实例已启动：`sqllocaldb s "MSSQLLocalDB"`

### 5.3 WebView2 初始化失败

弹窗提示：
> "Muse needs the Microsoft Edge WebView2 Runtime"

解决方案：下载并安装 [WebView2 Evergreen Standalone Installer](https://developer.microsoft.com/microsoft-edge/webview2/)。

### 5.4 第二次启动 Muse 没反应

这是正常行为。Muse 使用命名互斥锁 `Local\Muse.Desktop` 保证单实例运行，第二次启动会激活已存在的窗口，然后自身退出。

---

## 六、端口与地址速查

| 服务 | 默认地址 | 修改位置 |
|------|----------|----------|
| GooseAPI | `http://127.0.0.1:5080` | `GooseAPI/src/Goose.Api/appsettings.json` → `Kestrel:Endpoints:Http:Url` |
| Muse 桌面端 | 读取上述地址 | `src/Muse.Desktop/appsettings.json` → `GooseApi:BaseUrl` |

### 修改端口示例

把后端改为 8080：
```json
// GooseAPI/src/Goose.Api/appsettings.json
"Kestrel": {
  "Endpoints": {
    "Http": {
      "Url": "http://127.0.0.1:8080"
    }
  }
}
```

同步修改桌面端：
```json
// src/Muse.Desktop/appsettings.json
"GooseApi": {
  "BaseUrl": "http://127.0.0.1:8080/"
}
```

> 修改后需要重新启动后端和桌面端才能生效。

---

## 七、文件目录速览

```
D:\Labs\muse-desktop-main\
├── Muse.Desktop.sln              # 桌面端解决方案
├── src/
│   ├── Muse.Core/                # 策略核心库（导航、下载、权限）
│   └── Muse.Desktop/             # WPF 桌面应用
│       ├── appsettings.json      # ← API 连接配置
│       ├── MainWindow.xaml       # 主窗口（含 WebView2 + 状态条）
│       ├── SessionsWindow.xaml   # 会话管理窗口
│       └── Services/
│           ├── GooseApiClient.cs # HTTP 客户端
│           ├── AppSettings.cs    # 配置读取
│           └── WindowStateService.cs  # 窗口状态持久化
├── tests/
│   └── Muse.Core.Tests/          # xUnit 单元测试（48 个）
├── GooseAPI/
│   └── src/
│       └── Goose.Api/            # ASP.NET Core 8 Web API
│           ├── appsettings.json  # ← 数据库 + Kestrel 配置
│           ├── Program.cs        # 启动入口（含自动迁移）
│           ├── Data/
│           │   └── GooseDbContext.cs
│           ├── Models/           # Conversation, Message, Setting
│           ├── Contracts/        # DTO 定义
│           └── Controllers/      # Health, Conversations, Settings
├── tools/
│   ├── publish.ps1               # 发布脚本
│   └── install.ps1               # 安装脚本
└── dist/
    └── win-x64/                  # 自包含发布产物
        ├── Muse.exe              # 可执行文件
        └── appsettings.json      # ← 运行时配置（可独立修改）
```

---

## 八、一键启动脚本（可选）

创建 `start-all.ps1`：

```powershell
$ErrorActionPreference = 'Stop'

# 启动 GooseAPI（后台任务）
$apiJob = Start-Job {
    Set-Location D:\Labs\muse-desktop-main\GooseAPI\src\Goose.Api
    dotnet run --no-launch-profile
}

# 等待 API 就绪
Write-Host "Waiting for GooseAPI..."
$maxWait = 30
$elapsed = 0
while ($elapsed -lt $maxWait) {
    try {
        $resp = Invoke-RestMethod -Uri "http://127.0.0.1:5080/api/health" -TimeoutSec 2
        if ($resp.databaseConnected) {
            Write-Host "GooseAPI is ready." -ForegroundColor Green
            break
        }
    } catch {}
    Start-Sleep -Seconds 1
    $elapsed++
}

if ($elapsed -ge $maxWait) {
    Write-Error "GooseAPI failed to start within $maxWait seconds."
    exit 1
}

# 启动 Muse
& D:\Labs\muse-desktop-main\dist\win-x64\Muse.exe

# Muse 关闭后，停止 API
Stop-Job $apiJob
Remove-Job $apiJob
```

运行：
```powershell
.\start-all.ps1
```
