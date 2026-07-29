# Services Prechecker

Undefined SS Community 的 Windows 查端前置检查工具。它会在本机检查取证准备所需的系统服务，并可在一次管理员授权后恢复正确的启动方式、启动相关服务。

> **重要：启用任何所需服务后都必须重启 Windows。** 当前启动周期内的查端仍会按照“异常”处理；只有重启系统后的后续查端才有效。

![Undefined SS Community banner](assets/banner.png)

应用采用专为小尺寸 Windows 图标重新设计的双 S“取证门”标记，并使用连续的低对比度取证地图界面，避免 Banner 与产品文案争夺视觉焦点。

## 检查项目

| 界面名称 | Windows 服务名 | 启用后的启动方式 |
| --- | --- | --- |
| DNS Client | `Dnscache` | 自动 |
| Diagnostic Policy Service | `DPS` | 自动 |
| Connected User Experiences | `DiagTrack` | 自动 |
| Program Compatibility Assistant | `PcaSvc` | 手动并立即启动 |
| SysMain | `SysMain` | 自动 |
| Windows Event Log | `EventLog` | 自动 |
| Background Activity Moderator | `bam` | 系统启动；部分电脑需要重启 |

## 使用方法

1. 从 GitHub Releases 下载 `ServicesPrechecker.exe`。
2. 直接运行程序并查看七项服务的状态。读取状态不需要管理员权限。
3. 如果存在未运行或已禁用的项目，点击“一键启用全部系统服务”。
4. 在 Windows 用户账户控制提示中确认授权。程序会自动完成设置并再次检测。
5. 完成设置后必须重启电脑。不要在当前启动周期继续查端；本次结果仍会按照“异常”处理。

程序不会连接远程设备，不会采集文件，也不会上传任何数据；所有检查与服务设置均在当前电脑完成。

## 为什么必须重启

Minecraft 查端通常判断的是本次电脑启动到查端人员远程连接期间的活动。如果用户在即将查端时才运行 Services Prechecker，刚启用的服务无法补回本次启动周期此前缺失的系统记录，因此本次查端仍按异常处理。

程序会记录“等待重启”状态。在同一次 Windows 启动中再次打开程序时，会继续明确提示当前周期无效；完成系统重启后，该提示会自动解除。

## 构建

项目使用 Windows 自带的 .NET Framework 4.x C# 编译器，可生成不依赖额外安装包的 64 位单文件 EXE。

```powershell
.\build.ps1
```

构建产物位于 `dist\ServicesPrechecker.exe`。

## 代码签名

使用受信任的 Authenticode 代码签名证书：

```powershell
.\sign.ps1 -PfxPath "C:\secure\undefined-ss.pfx" -PfxPassword "<password>"
```

也可使用当前用户证书存储中的代码签名证书：

```powershell
.\sign.ps1 -CertificateThumbprint "<thumbprint>"
```

请勿把 PFX 文件或密码提交到仓库。GitHub Actions 可通过
`SIGNING_CERTIFICATE_BASE64` 和 `SIGNING_CERTIFICATE_PASSWORD` 两个仓库密钥对构建产物签名。

当前发布包附带社区自签名证书用于校验 Authenticode 签名与文件完整性；它不具备公共 CA 信任链或 SmartScreen 信誉。面向公众分发时，应在 GitHub Secrets 中配置受信任代码签名机构颁发的证书。

## 兼容性

- Windows 10 / Windows 11
- 64 位系统
- `.NET Framework 4.8`（现代 Windows 10/11 通常已内置或通过 Windows Update 提供）

## License

[MIT](LICENSE)
