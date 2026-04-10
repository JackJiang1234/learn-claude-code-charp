# AgentLoop

基于 Anthropic Messages API 的简易控制台「编码代理」示例：在终端输入自然语言，模型可反复调用 `bash` 工具在当前工作目录执行命令，直到给出最终文本回复。

## 前置条件

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- 可访问 Anthropic API（或兼容 Messages API 的网关）

## 配置

配置按优先级合并（后者覆盖前者）：`appsettings.json` → `appsettings.{Environment}.json`（可选）→ [用户机密](https://learn.microsoft.com/dotnet/core/extensions/user-secrets) → 环境变量。

环境名由 `DOTNET_ENVIRONMENT` 或 `ASPNETCORE_ENVIRONMENT` 决定，未设置时视为 `Production`。

### `appsettings.json` 中的 `Anthropic` 节

| 键 | 说明 |
| --- | --- |
| `ModelId` | **必填。** 要调用的模型 ID。 |
| `BaseUrl` | 可选。API 基地址；不填则使用 SDK 默认。 |
| `AuthToken` | 可选。API 密钥；与 `BaseUrl` 均省略时使用 SDK 默认凭据（例如环境变量）。 |

### 环境变量（常用）

| 变量 | 作用 |
| --- | --- |
| `MODEL_ID` | 覆盖 `Anthropic:ModelId` |
| `ANTHROPIC_BASE_URL` | 覆盖 `Anthropic:BaseUrl` |
| `ANTHROPIC_AUTH_TOKEN` | 覆盖 `Anthropic:AuthToken` |
| `Anthropic__ModelId` 等 | .NET 分层键，等价于 JSON 中的 `Anthropic:ModelId` |

将密钥放在用户机密或环境变量中，避免提交到版本库。

## 运行

在仓库根目录或本目录执行：

```bash
dotnet run --project src/AgentLoop/AgentLoop.csproj
```

**工作目录**：程序使用**启动时的当前目录**作为代理的工作区（`BashRunner` 在此目录执行命令）。请在目标项目目录下启动，或在说明中按需 `cd`。

**退出**：输入 `q`、`exit`，或 EOF（Windows 上一般为 Ctrl+Z 后回车）。

## 行为说明

- **系统提示**：告知模型当前目录，并引导使用 Windows `cmd` 风格检查与修改工作区（在 Windows 上实际通过 `cmd.exe /c` 执行）。
- **工具**：仅注册 `bash` 一项，参数为 `command` 字符串。
- **Shell**：Windows 使用 `cmd.exe /c`；非 Windows 使用 `/bin/sh -c`。
- **命令执行**：单次最长等待 120 秒；合并标准输出与标准错误；输出过长会截断（见 `BashRunner` 中的常量）。
- **安全**：部分危险片段会被拦截并返回错误信息，而非执行（见 `BashRunner.DangerousFragments`）。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `Program.cs` | 配置加载、Anthropic 客户端构造、控制台主循环 |
| `Agent/AgentLoopEngine.cs` | 与 API 的多轮对话及 `bash` 工具调用 |
| `Agent/LoopState.cs` | 消息历史与轮次状态 |
| `Bash/BashRunner.cs` | 本地命令执行实现 |
| `Domain/` | 配置与运行时异常类型 |

## 依赖

主要 NuGet 包：`Anthropic`、`Microsoft.Extensions.Configuration.*`（含 JSON、环境变量、用户机密）。
