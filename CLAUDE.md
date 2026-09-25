# 给 AI 助手的工程说明

用户要求**一律用中文回复**。设计以 `GDD.md` 为准,开发顺序看 `ROADMAP.md`。本工程从零设计,**不参考任何旧版设计或代码**。

## 目录

- `Assets/_Project/Scripts/Core/` — 纯 C# 游戏逻辑(程序集 `SRPG.Core`)。`Combat/` 是战斗核心。
- `Assets/_Project/Tests/EditMode/` — NUnit 测试,Unity Test Runner 和命令行共用同一份。
- `Tools/CoreBuild/` — 在 Unity 之外按 netstandard2.1 + C# 9 编译 Core,保证与 Unity 兼容。
- `Tools/CoreTests/` — 命令行测试工程。
- `Tools/BattleSim/` — 全自动对战的 ASCII 观战模拟器,用来目测兵团 AI。

## 命令(仓库根目录)

- 测试:`dotnet test Tools/CoreTests`
- 观战模拟:`dotnet run --project Tools/BattleSim -- [种子] [--every N] [--log]`
- 云端容器没有 .NET 时:`apt-get install -y dotnet-sdk-8.0`(Ubuntu 官方源可用)

## 架构规则

1. `Scripts/Core` 不得引用 UnityEngine(asmdef 设了 `noEngineReferences`)。只能用 netstandard2.1 的 API 和 C# 9 语法;`Tools/CoreBuild` 会按这个标准编译,警告视为错误。
2. 随机数只用 `Srpg.Core.Rng`,不用 `System.Random` 或 `UnityEngine.Random`,保证同一种子结果可复现。
3. 可调数值集中放在配置类(战斗是 `BattleConfig`),不要写死在逻辑里。GDD 第 13 节的数值都是暂定值。
4. 玩家和 AI 的操作都走 `Battle.TryExecute`,界面不直接改战斗状态。
5. 测试不要依赖暂定数值的具体大小,需要时从 `BattleConfig` 推算。
