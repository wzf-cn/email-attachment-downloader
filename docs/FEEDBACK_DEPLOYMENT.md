# 反馈后台部署与迁移

## 组成与存放

- `feedback-server/server.py`：Python 3.12 标准库实现，无第三方包、无邮箱依赖。
- 程序：`/opt/mail-intake-feedback/server.py`。
- 数据：`/var/lib/mail-intake-feedback/feedback.sqlite3`，SQLite WAL；`ip-salt` 用于提交频率统计的 IP 哈希，不存原始 IP。
- 备份：数据目录的 `backups/`，每天服务器时间 03:30 使用 SQLite 在线备份接口创建一致快照。备份目前保留全部历史，管理员需定期归档并关注磁盘空间。
- systemd：`mail-intake-feedback.service`、`mail-intake-feedback-backup.service/.timer`。
- 服务监听：`127.0.0.1:8891` 仅提交和健康检查；`127.0.0.1:8892` 仅管理页面。不要将这两个端口开放到公网。
- Nginx：`/etc/nginx/snippets/mail-intake-feedback.conf` 只公开提交接口与健康检查；`/etc/nginx/conf.d/mail-intake-feedback-limit.conf` 定义请求限速。
- 当前服务器、SSH 路径、验收回执及操作入口记录在本地 `.deployment/`，该目录不提交到 Gitee。

## 桌面使用

默认接口为 `https://wuzhuofei.com/mail-feedback/api/feedback`。用户可匿名提交，联系方式选填。仅上传用户填写的类型、标题、说明、联系方式、软件版本与随机反馈编号。无自动邮件、附件、账户、日志收集。

草稿位于 `%LOCALAPPDATA%\MailIntake\Feedback\draft.json`，关闭窗口自动保存，确认提交成功后删除草稿。同一编号与相同内容重试不重复保存；更改内容会使用新编号。

迁移时优先保持域名和接口路径不变，更新 DNS 与服务端即可。必须改变地址时，更新客户端默认地址并发新版；过渡期可在 `%LOCALAPPDATA%\MailIntake\feedback-endpoint.txt` 写入新的完整 HTTPS 接口地址。客户端不接受明文 HTTP，也不自动跟随重定向。

## 管理访问

通过 SSH 建立 `127.0.0.1:18892 -> 服务器 127.0.0.1:8892` 的隧道，再打开 `http://127.0.0.1:18892/`。管理员权限由 SSH 控制，不在软件内置管理密码。管理页面支持查看最近 1000 条、导出这些记录的 CSV、标记待处理/处理中/已处理。完整历史保存在数据库及备份中。

管理页面仅接受固定本机 Host；状态修改校验同源 Origin，内容按 HTML 转义，CSV 防公式注入。不要把管理端口接入公开 Nginx 路由。共享电脑上请在用完后关闭隧道。

## 备份

运行本地 `.deployment/backup-feedback.ps1`，它会让服务创建一致的数据库快照，将该快照与 `ip-salt` 打包，再下载到 `.deployment/backups/`。不会读取或拷贝 SSH 私钥。备份包含用户反馈和联系方式，应保密存放。

手动检查：`sudo -u mail-feedback python3 /opt/mail-intake-feedback/server.py --backup`。
服务检查：`systemctl status mail-intake-feedback --no-pager`。
定时器检查：`systemctl list-timers mail-intake-feedback-backup.timer`。
接口检查：`curl --fail https://wuzhuofei.com/mail-feedback/health`。

## 迁移到新服务器

1. 在旧服务器创建并下载最新备份，保留旧服务与本地副本。在最终切换期间暂停旧端提交，完成最后一次备份，避免遗漏切换窗口中新收到的反馈。
2. 新服务器准备 Python 3.12、Nginx、HTTPS 证书。检查 `8891/8892` 未被占用。上传 `feedback-server/`。部署脚本针对当前 `/etc/nginx/sites-available/home`，迁移前按新虚拟主机路径调整；不要直接覆盖现有网站配置。
3. 创建专用系统用户 `mail-feedback`，安装代码、systemd 文件和 Nginx 片段。把 Nginx include 放入对应 HTTPS server 块，限速 zone 放入 http 上下文。先运行单元测试和 `nginx -t`。
4. 新服务停止时，将备份中的 `feedback.sqlite3` 与 `ip-salt` 恢复到 `/var/lib/mail-intake-feedback/`，属主 `mail-feedback:mail-feedback`，目录 700、文件 600。不要把旧 WAL 文件混入已恢复的快照。
5. 检查 SQLite `PRAGMA integrity_check` 与记录数；启动服务，验证健康检查、模拟提交、重复提交、SSH 管理页面、CSV 导出与状态修改。
6. 更新域名 DNS/入口。HTTPS 正常后再让客户端使用新服务器。运行备份任务，确认备份存在。观察完成后停用旧服务与定时器，保留旧快照以便回退。

## 回滚

本次部署在修改 Nginx 前保留了 `home.before-feedback-日期时间`。若需要撤销，删除 home 中本项目的 include，先 `nginx -t` 后 reload；停用本项目服务和备份定时器。不要停止整机 Nginx，也不要删除数据目录。源码回滚可从 Gitee 取已验证版本替换 `/opt/mail-intake-feedback/server.py` 后重启本项目服务。

## 容量与限制

每个来源 IP 每小时最多 5 条新反馈，全站每天最多 1000 条；同一出口的用户共用限额。入口另有请求速率限制。此版本不支持附件、用户查看回复或账户系统；管理员可通过用户自愿填写的联系方式另行联系。服务使用标准库 HTTP 处理器，必须置于 Nginx 后，仅监听本机。
