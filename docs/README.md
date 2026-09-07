# 项目文档索引

| 文档 | 用途 |
| --- | --- |
| [快速开始](../README.md) | 下载、安装、绑定邮箱和开始接收 |
| [详细使用指南](USER_GUIDE.md) | 规则、模板、下载选项、回复停收、数据与迁移 |
| [更新记录](../CHANGELOG.md) | 按版本保留的功能变更 |
| [仓库管理](REPOSITORY_MANAGEMENT.md) | 提交范围、GitHub/Gitee 同步、分支及版本发布 |
| [邮箱参数](MAIL_PROVIDERS.md) | 服务商默认值、来源及认证限制 |
| [安装包](INSTALLER.md) | 安装、升级和卸载方案 |
| [测试方法](TESTING.md) | 自动化及人工验收方法 |
| [验证记录](VALIDATION.md) | 历史测试证据；各记录仅适用于其对应版本 |
| [反馈后台](FEEDBACK_DEPLOYMENT.md) | 部署、备份、迁移和回滚 |
| [贡献指南](../CONTRIBUTING.md) | 开发协作约定 |
| [安全反馈](../SECURITY.md) | 安全问题反馈方式 |

## 目录职责

- `src/MailIntake.Core/`：邮件协议、规则、归档和状态管理。
- `src/MailIntake.Desktop/`：Windows 界面、语言切换、托盘和本地配置。
- `tests/`：使用模拟数据的业务测试。
- `feedback-server/`：反馈后台源码及通用部署文件。
- `scripts/`：构建、打包、检查和整理脚本。
- `examples/`：合成示例；真实名单放在仓库外。
- `licenses/`、`support/`：分发所需的许可证及作者提供的赞助素材。
- `artifacts/`：本机构建和检查产物，整个目录不提交。
- `.deployment/`：本机部署记录、反馈报告和备份，整个目录不提交。

## 整理本地产物

运行 `./scripts/organize-artifacts.ps1` 预览；加 `-Apply` 才移动文件。
脚本将旧安装包移到 `artifacts/archive/版本/`，根目录截图移到 `artifacts/screenshots/批次/`。
保留当前版本安装包、便携包、校验文件和程序目录；不移动构建目录或运行中的程序目录，不处理部署记录与用户数据。
每次移动前保存清单，移动后逐文件核对 SHA-256，便于追溯原位置。

目录整理只修改本地文件；源码同步和版本发布按仓库管理说明单独执行。
