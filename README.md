# Services Prechecker

Undefined SS Community 的 Windows 查端前置检查工具。它会在本机检查取证准备所需的系统服务，并可在一次管理员授权后恢复正确的启动方式、启动相关服务。

![Undefined SS Community banner](assets/banner.png)

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
3. 如果存在未运行或已禁用的项目，点击“一键启用全部服务”。
4. 在 Windows 用户账户控制提示中确认授权。程序会自动完成设置并再次检测。

程序不会连接远程设备，不会采集文件，也不会上传任何数据；所有检查与服务设置均在当前电脑完成。

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

## 兼容性

- Windows 10 / Windows 11
- 64 位系统
- `.NET Framework 4.8`（现代 Windows 10/11 通常已内置或通过 Windows Update 提供）

## License

[MIT](LICENSE)
