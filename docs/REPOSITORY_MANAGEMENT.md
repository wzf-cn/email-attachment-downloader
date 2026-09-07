# GitHub / Gitee 管理方式

## 上传哪些文件

| 提交源码仓库 | 单独放 Releases | 只保存在本地/服务器 |
| --- | --- | --- |
| `src/`、`tests/`、`feedback-server/`、项目文件和依赖锁文件 | 安装包 `MailIntake-Setup-版本.exe` | `.deployment/`、SSH 配置和所有私钥 |
| `scripts/`、`docs/`、合成示例 `examples/` | 便携版 `MailIntake-win-x64.zip` | 用户邮箱配置、授权码、真实名单、正文、附件 |
| `README.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`licenses/` | `SHA256SUMS.txt` 与发布说明 | SQLite 数据库、反馈草稿、反馈导出和备份 |
| `.github/`、贡献指南、安全报告说明 | | `bin/`、`obj/`、`artifacts/` 等构建产物 |

`.gitignore` 只阻止新文件加入，不能清理已有提交历史。首次公开前检查 `git ls-files` 和全部可达历史；发现凭据时先撤销凭据，再制定历史清理方案。不要自行强制推送或删除历史。

## 两站如何分工

- GitHub：主开发入口，统一处理 PR、CI 和正式版本。
- Gitee：同步相同 `main` 和版本标签，方便国内用户获取。
- 在一个本地仓库工作，不维护两套源码。不在两个网站分别直接改同一文件。
- 保留现有 `origin` 指向 Gitee，新增 `github` 指向 GitHub，避免破坏已有操作。
- 同步命令：`git push github main`、`git push origin main`；标签分别推送到两站。禁止使用 `push --mirror`，避免覆盖无关分支。
- 每次同步后比较两站 `main` 的提交 SHA。某一站失败时记录待同步，不宣称完成。
- 暂不配置需要跨站令牌的自动镜像。后续有需要时，将最小权限令牌保存在平台 Secrets，绝不写入仓库或脚本。

## 分支与版本

- 稳定分支为 `main`；日常改动使用 `feat/*`、`fix/*`，通过 PR 合并。
- 建议在 GitHub 设置 main 保护：禁止强制推送，要求 CI 通过。维护者单人阶段可不强制第二人审批。
- 功能版本递增次版本，修复递增补丁版本，例如 `1.2.2` → `1.2.3`。
- 同步更新桌面 csproj 的 Version 与安装脚本 Version，更新 README/发布说明。
- 正式发布使用 `v版本号` 标签，指向经过测试的提交。标签和已发布安装包不得悄悄覆盖，修复后发新版本。

## 发布流程

1. 检查仓库状态，运行 `python scripts/check-public-source.py`，审阅结果；此启发式检查不能代替完整安全审计。
2. 运行核心测试、后台测试和桌面隔离启动检查。确认数据迁移与安装更新兼容。
3. 用 `scripts/build.ps1` 构建便携版；再用 `scripts/build-installer.ps1` 构建安装包。许可证自动随包分发。
4. 为安装包和便携版生成 SHA-256，创建标签并推送两站。
5. 在两站创建同版本 Release，附安装包、便携版、校验文件及更新说明。源码压缩包不是可直接运行的 Windows 安装包。
6. 分别验证仓库可匿名查看、附件可以下载。只推送 Git 标签并不等于发布了安装包。

CI 只构建与测试，不访问真实邮箱，不自动部署反馈后台，不自动向任何用户发信。反馈服务器备份与迁移按 `FEEDBACK_DEPLOYMENT.md`，本机连接信息继续留在被忽略的 `.deployment/`。
