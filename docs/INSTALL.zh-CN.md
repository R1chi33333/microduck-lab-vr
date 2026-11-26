# 在另一台电脑上安装游戏

[English](INSTALL.md)

玩游戏不需要装 Unity：游戏是做好的 Windows 版本，直接运行即可。每台要玩的电脑都要装一份（双人联机就是两台，见 [MULTIPLAYER.md](MULTIPLAYER.md)）。

## 需要准备

- **一台能跑 PC VR 的 Windows 10 / 11 电脑**（近几年的 NVIDIA 或 AMD 显卡，也就是能用 Quest Link 的电脑）。
- **一台 Meta Quest 头显**（按 Quest Pro 做的，Quest 2 / Quest 3 通过 Link 应该也能玩），加一根 **USB-C Link 线**，或者用 5 GHz Wi-Fi 的 **Air Link**。
- **Meta Horizon Link 电脑端应用**（以前叫 Oculus 应用）：<https://www.meta.com/help/quest/articles/headsets-and-accessories/oculus-rift-s/install-app-for-link/>

## 第一步：设置 Link（每台电脑一次）

1. 在电脑上安装并打开 **Meta Horizon Link** 应用，登录账号，在 **Devices（设备）** 里配对头显。
2. 在应用里：**Settings > General > OpenXR Runtime > 把 Meta Horizon Link 设为当前运行时（Set as active）**。游戏通过 OpenXR 连接头显，这一步不做的话游戏只会在显示器上打开。
3. 同样在 **Settings > General** 里打开 **Unknown Sources（未知来源）**，因为游戏不是从 Meta 商店装的。
4. 戴上头显，插上 Link 线（或打开 Air Link），在头显的快捷设置里进入 **Link**。头显里能看到 Link 的主界面就对了。

## 第二步：获取游戏

**方法 A：下载发布版（最简单）**

1. 打开仓库的 **Releases** 页面：<https://github.com/R1chi33333/microduck-lab-vr/releases>
2. 在最新版本里下载 **`MicroduckLabVR-Windows.zip`**。
3. 右键压缩包 > **全部解压缩**，解压到自己的文件夹，比如 `C:\Games\MicroduckLabVR`。要从解压出来的文件夹里运行，不要直接在压缩包里双击。

**方法 B：自己从源码打包**（只有想用最新代码时才需要）

1. 安装 **Unity Hub** 和 **Unity 6000.6.7f1**（自带 Windows 打包支持 *Windows Build Support (Mono)*）。
2. `git clone https://github.com/R1chi33333/microduck-lab-vr.git`，在 Unity Hub 里 **Add > Add project from disk** 打开这个文件夹。第一次打开要导入所有资源，比较慢。
3. 在 Unity 里点菜单 **Microduck > Build Windows**，游戏会生成在 `Builds/Windows/`。把整个文件夹复制到另一台电脑。

## 第三步：开始玩

1. 确认头显已经进入 **Link**（第一步第 4 条）。
2. 双击游戏文件夹里的 **`MicroduckLabVR.exe`**。
   - 如果 Windows 提示 **"Windows 已保护你的电脑"**（因为游戏没有数字签名），点 **更多信息 > 仍要运行**。
   - 第一次用联机时，Windows 防火墙会询问网络权限：勾选 **专用网络** 并允许。
3. 游戏会在头显里启动。第一次进入时先在镜子前自定义你的鸭子，然后是一段教会所有按键的教程。玩具包（**Y**）里有 **HELP**（全部按键说明）和 **TUTORIAL**（重新走一遍教程）。

按键说明见 [README](../README.md#playing)。

## 更新

下载新的发布版，解压覆盖旧文件夹（或者解压到新文件夹）。鸭子的外观和教程进度会保留，它们存在 Windows 里，不在游戏文件夹里。**联机时两台电脑必须是同一个版本。**

## 常见问题

| 问题 | 解决办法 |
|---|---|
| 游戏只在显示器上打开，头显里没有画面 | 把 Meta Horizon Link 设为当前 OpenXR 运行时（第一步第 2 条），确认头显在 Link 里，再重新打开游戏。 |
| 提示找不到 "VCRUNTIME140.dll" 之类的文件 | 从微软官网安装最新的 Microsoft Visual C++ Redistributable（x64），再试一次。 |
| 头显里画面卡顿 | 用 Link 线代替 Air Link；在 Link 应用的设备设置里把刷新率降到 72 Hz，或把渲染分辨率调低一档。 |
| 联机时 JOIN 一直在搜索 | 见 [MULTIPLAYER.md](MULTIPLAYER.md#networks)：两台在同一网络、网络设成"专用"、两台都允许了防火墙，或者手动填主机地址。 |
| 想重新自定义鸭子 | 玩具包（**Y**）> **MY LOOK**，或者走到镜子前按 **X**。 |
