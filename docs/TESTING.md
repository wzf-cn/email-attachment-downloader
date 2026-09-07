# 验证方法

## 自动化业务测试

```powershell
dotnet run --project tests/MailIntake.Tests/MailIntake.Tests.csproj -c Release
```

测试使用临时目录、真实 SQLite 和 MimeKit MIME 序列化、替代发信接口，不连接外部邮箱。

覆盖：主题结构；名单姓名与学号；CSV 前导零；目录必填；Unicode 附件归档与元数据；多目录归档单次回复；连续两次错误及第三次停收通知、可调阈值、成功清零和重复检查去重；跨账号计数；重启持久化；管理员按邮箱重置；旧邮件不补处理；发送不确定时不重试；自动邮件防循环；全局回复限额；大邮件不读取正文；同主题跨邮箱差异提醒。

## 原生界面与迁移检查

```powershell
$env:MAILINTAKE_TEST_HOME = Join-Path $env:TEMP ('MailIntakeSmoke-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $env:MAILINTAKE_TEST_HOME | Out-Null
./artifacts/win-x64/MailIntake.exe --smoke-test
Get-Content (Join-Path $env:MAILINTAKE_TEST_HOME 'smoke-result.txt')
```

仅在明确设置隔离目录后可运行。检查 DPAPI 加密往返、合成 Python 配置与停收/去重迁移、源配置不变、设置保存读取、主窗口和编辑窗口构建。不会真实收发，不写登录启动项，不访问真实邮箱配置。成功标记：`WINDOWS_FORMS_SMOKE_OK`。

## 后续真实邮箱验收

需要用户在软件界面输入测试邮箱授权码后进行：

1. 分别验证 IMAP 与 SMTP 登录；POP3 单独验证。
2. 中文主题及有附件的正确提交保存、回复。
3. 默认连续错误主题前两次统一回复，第三次通知停收；其后正确主题也不处理。
4. 管理员重置后重新发送恢复；旧停收邮件不补处理。
5. 断网、SMTP 失败、休眠恢复、实际 Windows 注销/登录启动。

这些项目不能由离线测试替代，目前不声称已通过真实 QQ、POP3、SMTP 或开机启动验收。
