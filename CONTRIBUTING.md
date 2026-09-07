# 参与贡献

请优先在 GitHub 提交 Issue 和 Pull Request，Gitee 用于国内访问与源码同步。

1. 从 `main` 创建 `feat/简短名称` 或 `fix/简短名称` 分支，一次解决一个明确问题。
2. 修改前说明预期行为；提交时写清修改内容、兼容影响和验证结果。
3. 涉及邮件处理、重复回复、停收、数据迁移时，补充行为测试。界面修改附合成数据截图。
4. 使用 .NET 10 SDK，运行 `dotnet run --project tests/MailIntake.Tests -c Release`；桌面构建要求 Windows。
5. 后台修改运行 `python -m unittest discover -s feedback-server -p test_server.py -v`。
6. 不提交邮箱授权码、真实邮件/名单/附件、反馈记录、私钥或用户配置。截图使用 example.test 等演示地址。

默认不接受未经说明的自动发信、凭据上传或数据目录变更。贡献者应有权提交相关代码，贡献将采用本仓库 MIT 许可证；第三方代码保留原有许可和出处。
