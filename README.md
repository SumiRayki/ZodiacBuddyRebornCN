# Zodiac Buddy Reborn（国服适配）

非官方国服适配。基于 [FFXIV-CombatReborn/ZodiacBuddyReborn](https://github.com/FFXIV-CombatReborn/ZodiacBuddyReborn) 标签 **7.5.6.1**（commit `4913f31ab829c21f6ae5289568f14cd182233bbc`）。

上游没有参与维护，也没有背书。这不是官方国服版本。

许可证是 GNU AGPL-3.0，见 `LICENSE` 和 `NOTICE`。发布的安装包对应本仓库同一版本的源码。

## 卫月自定义仓库

在卫月的自定义插件仓库中添加：

```text
https://raw.githubusercontent.com/SumiRayki/ZodiacBuddyRebornCN/main/pluginmaster.json
```

这是订阅文件本身，不是 GitHub 仓库页面。当前版本是测试版，需要在卫月里允许测试版插件后才能看到 **Zodiac Buddy Reborn（国服适配）**。订阅里只有这一个插件。

安装前先禁用原版 `ZodiacBuddy Reborn`。两个插件不能同时启用：命令 `/pzodiac`、`/ztarget` 会冲突，文书自动化也不能并存。本插件始终提供 `/pzodiac-cn` 和 `/ztarget-cn`。

本插件使用独立的 `ZodiacBuddyRebornCN` 配置，不读取、不覆盖原版配置。不需要迁移。要回到原版，在卫月里禁用或删除这个自定义仓库，再重新启用原版即可。已发布的旧测试包留在对应 Release 里。

## 支持范围

- 国服客户端 `2026.10.06.0000.0000`
- Dalamud API 15
- 发布包由 GitHub Actions 按 `ottercorp/dalamud-distrib` 的 Dalamud `15.0.3.8`（git `ecd08fafd6092c51a011b23fe322442baaecf5ce`，同一游戏版本）编译
- 开发时本机 XIVLauncherCN 的 Dalamud 已是 `15.0.3.9`（commit `e7efef21c1db9fa975b5fd97a4e3b77246bbec0a`）。两份 `FFXIVClientStructs.dll` 不是同一个文件

这个版本标为测试版。理符发行人、接取菜单和任务地点的国服名称来自客户端数据核对，还没有把整本流程在实机里跑完。

## 这个适配改了什么

- 理符发行人按 `LevelLevemete` 的 NPC 数据 ID 识别，不再比较英文名。
- 接取菜单使用客户端的理符分类名。无法识别的菜单会停下并记日志，不会点第一项，也不会把文书改成“暂不可接”后换下一个点。
- 交付菜单优先选择带“报告”的项；第一项如果是接取分类则不点。
- 任务地点和相关 NPC 使用国服名牌或战斗 NPC 名称 ID。例如英文 `prime location` 在国服名牌上是「设置地点」。
- 野怪、FATE、副本的原有执行流程保持原样。

## 本地构建

需要 .NET 10 SDK，以及国服 Dalamud API 15 运行时。把 `DALAMUD_HOME` 指到该运行时目录（里面要有 `Dalamud.dll`）。不要把游戏目录或本机路径写进仓库。

```powershell
$env:DALAMUD_HOME = "<Dalamud Hooks\dev>"
dotnet test ZodiacBuddyRebornCN.Tests/ZodiacBuddyRebornCN.Tests.csproj -c Release
dotnet build ZodiacBuddy/ZodiacBuddy.csproj -c Release
```

Release 构建会在 `ZodiacBuddy/bin/Release/ZodiacBuddyRebornCN/latest.zip` 生成卫月安装包。安装包里是插件 DLL、清单和 `NOTICE` 中列出的依赖，不是源码压缩包。带国服 sqpack 的数据测试只有同时设置 `DALAMUD_HOME` 和 `FFXIV_SQPACK` 时才会编译。
