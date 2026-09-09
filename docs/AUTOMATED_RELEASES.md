# 自动发布与 Gitee 镜像

GitHub 是源码及安装包的发布源。推送 `vX.Y.Z` 标签后，`Publish Windows release` 自动编译、测试并发布安装包、便携包和 SHA256 校验文件。

`Mirror release to Gitee` 在正式发布成功后自动运行：同步 main 和对应标签（不强制覆盖），下载 GitHub 已发布的三个文件，验证 SHA256，再创建 Gitee 发行版并上传。上传后逐个下载校验；同名同内容跳过上传，同名不同内容报错，保留原文件。同步失败不会撤销 GitHub 发布。

## 首次授权

1. 在 Gitee 的“设置 → 私人令牌”创建用于此项目发布的令牌，授予 `projects` 权限。令牌必须能访问并发布 `wzFeel/email-attachment-downloader`。
2. 打开 GitHub 仓库的 `Settings → Secrets and variables → Actions → New repository secret`。
3. 名称填写 `GITEE_TOKEN`，值填写刚创建的令牌。不要把令牌写入源码、普通 Variables、日志或聊天。
4. 打开 GitHub `Actions → Mirror release to Gitee → Run workflow`，分支选择 `main`，tag 填 `v1.7.0`，补同步当前版本。

后续每次正式发布自动同步。缺少授权、令牌过期、Gitee 空间/附件限制或网络问题时任务会明确失败；处理后可重跑。首次需以 Actions 成功和 Gitee 三个附件校验成功为准，配置文件存在不表示已完成上线验证。

本流程不重新编译 Gitee 安装包，不删除旧发行版，也不强制覆盖 Gitee 的独立提交。Gitee 分支存在冲突时需先处理差异。
