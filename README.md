# AnrylnroBannerlord

一个为 **Mount & Blade II: Dedicated Server** 提供 **HTTP 玩家数据接口** 的 Mod。

启动服务器后自动提供本地 API，可用于：

- OBS 数据叠加
- 外部统计工具
- 管理面板
- 自动化脚本
- 多实例数据聚合

支持 **单实例使用** 和 **主从多实例协同模式**。

---

## ✨ Features

- ✅ 游戏内玩家数据 HTTP API
- ✅ JSON 输出，易于集成
- ✅ 单实例开箱即用
- ✅ 多实例自动转为主从架构
- ✅ 自动请求转发
- ✅ API Key 鉴权
- ✅ 纯原生API，无需Harmony组件
- ✅ 使用官方同款Kestrel容器，无需管理员权限

---

# 🚀 Quick Start（普通用户）

> 如果你只是想使用插件获取玩家数据，只需要看这一部分。

## 1️⃣ 安装 Mod

1. 下载已编译好的 Mod
2. 放入 `Mount & Blade II Dedicated Server\Modules`

3. 在你的启动参数中添加模组 `AnrylnroBannerlord`

   **例子：**

   ```bash
   @echo off
   :restart
   Dedicatedcustomserver.starter.exe /dedicatedcustomserverconfigfile skirmish.txt /port 7640 _MODULES_*Native*Multiplayer*AnrylnroBannerlord*_MODULES_
   goto restart
   ```

4. 开放端口7011 （TCP）

5. 启动服务器

---

## 2️⃣ 测试是否运行成功

默认配置：

| 项目 | 默认值 |
|---|---|
| API Port | `7011` |
| API Key | `YourSecretKey` |

进入游戏后执行：

```bash
curl -H "X-API-KEY: YourSecretKey" http://127.0.0.1:7011/players
````

如果返回 JSON 数据（无玩家时通常仅返回 `[]`），说明运行成功 🎉

---

## 3️⃣ 修改端口或 API Key（不推荐）

配置项：

* `AnrylnroPort`（默认 `7011`）
* `AnrylnroApiKey`（默认 `YourSecretKey`）

⚠ 您的API Key仅用于获取玩家列表，不会对您的服务器造成威胁。

> 为了维护社区便捷性，我们建议您不要修改端口。虽然即便您修改了此端口也不会影响正常查询，因为您的端口信息会注册到我们的中央服务器上。

****

---

## 4️⃣ 常见疑问

**您不用关注端口占用问题！**

如果您在同一个服务器上运行多个实例，您仍然可不对AnrylnroPort进行任何修改。

当端口已被本插件另一实例占用时，会复用该端口。



---

# 🧩 API 示例

## 获取玩家列表

```
GET /players
```

示例：

```bash
curl -H "X-API-KEY: YourSecretKey" http://127.0.0.1:7011/players
```

---

# 🛠 Development（开发者说明）

> 以下内容面向开发者或项目接手者。

---

## 项目做什么

这是一个 Bannerlord Mod，核心是通过 HTTP 提供玩家数据。

支持多实例协同：

* 1 个主节点（共享 API 端口）
* 多个子节点（本地 API 端口）
* 主节点按 `gamePort` 转发请求

---

## 开发环境

* .NET SDK 6.x
* 目标框架：`net6.0`

构建：

```bash
dotnet build AnrylnroBannerlord.csproj -c Debug
```

说明：

* 由于引用 Bannerlord AMD64 库，MSIL/AMD64 警告属于预期现象。

---

## 项目结构

```
Api/        HTTP 服务、路由、主从协同
Network/    玩家追踪与快照
Utils/      日志、配置、JSON 工具
```

---

## API 模块结构

`ApiServer` 使用 partial 拆分：

* `Api/ApiServer.cs`：字段、常量、Start/Stop
* `Api/ApiServer.Hosting.cs`：监听循环、请求分发、鉴权入口
* `Api/ApiServer.Endpoints.cs`：端点处理与路由注册
* `Api/ApiServer.Cluster.cs`：心跳、选主、端口探测
* `Api/ApiServer.Lifecycle.cs`：生命周期注册/注销、HTTP 转发
* `Api/ApiEndpointAttribute.cs`：注解路由元数据

---

## `/players` 当前行为

```
GET /players?gamePort=xxxx
```

### 主节点请求自身

* 返回本地玩家快照
* 使用主节点 API Key 校验

### 主节点请求子节点

* 主节点转发请求到子节点 `/players`
* 透传请求头 `X-API-KEY`
* 子节点自行完成鉴权

这意味着：

即使主节点 Key 不匹配，只要目标子节点 Key 匹配，请求仍可成功。

---

## 如何新增端点

1. 在 `Api/ApiServer.Endpoints.cs` 添加处理方法
2. 使用：

```csharp
[ApiEndpoint("METHOD", "/path")]
```

3. 根据需要设置：

* `HostOnly`
* `ChildOnly`

4. 明确鉴权策略（本地校验或转发透传）

---

## 常见问题排查

| 问题     | 原因                   |
| ------ | -------------------- |
| 端口启动失败 | 端口被其他进程占用            |
| 转发 404 | 子节点未注册或 gamePort 错误  |
| 403    | API Key 不匹配或请求命中错误节点 |

---

## License

![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)