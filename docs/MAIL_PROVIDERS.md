# 邮箱默认参数

核对日期：2026-09-07。内置离线配置，不向外发送正在填写的邮箱地址。按完整域名匹配，不将自定义单位域名猜测成某个服务商。

| 服务商/域名 | IMAP | POP3 | SMTP |
| --- | --- | --- | --- |
| QQ / qq.com、foxmail.com | imap.qq.com | pop.qq.com | smtp.qq.com |
| 163.com | imap.163.com | pop.163.com | smtp.163.com |
| 126.com | imap.126.com | pop.126.com | smtp.126.com |
| yeah.net | imap.yeah.net | pop.yeah.net | smtp.yeah.net |
| sina.com | imap.sina.com | pop.sina.com | smtp.sina.com |
| sina.cn | imap.sina.cn | pop.sina.cn | smtp.sina.cn |
| vip.sina.com | imap.vip.sina.com | pop.vip.sina.com | smtp.vip.sina.com |
| vip.sina.cn | imap.vip.sina.cn | pop.vip.sina.cn | smtp.vip.sina.cn |
| aliyun.com | imap.aliyun.com | pop3.aliyun.com | smtp.aliyun.com |
| 139.com | imap.139.com | pop.139.com | smtp.139.com |
| Gmail / gmail.com、googlemail.com | imap.gmail.com | pop.gmail.com | smtp.gmail.com |
| Yahoo / yahoo.com、ymail.com、rocketmail.com | imap.mail.yahoo.com | pop.mail.yahoo.com | smtp.mail.yahoo.com |
| Outlook / outlook.com、hotmail.com、live.com、msn.com | outlook.office365.com | outlook.office365.com | smtp-mail.outlook.com |

IMAP 均为 993 / SSL/TLS，POP3 均为 995 / SSL/TLS。SMTP 除 Outlook 为 587 / STARTTLS 外，均为 465 / SSL/TLS。

## 来源和差异

- [Thunderbird ISPDB](https://github.com/thunderbird/autoconfig/tree/master/ispdb)：核对 qq.com.xml、163.com.xml、126.com.xml、yeah.net.xml、googlemail.com.xml、yahoo.com.xml、hotmail.com.xml 的配置。前四项记录主要提供 IMAP 与 SMTP，不应宣称全部 POP 参数也来自 ISPDB。
- [Nodemailer SMTP 预设](https://nodemailer.com/smtp/well-known-services)：交叉核对 QQ、163、126、Aliyun、Gmail、Yahoo、Hotmail 的 SMTP；该库不提供 IMAP/POP 默认值。
- [新浪官方参数](https://help.sina.com.cn/comquestiondetail/view/160/)：四种新浪后缀的完整收发地址及加密端口。
- [139 官方参数](https://help.mail.10086.cn/statichtml/1/Content/858.html)：采用明确标注 SSL 的 IMAP 993、POP3 995、SMTP 465，不采用旧帮助中的明文端口。
- [阿里云个人邮箱](https://help.aliyun.com/zh/document_detail/465307.html)：个人邮箱 POP 主机为 **pop3.aliyun.com**，不能套用企业邮箱 pop.qiye.aliyun.com。
- [网易客户端帮助](https://help.mail.yeah.net/faq.do?categoryID=90&m=list)：客户端授权码与收发协议设置；保留标准 POP 配置，需用真实账号完成连接验收。
- [Gmail 协议说明](https://developers.google.cn/workspace/gmail/imap/imap-smtp)：IMAP/POP 的 SSL 与 SMTP TLS 设置。
- [Microsoft 官方参数](https://support.microsoft.com/en-US/Outlook/pop-imap-and-smtp-settings-for-outlook-com)：Outlook 要求 OAuth2。当前客户端只实现授权码/应用密码登录，**识别参数不代表支持 Outlook 登录**；界面会明确提示这一限制。

Gmail、Yahoo 等需要账号允许的应用专用密码，企业账号还可能受管理员策略限制。配置核对并非真实收发验收。没有充分核对的服务商暂不自动填入，可选择自定义。

## 自动填入与手动修改

新建邮箱输入完整地址即填入对应服务器；切换 IMAP/POP3 时更新仍由默认值控制的收信参数。已有邮箱与手动改过的服务器组保持原值。“应用收发默认参数”可明确替换为所选服务商的配置。新邮箱改成未知域名时清除未修改的旧默认主机，避免误用上一家服务商。

更换语言不改变协议枚举、规则、账号、邮件内容或回复正文。界面语言偏好单独存于本机数据目录的 language.txt。
