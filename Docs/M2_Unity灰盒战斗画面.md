# M2 任务说明:Unity 灰盒战斗画面

给在本机运行、能打开 Unity 的 Claude Code session。开始前先读 `CLAUDE.md`、`GDD.md`(第 2、13 节)、`ROADMAP.md`。

## 目标

做一个能在 Unity 里玩的灰盒战斗场景,用来验证"40+ 人按兵团交替行动 + 五级指挥里的第 1、4、5 级"(D7~D9、D36、D37)好不好玩。
画面只求看得清,不做美术。

## 已经有的东西

- `Assets/_Project/Scripts/Core/` 纯 C# 战斗核心(程序集 `SRPG.Core`,不依赖 UnityEngine),29 个测试通过。
- `Srpg.Core.Combat.Sandbox.SandboxBattle.Create(seed)` 生成 40×24 的测试战场,双方各 4 个兵团 × 12 人。敌方"要塞卫队"是第 4 级固守,其余全是第 5 级自动。
- 界面需要的接口都在 `Battle` 上:
  - 流程:`Start()`、`ActiveCorps`、`ControllableUnits(corps)`、`EndActivation()`、`RunAiActivation()`、`Outcome`、`Round`、`TurnLimit`、`TurnOrder`
  - 查询:`UnitAt(pos)`、`Destinations(unit)`、`AttackTargetsFrom(unit, pos)`、`PickUpTargetsFrom(unit, pos)`、`Forecast(...)`、`CanCounter(...)`、`EffectiveStats(unit)`、`AuraLeadership(unit, pos)`
  - 操作:`TryExecute(UnitCommand, out error)`,`UnitCommand.Wait / Attack / PickUp`
  - 结果:`Score(Side.Player)`、`CapturedBy(Side.Player)`、`Log`
  - 指挥:直接改 `corps.Level`(Manual / Coarse / Auto)和 `corps.Order`(`CorpsOrder.AttackNearest / AttackAt / HoldAt / RetreatTo / Follow`)

## 要做的

1. **程序集**:新建 `Assets/_Project/Scripts/Game/`,程序集 `SRPG.Game`,引用 `SRPG.Core`。所有 MonoBehaviour 放这里。
2. **场景**:`Assets/_Project/Scenes/BattleSandbox.unity`,场景里只放一个启动组件,地图、单位、界面都由代码生成,避免场景文件难以合并。
3. **地图显示**:俯视正交相机。每格一个方块,按地形着色(平地、森林、山地、堡垒、水、墙)。
4. **单位显示**:
   - 按阵营着色,每个兵团一个可区分的色调或字母标记,兵团长要能一眼认出。
   - 每个单位头顶显示 HP 条和士气条。
   - 状态要区分:动摇(士气低于 30)、溃逃、打晕、投降、扛着俘虏。
5. **兵团面板**:
   - 按 `TurnOrder` 列出所有兵团,高亮当前行动的兵团,显示每个兵团的在场人数。
   - 我方兵团可以随时切换指挥级别(D9):手操 / 粗粒度 / 观战。
   - 选粗粒度时可以下指令:进攻最近敌人 / 进攻所点格子 / 固守所点格子 / 撤退到所点格子 / 跟随某兵团。
6. **手操(第 1 级)**:
   - 轮到手操兵团时:点击未行动的单位,高亮 `Destinations`;点击目标格子,再显示从该格能攻击或扛起的目标。
   - 悬停或选中敌人时显示战斗预测:命中、伤害 × 次数,以及对方能否反击和反击数值。
   - 确认后执行攻击、扛起或待机;另有"结束本兵团行动"按钮。
   - 操作失败时显示 `error` 文本。
7. **AI 兵团(第 4、5 级)**:轮到时自动执行 `RunAiActivation()`。要有"单步 / 自动播放"开关和速度调节,能看清每个兵团做了什么。溃逃单位由核心自动处理,手操兵团也一样。
8. **日志与结算**:画面上显示最近若干行 `Log`;战斗结束时弹出结果、`Score`、俘获名单。
9. **种子**:界面上可以输入种子并重开战斗。

## 规则

- 战斗状态只能通过 `Battle` 的方法改变,界面层不写任何战斗规则。
- 如果界面需要核心里没有的查询,就加到 `SRPG.Core` 并补测试,不要在界面里重算规则。
- `SRPG.Core` 不能引用 UnityEngine。改了核心以后运行 `dotnet test Tools/CoreTests`(需要 .NET 8 SDK)或 Unity Test Runner 的 EditMode 测试。
- Unity 生成的 `.meta`、`Packages/`、`ProjectSettings/` 要提交;`Library/` 等已在 `.gitignore` 里。
- 需要设计决定时(比如界面布局偏好)问用户,不要自行扩展玩法。

## 验收

- 打开 `BattleSandbox` 进入 Play:能用手操打完一个我方兵团的行动,能把某个兵团切到观战后看 AI 打,能切换粗粒度指令并看到效果,能用棍打晕敌人并扛起。
- EditMode 测试全部通过。
- 截几张图给用户看:开局、手操选中单位、战斗预测、战斗结束结算。
- 提交并推送到 `claude/cloud-session-check-w325l0` 分支。
