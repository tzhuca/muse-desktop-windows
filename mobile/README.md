# Muse Mobile

.NET MAUI 移动应用，支持 **iOS** 和 **Android**，连接 GooseAPI 后端。

## 功能

| 功能 | 说明 |
|------|------|
| WebView 加载 Muse | 主页面嵌入 `https://muse.ai` |
| API 健康状态条 | 底部状态条轮询 `/api/health`，绿/橙/灰三色指示 |
| 会话管理 | Sessions 页面：创建、删除、查看会话列表 |
| 消息收发 | Conversation 页面：查看消息历史，发送新消息（user/assistant/system） |
| 共享策略 | 引用 `Muse.Core` 项目，复用桌面端的 URL 策略和下载逻辑 |

## 项目结构

```
mobile/
├── Muse.Mobile.csproj          # MAUI 项目文件
├── appsettings.json            # GooseAPI 连接配置
├── MainPage.xaml/.cs           # 主页面（WebView + 状态条）
├── SessionsPage.xaml/.cs       # 会话列表页面
├── ConversationPage.xaml/.cs   # 消息详情页面
├── Services/
│   ├── GooseApiClient.cs       # HTTP 客户端（复用桌面端 DTO）
│   └── AppSettings.cs          # 配置读取
├── Platforms/
│   ├── Android/                # Android 特定配置
│   │   ├── AndroidManifest.xml
│   │   └── Resources/xml/network_security_config.xml
│   └── iOS/                    # iOS 特定配置
│       └── Info.plist
└── Resources/                  # 图标、字体、样式
```

## 构建

### 前提条件

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [MAUI 工作负载](https://learn.microsoft.com/dotnet/maui/get-started/installation)
- **Android**: Android SDK + JDK 17+（通过 Android Studio 或 Visual Studio 安装）
- **iOS**: macOS + Xcode（Windows 上需连接远程 Mac）

安装 MAUI 工作负载：
```bash
dotnet workload install maui
```

### 构建命令

```bash
# iOS（macOS 或连接远程 Mac）
dotnet build Muse.Mobile.csproj -f net9.0-ios

# Android
dotnet build Muse.Mobile.csproj -f net9.0-android

# 同时构建所有目标
dotnet build Muse.Mobile.csproj
```

### 运行

```bash
# iOS 模拟器
dotnet build Muse.Mobile.csproj -f net9.0-ios -t:Run

# Android 模拟器/设备
dotnet build Muse.Mobile.csproj -f net9.0-android -t:Run
```

## 配置

编辑 `appsettings.json` 修改 GooseAPI 地址：

```json
{
  "GooseApi": {
    "BaseUrl": "http://127.0.0.1:5080/",
    "TimeoutSeconds": 10,
    "HealthPollSeconds": 30
  }
}
```

> **注意**: 移动端使用 `127.0.0.1` 只能访问设备本机服务。如果 GooseAPI 运行在电脑上，需要将 `BaseUrl` 改为电脑的局域网 IP（如 `http://192.168.1.100:5080/`）。

### 局域网调试

1. 确保电脑和手机在同一 WiFi 下
2. 找到电脑的局域网 IP：`ipconfig`（Windows）或 `ifconfig`（macOS）
3. 修改 `appsettings.json` 中的 `BaseUrl`
4. 确保电脑防火墙放行 5080 端口

## 平台特定配置

### Android

- `AndroidManifest.xml` 中已配置 `usesCleartextTraffic="true"` 和 `networkSecurityConfig`，允许 HTTP 明文通信（开发用）
- 生产环境应使用 HTTPS，并移除明文通信配置

### iOS

- `Info.plist` 中已配置 `NSAppTransportSecurity` → `NSAllowsArbitraryLoads`，允许 HTTP（开发用）
- 提交 App Store 前应移除此配置，改用 HTTPS

## 已知限制

1. **WebView 导航策略**: 移动端 WebView 的导航控制不如桌面端 WebView2 精细。目前仅加载 Muse 首页，外部链接由系统浏览器处理。
2. **OAuth 弹窗**: 部分身份提供商可能不支持在移动 WebView 中完成 OAuth。如遇问题，建议在系统浏览器中完成登录后返回应用。
3. **推送通知**: 当前版本暂未实现原生推送通知，依赖 WebView 内的网页通知。
