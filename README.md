# Services Prechecker

Undefined SS Community 的 Windows 查端前置检查工具。它会在本机检查取证准备所需的系统服务，并可在一次管理员授权后恢复正确的启动方式、启动相关服务。程序还会在本机生成用于设备识别的 HWID。

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

1. 从 GitHub Releases 下载带版本号的 `ServicesPrechecker-v*.exe`。版本化文件名可避免 Windows 资源管理器沿用旧版图标缓存。
2. 直接运行程序并查看七项服务的状态。读取状态不需要管理员权限。
3. 如果存在未运行或已禁用的项目，点击“一键启用全部系统服务”。
4. 在 Windows 用户账户控制提示中确认授权。程序会自动完成设置并再次检测。
5. 完成设置后必须重启电脑。不要在当前启动周期继续查端；本次结果仍会按照“异常”处理。
6. 如需提供设备标识，点击底栏中的“点击复制 · HWID …”；剪贴板中只会写入以 `USS1-` 开头的 HWID。

程序不会连接远程设备，不会采集文件，也不会上传任何数据；服务检查、服务设置与 HWID 生成均在当前电脑完成。

## HWID

底栏会以 `点击复制 · HWID  USS1-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XX` 的格式显示设备标识。整段文字均可点击，不设置独立复制按钮；复制成功后会在原位置短暂显示“已复制”。也可使用键盘聚焦该文字并按 Enter 或空格复制。

`USS1` 表示第一版 HWID 算法。程序按 SMBIOS System UUID、主板序列号、BIOS 序列号的固定优先级选取第一个有效标识，过滤全零、全 F 及常见 OEM 占位值；硬件标识全部不可用时才回退到 Windows 注册表 `MachineGuid`。规范化后的字段与固定命名空间 `UndefinedSS.ServicesPrechecker/HWID/v1` 一起计算 SHA-256，截取 128 位摘要并使用 Crockford Base32 编码。原始硬件标识只在生成期间存在于内存中，不会显示、保存或上传。

在可读取有效硬件标识的正常物理电脑上，重装 Windows、更换系统盘或内存通常不会改变 HWID，更换主板则可能改变。若程序只能回退到 `MachineGuid`，重装 Windows 可能使 HWID 改变。OEM 重复标识、克隆虚拟机或人为修改 SMBIOS 也可能导致 HWID 重复或变化，因此它仅用于查端时辅助识别设备，不作为不可伪造的身份凭证，也不接入账号授权、白名单或封禁系统。

点击复制是用户主动将 HWID 写入 Windows 剪贴板的操作；若系统启用了剪贴板历史或跨设备同步，Windows 可能保留或同步该内容。Services Prechecker 自身不会上传 HWID。

## 为什么必须重启

Minecraft 查端通常判断的是本次电脑启动到查端人员远程连接期间的活动。如果用户在即将查端时才运行 Services Prechecker，刚启用的服务无法补回本次启动周期此前缺失的系统记录，因此本次查端仍按异常处理。

程序会记录“等待重启”状态。在同一次 Windows 启动中再次打开程序时，会继续明确提示当前周期无效；完成系统重启后，该提示会自动解除。

## 构建

项目使用 Windows 自带的 .NET Framework 4.x C# 编译器，可生成不依赖额外安装包的 64 位单文件 EXE。

```powershell
.\build.ps1
```

构建产物使用程序集版本生成唯一文件名，例如 `dist\ServicesPrechecker-v1.3.0.exe`。

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
