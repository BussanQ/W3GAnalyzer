# W3GAnalyzer — 魔兽争霸 III 录像分析器

一个 Windows 工具，解析魔兽争霸 III 录像文件（`.w3g`），提取其中的全部信息：游戏版本、地图、玩家、聊天记录、APM、离开事件与时间线。针对国内对战平台录像做了 GBK 编码兼容。

## 功能

| 类别 | 内容 |
|------|------|
| 版本 | War3 RoC/TFT、补丁号（如 1.24）、build 号 |
| 地图 | 地图路径、地图名 |
| 玩家 | 名字、队伍、颜色、种族、是否观察者/电脑 |
| 进度 | 游戏时长、每玩家 APM、动作数、离开时间/原因、推测胜方 |
| 聊天 | 时间、玩家、频道（全体/盟友/观察者/私聊）、内容 |
| 时间线 | 开局、全部聊天、离开事件按时间排序 |

- 玩家名、聊天、地图路径自动按 **严格 UTF-8 → GBK(936)** 回退解码，正确显示中文
- 容错优先：遇到未知数据块返回已解析的部分结果并记录警告，不整体失败
- 经典版录像支持补丁 1.07–1.27（实测 1.20 / 1.24 / 1.26 均正常）；重制版（≥1.32）会给出友好提示

## 使用

### 图形界面
直接运行 `W3GAnalyzer.exe`，把 `.w3g` 文件拖入窗口，或用「文件 → 打开」。
四个标签页：概览、玩家、聊天、时间线。可导出 JSON / 文本。

### 命令行
```
W3GAnalyzer.exe --json <录像.w3g> [输出.json]
W3GAnalyzer.exe --text <录像.w3g> [输出.txt]
```
退出码 0 表示成功，1 表示解析失败，2 表示参数错误。

## 构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)。

```powershell
dotnet build -c Release
# 发布自包含单文件 exe（免安装运行时）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

## 项目结构

```
Core/
  ReplayParser.cs        编排：头 → 解压 → 静态数据 → 回放流
  ActionParser.cs        动作长度表 + APM 统计
  TextDecoder.cs         UTF-8 / GBK 自动解码
  BinaryReaderEx.cs      带边界检查的小端读取
  EncodedStringDecoder.cs 设置字符串反混淆
  Models.cs              数据模型
  Exporter.cs            JSON / 文本导出
Forms/
  MainForm.cs            WinForms 界面
Program.cs               入口（GUI / CLI 双模式）
```

## 实现说明

w3g 格式无官方规范，本项目基于社区逆向的格式文档实现。关键点：

- 数据块为 zlib 压缩（`78 01`），用 `ZLibStream` 解压后拼接
- 动作切片按 TimeSlot 的 `u16` 长度限长解析，单个动作长度估错不会跨块扩散
- vs-AI 录像中电脑玩家 APM 为 0 属正常——魔兽录像本就不记录 AI 操作
