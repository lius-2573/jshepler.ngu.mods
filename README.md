# jshepler.ngu.mods 精简版 — NGU Idle 模组

基于 [jshepler](https://github.com/jshepler) 的 NGU Idle 模组 `jshepler.ngu.mods` 精简、维护而来,仅保留核心常用功能。以 BepInEx 5.4.21 插件形式运行于 NGU Idle。

## 原仓库与本仓库

| 项目 | 地址 |
|---|---|
| 原仓库(作者 jshepler) | <https://github.com/jshepler/jshepler.ngu.mods> |
| 本仓库(fork/精简版) | `git@github.com:lius-2573/jshepler.ngu.mods.git` |

原版功能数量庞大(200+ 项),完整功能列表与 GO(Gear Optimizer)集成说明见原仓库 README。本精简版只保留下文列出的功能。

## 兼容性

- 游戏:NGU Idle(实测存档 Build 1260,程序集 `Assembly-CSharp.dll` 2024-07 版)
- 运行环境:BepInEx 5.4.21 x64、Unity 2019.4.22、.NET Framework 4.8
- 兼容常见汉化包:若汉化包替换了游戏内字符串字面量,模组相关显示增强会自动跳过,不影响游戏运行
- 模组版本:1.30.1(`jshepler.ngu.mods.dll`)

## 功能支持

### 自动化功能

| 功能 | 配置文件开关(默认值) | 游戏内热键 | 说明 |
|---|---|---|---|
| 自动收割/食用 Yggdrasil 果实 | `[Yggdrasil] AutoHarvest`(true) | Yggdrasil 按钮 **Shift+右键** 切换(变浅蓝) | 任一果实达到最高等级时自动全部食用;转生时自动收获/食用 ≥1 级果实(此项常驻,不受开关控制) |
| 钱坑自动投金币 | `[MoneyPit] AutoToss`(true) | 钱坑按钮 **Shift+右键** 切换(浅蓝) | 钱坑冷却结束(`canToss`)即自动投入金币并弹出提示 |
| 每日转盘自动转 | `[DailySpin] AutoSpin`(true) | 钱坑按钮 **Alt+右键** 切换(绿色) | 每日转盘就绪(`canSpin`)即自动转动并弹出结果提示 |
| 血魔法自动施铁柱 | `[BloodMagic] AutoCast`(true) | 血魔法菜单按钮 **Ctrl+右键** 切换(黄色/绿色) | 击败 boss 37 解锁后,冒险法术冷却结束即自动施放铁柱(Iron Pill);Ctrl+右键与仪式魔力的 Shift+右键共用血魔法菜单按钮,两种状态使用不同颜色区分 |
| 时光机器自动分配能量/魔力 | `[TimeMachine] AutoAllocateEnergy`(true) | 时光机器按钮 **Shift+右键** 切换(浅蓝) | 时光机器解锁后，速度与金币倍增两条进度分别自动分配达到下一等级所需的能量/魔力;分别优先取空闲池，不足时从对应 NGU 释放，达到目标后停止重新分配 |
| 高级训练自动切换能量 | `[AdvancedTraining] AutoAllocateEnergy`(true) | 高级训练按钮 **Shift+右键** 切换(浅蓝) | 高级训练解锁后，所有未完成训练分配下一级100%等级上限能量;完成的训练能量回到全局空闲池 |
| 多资源自动上限分配 | `[AutoAllocation]`(true) | 挂件、Wandoos、NGU 菜单按钮 **Shift+右键**;血魔法菜单 **Shift+右键** 切换仪式魔力 | 每次检查先回收已由自动化管理的分配，再按 `Priority` 重新计算上限并分配;包括挂件能量/升级能量、血魔法魔力、时光机器能量/魔力、高级训练、Wandoos能量/魔力、NGU能量/魔力和 Wishes 的能量/魔力/Res3 |
| 自动许愿资源分配与续愿 | `[Wishes] AutoAllocate`(false) | 许愿按钮 **Shift+右键** 切换(浅蓝) | 每 1 秒检查一次;汇总当前运行许愿与空闲池中的三种资源后重新平均分配(各槽位最多相差 1);存在空槽时按当前许愿列表顺序自动开启下一个未满级、未锁定、未运行的许愿(加载存档后空槽即自动补位,无需等待满级事件) |
| 卡牌自动整理与自动出牌 | `[Cards] AutoCast.Enabled`(false);排序默认开启 | Cards 按钮 **Shift+右键** 切换自动出牌(浅蓝);卡牌界面 `S`=排序、`Y`=手动执行弃牌 | 自动排序/弃牌/保护 Chonker 沿用原版逻辑。自动出牌全局运行，每次检查最多一张，始终选择当前排序字段下的高优先级普通卡或 Chonker；跳过 `THE END` 与普通受保护卡。Chonker 保持保护直到被选中且蛋黄充足，出牌时临时解除保护；蛋黄不足时暂停无关生成器，只运行目标卡缺少的类型并在槽位不足时轮换；目标完成或自动出牌关闭后恢复之前的生成器状态 |
| 自动合并/转化吊坠与 Looty | `[AutoMergeTransform] Enabled`(true) | — | 执行合并时自动把吊坠/Looty/Flubber 合成到最高级并转化为下一阶段物品(A9/lootz 除外),可一次跑多轮直至无可转化 |
| 自动使用黄油 | `[Questing] AutoButter`(true) | 野兽任务界面"黄油"按钮 **Shift+右键** 切换(浅蓝) | 开始主线任务时自动使用黄油(有黄油、非挂机模式、未使用过黄油时) |
| 自动手动主线任务 | 状态存入存档(无配置文件项) | 野兽按钮 **Shift+右键** 切换(浅蓝);普通**右键** = 收集任务物品 | 自动领取/完成手动主线任务:自动收集掉落、自动跳转任务区域、完成后自动接续下一个;取消勾选游戏内"使用主线"即停止。转生结算后若不再处于手动主线,自动关闭。**银行主线积攒达到阈值自动开跑**:关闭状态下积攒数达到存档键 `AutoQuestingStartThreshold`(默认 `0` = 跟随当前银行上限,攒满即触发;设 >0 可指定具体阈值)时自动开启并开始做主线,自动开启 beast、退出挂机模式并切换到主线;做到积攒数为 0 自动停止。右键显式关闭后不再自动开启,需手动重新打开 |
| 掘金者自动载入已保存方案 | `[GoldDiggers] AutoLoadSaved`(true) | 掘金者按钮 **Shift+右键** 切换(浅蓝) | 读档、离线结算和转生后自动应用游戏内“已保存的掘金者”;等待金币产出可用后重试 |
| 自动回到冒险区域 | 状态与目标区域存入存档(无配置文件项) | 主菜单"冒险"按钮 **Shift+右键** 切换(浅蓝) | 在目标冒险区域中开启即锁定该区域;在该区域战败被送回安全区、HP 恢复满后自动回到该区域继续自动战斗。开启状态下在冒险界面下拉框切换区域会自动更新目标。ITOPOD 不适用(战败不送安全区);转生、离线结算后不会自动拉人回冒险 |

### 常驻增强(加载即生效)

| 功能 | 说明 |
|---|---|
| Yggdrasil 工具提示增强 | 果实 tooltip 显示上次收获数量、隐藏等级(力量/幸运/永久加成/永久数字等,即 wiki 所称 "invisible levels")、`[NGU YIELD FH]` 产出来源标注、到最高等级所需时间;Ygg 总加成按倍率显示 |
| Ygg 种子获得数量显示格式修复 | 收获/食用果实提示中的种子、金币、AP 等数量改用游戏统一的大数字格式显示 |
| 数字格式改进 | 修正负数与特殊值(Infinity/NaN)显示;绝对值小于 100 万的数字用千分位普通格式而非后缀缩写 |
| 一键合并/强化全部 | 背包界面 **Shift+M** = 合并全部、**Shift+B** = 强化全部;背包按钮 **右键** = 合并+强化全部 |
| 传家宝重开 | 结局(End)面板按钮 **右键** = 传家宝重开:重置进度但保留传家宝物品、游戏设置、已购礼包(新手包/Res3/时装/ITOPOD 名称包)与 Krissmiss 奖励;加载存档后自动关闭结局面板 |
| 纯净存档导出 | 游戏内保存文件时**按住 Shift**,导出不含模组数据的干净存档(文件名带 `(CLEAN)` 标记),可被 Gear Optimizer 等外部工具正常读取 |
| 模组数据随存档持久化 | 自动主线开关、上次果实收获记录等保存在存档内(序列化类型伪装为 `PlayerData`,香草客户端亦可读取该存档) |

### 配置开关汇总(`BepInEx\config\jshepler.ngu.mods.cfg`)

| 节 | 项 | 默认 | 说明 |
|---|---|---|---|
| `[Yggdrasil]` | `AutoHarvest` | `true` | 果实满级自动食用 |
| `[MoneyPit]` | `AutoToss` | `true` | 钱坑自动投金币 |
| `[DailySpin]` | `AutoSpin` | `true` | 每日转盘自动转 |
| `[BloodMagic]` | `AutoCast` | `true` | 铁柱自动施法 |
| `[AutoMergeTransform]` | `Enabled` | `true` | 吊坠/Looty 自动合并转化 |
| `[AutoAllocation]` | `Priority` | `Augment,BloodMagic,TimeMachine,AdvancedTraining,Wandoos,NGU,Wishes` | 全局资源分配顺序;缺少或重复项会自动补齐默认顺序 |
| `[AutoAllocation]` | `Augment` | `true` | 挂件能量和升级能量自动分配 |
| `[AutoAllocation]` | `BloodMagic` | `true` | 血魔法仪式魔力自动分配 |
| `[AutoAllocation]` | `Wandoos` | `true` | Wandoos 能量和魔力自动分配 |
| `[AutoAllocation]` | `NGU` | `true` | NGU 能量和魔力自动分配 |
| `[TimeMachine]` | `AutoAllocateEnergy` | `true` | 时光机器能量和魔力自动分配 |
| `[AdvancedTraining]` | `AutoAllocateEnergy` | `true` | 高级训练能量自动分配 |
| `[Wishes]` | `AutoAllocate` | `false` | 汇总运行许愿与空闲池中的能量、魔力和 Res3 后平均重分配;完成后自动开启下一个许愿 |
| `[GoldDiggers]` | `AutoLoadSaved` | `true` | 自动应用已保存的掘金者 |
| `[Performance]` | `FrameCheckInterval` | `60` | 帧轮询自动化之间的检查间隔(帧),用于多资源分配、血魔法、钱坑/转盘、冒险和自动主线 |
| `[Performance]` | `WishCheckIntervalSeconds` | `1` | `AutoWishes` 的真实时间检查间隔(秒) |
| `[Questing]` | `AutoButter` | `true` | 主线任务自动黄油 |
| `[Cards]` | `AutoCast.Enabled` | `false` | 全局自动出牌;每次帧轮询最多打出一张高优先级普通卡或 Chonker;跳过 `THE END` 与普通受保护卡 |
| `[Cards]` | `AutoSort.Enabled` | `true` | 新增卡牌时自动排序;卡牌界面按 `S` 可手动排序 |
| `[Cards]` | `AutoSort.By` | `RarityFirst` | `RarityFirst`、`TypeFirst`、`Efficiency` 或 `Variance` |
| `[Cards]` | `AutoSort.Direction` | `Ascending` | 现有牌堆排序方向;自动出牌始终使用高优先级方向 |
| `[Cards]` | `AutoYeet.Mode` | `Disabled` | `Disabled`、`Efficiency`、`Variance` 或 `Rarity` |
| `[Cards]` | `AutoProtectChonkers` | `true` | 新生成 Chonker 卡自动保护 |

### 自动化检查间隔

- `[Performance] FrameCheckInterval` 默认 `60`,表示帧数而不是固定秒数;多资源上限分配、Iron Pill、掘金者载入及其他帧轮询功能都使用此间隔。
- `[Performance] WishCheckIntervalSeconds` 默认 `1`,自动许愿使用真实时间间隔,同时处理能量、魔力和 `Res3`。
- 配置文件修改需要重启游戏;Shift+右键切换开关会立即生效并重置对应检查计时器。

### 卡牌自动化执行逻辑

- **自动排序不是定时任务**。`AutoSort.Enabled=true` 时，新生成普通卡或 Chonker 卡会先执行自动弃牌，再按 `AutoSort.By` 与 `AutoSort.Direction` 排序;已有牌堆不会周期性重排。卡牌界面按 `S` 可立即排序，按 `Y` 可手动执行弃牌规则。
- **自动出牌是定时检查**。`AutoCast.Enabled=true` 后全局运行，检查间隔使用 `[Performance] FrameCheckInterval`(默认 60 帧)，每次最多打出一张。
- 自动出牌沿用 `AutoSort.By` 的比较字段，但始终按高优先级方向选择目标，不受 `AutoSort.Direction` 影响。`RarityFirst` 按稀有度、卡牌类型、效果值比较;`TypeFirst` 按卡牌类型、稀有度、效果值比较;`Efficiency` 和 `Variance` 使用卡牌对应指标。
- 普通受保护卡与 `THE END` 不会自动打出。Chonker 会保持自动保护，直到它成为当前最高优先级目标且蛋黄酱足够;此时自动出牌流程临时解除保护并调用游戏原生出牌逻辑，打出失败则恢复保护。
- 目标卡缺少蛋黄酱时，只运行目标卡缺少的类型;生成器槽位不足时按剩余缺口轮换。自动流程会暂存并恢复目标卡完成、目标改变或自动出牌关闭前的生成器运行状态。
- 自动出牌不会自动解除普通卡的保护，也不会处理 `THE END`;这两个规则用于避免不可逆的误消费。

## 构建操作

### 环境要求

- .NET SDK(实测 10.0.302 可构建;目标框架为 `net48`,通过 `Microsoft.NETFramework.ReferenceAssemblies` 包自动还原参考程序集)
- 游戏本体:构建需要引用游戏的 `Assembly-CSharp.dll` 与 `UnityEngine.UI.dll`,路径由 `GameFolder` 属性指定

### 依赖还原

`source/NuGet.Config` 已配置 BepInEx 的 NuGet 源(`https://nuget.bepinex.dev/v3/index.json`),`dotnet build` 会自动还原以下包:

- `BepInEx.Core` 5.*、`BepInEx.Analyzers`、`BepInEx.PluginInfoProps`
- `UnityEngine.Modules` 2019.4.22
- `Microsoft.NETFramework.ReferenceAssemblies` 1.0.2

### 构建命令

`jshepler.ngu.mods.csproj` 中 `GameFolder` 的默认值为 `D:\Steam\steamapps\common\NGU IDLE`(原作者机器路径)。本机游戏位于 `C:\Program Files (x86)\Steam\steamapps\common\NGU IDLE`,构建时用 `-p:GameFolder` 覆盖(路径含空格,需加引号):

```bat
dotnet build source\jshepler.ngu.mods.csproj -c Release -p:GameFolder="C:\Program Files (x86)\Steam\steamapps\common\NGU IDLE"
```

### 构建产物与部署

- 产物:`source\bin\Release\net48\jshepler.ngu.mods.dll`
- 发布副本:`release\jshepler.ngu.mods-1.30.1.dll`
- 部署:项目含 `CopyDLL` 构建目标,构建成功后**自动复制**到 `$(GameFolder)\BepInEx\plugins\jshepler.ngu.mods.dll`,即游戏内生效位置(会覆盖同名旧 dll,见下方 dll 位置示意)

![dll 放置位置](dll.png)

### 构建验证

1. 构建成功(0 错误;仅有 `NumberFormating.cs` 的既有 Harmony003 警告,不影响功能)
2. 启动游戏,检查 `BepInEx\LogOutput.log`,应出现:
   - `Loading [jshepler.ngu.mods 1.30.1]`
   - `Plugin jshepler.ngu.mods is loaded!`
   - 无 `Patching exception` / 异常堆栈
3. 首次运行会生成 `BepInEx\config\jshepler.ngu.mods.cfg`(新增配置项需先启动一次游戏才会写入该文件)

## 配置说明

- 配置文件:`…\NGU IDLE\BepInEx\config\jshepler.ngu.mods.cfg`
- 配置仅在游戏启动时读取,修改后需重启游戏
- 新版本新增的配置项,需要先启动一次游戏生成到 cfg 文件,退出后即可编辑
- 游戏内热键切换的状态会写回该 cfg(以及存档内的开关),两种方式等效

## 数据与备份注意事项

- 存档位置:`%USERPROFILE%\AppData\LocalLow\NGU Industries\NGU Idle`(如 `NGUSteamBackup.txt`、`NGUSteamBackup2.txt` 及游戏按日期生成的 `NGUSave-Build-*.txt` 均为游戏存档/备份数据)
- 本模组的构建与部署只涉及游戏目录下的 `BepInEx\plugins\jshepler.ngu.mods.dll`,不会读写存档目录
- 建议在升级模组或游戏前自行备份上述存档文件

## 致谢与免责声明

- 全部代码源自 [jshepler/jshepler.ngu.mods](https://github.com/jshepler/jshepler.ngu.mods),作者 jshepler
- 本仓库仅作个人使用而裁剪、修改,与原仓库功能、发布节奏无关;原版详细文档以原仓库 README 为准
- 使用风险自负,请留意游戏更新后程序集变更可能导致的兼容性问题(补丁目标不存在时 Harmony 会自动跳过并记录日志,重新构建验证即可)
