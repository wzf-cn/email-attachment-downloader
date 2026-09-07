# 邮件接收管理

Windows 桌面软件：按邮件主题关键词自动保存正文和附件，支持多个邮箱、独立规则目录和检查频率。

## 下载与安装

前往 [GitHub Releases](https://github.com/wzf-cn/email-attachment-downloader/releases) 或 [Gitee Releases](https://gitee.com/wzFeel/email-attachment-downloader/releases) 获取 Windows x64 版本：

- **安装版（推荐）**：下载 `MailIntake-Setup-版本号.exe`，按向导安装，完成后选择“立即运行”。
- **便携版**：下载 `MailIntake-win-x64.zip`，完整解压后运行 `MailIntake.exe`。

无需安装 Python 或 .NET。请下载发布附件，源码压缩包不能直接运行。若发布页暂无附件，表示安装包尚未公开发布。

## 五步开始收邮件

以 QQ 邮箱收集“工程实践”报告为例：

1. **准备邮箱**：在 QQ 邮箱网页版设置中开启 IMAP/SMTP 服务，取得授权码。
2. **绑定邮箱**：点击“绑定邮箱”，输入完整邮箱地址，软件会填入默认服务器参数；输入授权码并保存。选中邮箱，点击“测试连接”。
3. **新增规则**：选中邮箱 →“新增规则”，名称填“工程实践报告”，模式选“关键词”，关键词填“工程实践”。多个关键词可用中文或英文逗号分隔；需要全部命中时勾选“要求全部关键词匹配”。
4. **选择保存方式**：为这条规则指定下载目录，设置检查频率（例如 5 分钟），勾选需要的正文、附件和原始邮件。仅收集文件时，取消自动回复；需要回复时先修改回复内容。保存规则。
5. **开始接收**：核对邮箱开始日期（默认最近 30 天），点击主界面的“保存并开始”。新邮件会按规则处理，可在“归档记录”查看结果并打开目录。

“立即检查”会检查全部规则。关闭窗口后软件继续在托盘运行；结束接收请点击“暂停”或“退出软件”。需要开机运行时，在“运行设置”勾选登录启动并保存。

## 更新与数据

- 默认启动时检查新版本，也可在“运行设置”手动检查。发现新版后打开发布页下载，安装时会询问是否退出旧程序。
- 安装版使用新版安装包升级；便携版的更新方法见[详细指南](docs/USER_GUIDE.md)。
- 配置和记录保存在 `%LOCALAPPDATA%\MailIntake`，下载文件保存在规则指定的目录。更新和卸载保留这些数据。

## 更多设置

- [详细使用指南](docs/USER_GUIDE.md)：学号/姓名校验、规则模板、自动回复与停收、附件大小限制、重新导出。
- [邮箱默认配置](docs/MAIL_PROVIDERS.md)：邮箱类型、端口和登录限制。
- [更新记录](CHANGELOG.md) · [全部文档](docs/README.md) · [开发与贡献](CONTRIBUTING.md)

右上角可切换中文 / English，也可提交意见反馈、点 Star 或自愿赞助。

当前版本 **1.4.5**。安装包尚未数字签名，Windows 可能显示安全提示。QQ 云附件只记录链接，需要手动下载；Outlook 的 OAuth 登录暂未支持。自动化测试和界面检查已通过，真实邮箱收发仍需使用者验证。

## 开源许可

采用 [MIT 许可证](LICENSE)，允许商用、修改和分发。基于 MailKit / MimeKit 开发，是独立应用；第三方组件与品牌素材保留各自权利，见 [第三方说明](THIRD_PARTY_NOTICES.md)。

[GitHub](https://github.com/wzf-cn/email-attachment-downloader) · [Gitee](https://gitee.com/wzFeel/email-attachment-downloader)
