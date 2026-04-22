# SmartPCAssistant - 智能电脑助手

一款全智能控制电脑的 AI 助手程序，让你一句话就可以操作电脑做任何事。

## 功能特性

### AI 对话
- 支持 OpenAI、Claude、DeepSeek、Gemini 等多种 AI 提供商
- 自定义 OpenAI 兼容 API 端点
- 完整的对话上下文支持
- 多会话管理和历史记录

### 智能语音
- 语音输入（需 Windows 语音识别支持）
- 语音播报回复内容
- 语音交互开关可配置

### 自动化执行
- 自然语言任务解析
- 智能打开/关闭应用程序
- PowerShell 命令执行
- UI Automation 桌面控制
- 浏览器搜索和学习功能

### 安全隔离
- Windows 沙箱隔离执行
- 可配置的沙箱模式
- 操作日志完整记录

### 现代化界面
- 深色主题 Fluent 设计
- 悬浮球快速入口
- 可拖拽悬浮球
- 历史记录面板
- 设置管理界面
- 日志查看器

## 系统要求

- Windows 10/11
- .NET 8.0 Runtime
- 2GB 可用内存
- 100MB 磁盘空间

## 快速开始

### 从源码构建

```bash
cd SmartPCAssistant
dotnet restore
dotnet build
dotnet run
```

### 发布为可执行文件

```bash
cd SmartPCAssistant
dotnet publish -c Release -r win-x64 --self-contained
```

生成的文件位于 `bin/Release/net8.0-windows/win-x64/publish/`

### 运行发布版本

```bash
cd bin/Release/net8.0-windows/win-x64/publish
./SmartPCAssistant.exe
```

## 使用说明

### 基本对话
在输入框中输入你的指令或问题，AI 会理解并执行。

### 语音输入
点击 🎤 按钮开始语音输入。

### 搜索学习
点击 🔍 按钮让 AI 搜索并学习如何完成特定任务。

### 快捷命令
- `新对话` - 开始新的对话会话
- `历史` - 查看对话历史
- `设置` - 打开设置面板

### AI 提供商配置
1. 点击 ⚙️ 打开设置
2. 选择 AI 提供商
3. 输入 API Key
4. 选择或输入模型
5. 点击保存

支持的自定义配置：
- 自定义 API 端点
- OpenAI 兼容接口
- 多种 AI 模型

## 项目结构

```
SmartPCAssistant/
├── Models/                 # 数据模型
│   └── Models.cs
├── Services/              # 核心服务
│   ├── AiProviderService.cs   # AI 提供商
│   ├── ConfigService.cs        # 配置管理
│   ├── DatabaseService.cs     # 数据库
│   ├── ExecutorService.cs     # 命令执行
│   ├── HotkeyService.cs       # 快捷键
│   ├── LearningService.cs      # 搜索学习
│   ├── SessionService.cs      # 会话管理
│   ├── SpeechService.cs        # 语音服务
│   ├── TaskEngine.cs          # 任务引擎
│   └── TrayService.cs         # 托盘服务
├── ViewModels/            # 视图模型
│   ├── MainWindowViewModel.cs
│   ├── SettingsViewModel.cs
│   └── LogViewerViewModel.cs
├── Views/                 # 视图
│   ├── MainWindow.axaml
│   ├── FloatBallWindow.axaml
│   ├── SettingsWindow.axaml
│   └── LogViewerWindow.axaml
├── App.axaml              # 应用入口
└── Program.cs            # 程序入口
```

## 配置说明

配置文件位于：
```
%LOCALAPPDATA%\SmartPCAssistant\config.json
```

日志文件位于：
```
%LOCALAPPDATA%\SmartPCAssistant\logs\
```

数据库文件位于：
```
%LOCALAPPDATA%\SmartPCAssistant\data.db
```

## 技术栈

- **UI 框架**: Avalonia 11.2
- **架构模式**: MVVM
- **数据库**: SQLite
- **日志**: Serilog
- **AI SDK**: 原生 HTTP 调用

## License

MIT License
