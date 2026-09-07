using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MailIntake.Desktop;

internal static class UiLanguage
{
    private sealed class Caption { public string Source=""; public bool Updating; public int Width; }
    private static readonly ConditionalWeakTable<Control,Caption> captions=new();
    private static string Preference=>Path.Combine(LocalSettings.Root,"language.txt");
    public static bool English {get;private set;}
    public static string Original(string? text)=>Reverse.GetValueOrDefault(text??"",text??"");
    private static readonly Dictionary<string,string> Words=Catalog.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(l=>l.TrimEnd('\r').Split('|',2)).Where(p=>p.Length==2).GroupBy(p=>p[0]).ToDictionary(g=>g.Key,g=>g.Last()[1]);
    private static readonly Dictionary<string,string> Reverse=Words.GroupBy(p=>p.Value).ToDictionary(g=>g.Key,g=>g.First().Key);
    public static void Load()
    {
        try{English=File.Exists(Preference)&&File.ReadAllText(Preference).Trim()=="en";}catch{English=false;}
    }
    public static void Change(bool english)
    {
        Directory.CreateDirectory(LocalSettings.Root);File.WriteAllText(Preference+".tmp",english?"en":"zh");File.Move(Preference+".tmp",Preference,true);
        English=english;
        foreach(Form form in Application.OpenForms)Apply(form);
    }
    public static string T(string text)
    {
        if(!English)return text;
        if(Words.TryGetValue(text,out var translated))return translated;
        var roster=Regex.Match(text,@"^导入 CSV · 已有 (\d+) 条$");
        if(roster.Success)return "Import CSV · "+roster.Groups[1].Value+" entries";
        return text;
    }
    public static void Apply(Control root)
    {
        if(root is Form window)window.Icon=AppIcon.Value;
        if(root is PageDeck deck)deck.UpdateLanguageLayout(English);
        if(root is MainForm main)main.MinimumSize=new Size(English?1240:1100,760);
        if(root.Tag is not "keep-text"&&(root is Form or Label or ActionButton or CheckBox))
        {
            if(!captions.TryGetValue(root,out var item))
            {
                item=new(){Source=root.Text,Width=root.Width};captions.Add(root,item);
                root.TextChanged+=(_,_)=>{if(item.Updating)return;item.Source=root.Text;Render(root,item);};
            }
            Render(root,item);
        }
        if(root is ComboBox box&&!Equals(box.Tag,"localized-choice"))
        {
            // Only presentation changes. ComboBox.Text and saved rule enum values remain canonical.
            box.Tag="localized-choice";box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=26;
            box.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,T(box.Items[e.Index]?.ToString()??""),box.Font,e.Bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};
        }
        if(root is DataGridView grid)
        {
            void Headers(){foreach(DataGridViewColumn col in grid.Columns)col.HeaderText=T(col.DataPropertyName);}
            if(!Equals(grid.Tag,"localized-grid")){grid.Tag="localized-grid";grid.DataBindingComplete+=(_,_)=>Headers();grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&(grid.Columns[e.ColumnIndex].DataPropertyName is "状态" or "模式" or "接收状态" or "自动回复")&&e.Value is string value)e.Value=T(value);};}
            Headers();
        }
        foreach(Control child in root.Controls)Apply(child);
        root.Invalidate();
    }
    private static void Render(Control control,Caption item)
    {
        item.Updating=true;
        try
        {
            control.Text=T(item.Source);
            if(control is ActionButton&&control.Dock==DockStyle.None)
            {
                int measured=TextRenderer.MeasureText(control.Text,control.Font).Width+24;
                control.Width=English?Math.Max(item.Width,Math.Min(measured,control.Parent?.ClientSize.Width-16??300)):item.Width;
            }
        }
        finally{item.Updating=false;}
    }
    private const string Catalog="""
建议关键词|Suggested keywords
采用建议关键词|Use suggested keywords
建议说明|About suggestions
生成后给出主题中的固定文字作为建议；关键词为空时自动填入，已有关键词不会覆盖。|Use the topic's fixed text as keywords. Empty keywords are filled after generation; existing keywords are preserved.
本次邮件主题|Submission topic
主题填写示例|Example topic
例如：工程实践报告。生成后会自动补上姓名、学号等占位项目。|Example: Practice report. Name and ID placeholders are added automatically when required.
请先填写触发关键词。|Enter keywords first.
关键词不能包含换行。|Keywords cannot contain line breaks.
请填写主题项目和分隔符；关键词不要包含字段分隔符。|Set the required fields and separator. Keywords must not contain the field separator.
关键词匹配成功后回复|Reply when keywords match
请核对规则名称、关键词、检查频率及回复内容，并选择本组下载目录。|Review the rule name, keywords, interval and reply, then choose a download folder.
发给提交者|For senders
一键生成主题说明|Generate subject instructions
主题填写说明|Subject instructions
复制说明|Copy instructions
已复制|Copied
可修改下面的说明，再复制发给提交邮件的人。|Edit the instructions below, then copy and share them with senders.
复制失败，请选中文字后按 Ctrl+C。|Could not copy. Select the text and press Ctrl+C.
检索主题|Search subject
检索正文|Search body
检索附件名|Search attachment names
检索范围|Search in
检索说明|Search details
请至少选择一个检索范围。|Select at least one search scope.
测试正文|Sample body
测试附件名（逗号分隔）|Sample filenames (comma-separated)
可多选。全部匹配时，每个关键词可出现在不同范围。正文或附件名检索需先接收邮件；不搜索附件内部内容。结构校验仍只校验主题。|Select one or more scopes. Required keywords may occur in different scopes. Body and filename searches download the message first; attachment contents are not searched. Structure validation still checks the subject.
支持开源项目|Support the project
选择一个平台，登录后点击项目页面上的 Star。|Sign in to GitHub or Gitee, then click Star.
感谢你的支持|Thank you for your support
检索主题、邮箱或规则|Search subject, email or rule
清空检索|Clear search
检查频率|Check frequency
检查频率（分钟）|Check interval (minutes)
检查频率必须为 1 到 1440 分钟。|Check interval must be between 1 and 1440 minutes.
分隔符和主题项目不能为空。|Separator and subject fields are required.
启动时检查软件更新|Check for software updates at startup
软件更新|Software updates
检查软件更新|Check for updates
无法保存更新设置。|Could not save update preferences.
邮件接收管理|Mail Intake
邮箱与规则|Accounts / rules
归档记录|Archives
回复记录|Replies
停收管理|Blocked senders
异常与审计|Alerts / audit
运行日志|Logs
运行设置|Settings
意见反馈|Feedback
赞助支持|Donate
点个 Star|Star us
绑定邮箱|Add account
编辑邮箱|Edit account
移除|Remove
测试连接|Test login
导入旧版配置|Import legacy
新增规则|Add rule
从模板新增|Use template
保存为模板|Save template
编辑规则|Edit rule
删除规则|Delete rule
上移|Move up
打开下载目录|Open folder
刷新|Refresh
打开选中目录|Open selected folder
导出 CSV|Export CSV
重置并恢复接收|Reset and unblock
查看详情|View details
保存并开始|Save and start
立即检查|Check now
暂停|Pause
退出软件|Exit
取消|Cancel
保存|Save
关闭|Close
绑定邮箱 · 选中邮箱后管理其规则|Accounts · Select an account to manage its rules
收件规则 · 每组规则使用独立下载目录|Rules · Each group has its own download folder
尚未开始 · 请先绑定邮箱并设置规则|Not started · Add an account and configure rules
自动检查间隔（分钟）|Check interval (minutes)
整封邮件上限（MB）|Message limit (MB)
每小时最多自动回复（封）|Reply limit per hour
连续错误几次后停收|Block after consecutive errors
开机启动|Startup
登录 Windows 后自动运行|Run at Windows sign-in
历史邮件|Previous messages
忽略之前的记录，重新导出（仅本次，不回复）|Export again once (no replies)
设置生效|Apply changes
修改后点击下方“保存并开始”。重新导出仅用于下一次检查，不重复自动回复。邮件上限用于跳过整封大邮件；附件大小在各组规则中设置。|Click Save & start to apply changes. Export again only affects the next check and sends no replies. Set attachment limits in each rule.
邮箱登录|Account sign-in
邮箱地址|Email address
授权码（留空保留原值）|App password (blank to keep)
首次处理起始日期|Process messages since
开始日期|Start date
启用|Enabled
启用此邮箱|Enable this account
收发服务器|Mail servers
收信协议|Incoming protocol
收信服务器|Incoming server
收信端口|Incoming port
收信加密|Incoming encryption
IMAP 文件夹|IMAP folder
SMTP 发信服务器|SMTP server
SMTP 端口|SMTP port
发信加密|SMTP encryption
邮箱服务商|Email provider
自定义 / 保留现值|Custom / keep current
应用收发默认参数|Apply server defaults
识别结果|Detection result
说明|Notes
输入完整邮箱地址后自动识别；未知域名请手动填写。|Enter a full email address to detect the provider. Configure unknown domains manually.
未识别此域名，请选择服务商或手动填写服务器。|Unknown domain. Select a provider or enter the servers manually.
此服务商要求 OAuth2 登录；当前版本仅支持授权码，填入参数后仍无法直接登录。|This provider requires OAuth2, which this version does not support. Server defaults alone will not enable sign-in.
已识别服务商；保留已有或手动修改的参数。需要替换时点击应用。|Provider detected. Existing or manually edited settings were kept. Click Apply to replace them.
已填入收发默认参数，可按服务商要求修改。请使用授权码或应用专用密码。|Server defaults filled in. You can edit them. Use an authorization code or app password.
请先在邮箱网页开启 IMAP/POP3 和 SMTP，并使用授权码或应用专用密码。自动填入不会覆盖已有邮箱或手动修改的服务器；点击应用可主动替换。|Enable IMAP/POP3 and SMTP in webmail and use an app password. Automatic defaults preserve existing and manually edited servers. Apply explicitly replaces them.
请填写有效的邮箱地址及服务器。|Enter a valid email address and server names.
请输入邮箱授权码。|Enter the email app password.
触发与主题|Subject matching
下载与存放|Downloads
回复与安全|Replies / safety
测试识别|Test matching
新增规则 · 关键词与目录成组保存|Add rule · Keywords and destination
从模板新增规则|Create rule from template
规则名称|Rule name
模式|Mode
结构校验|Structured subject
关键词|Keywords
触发关键词（逗号分隔）|Keywords (commas)
关键词模式|Keyword matching
要求全部关键词匹配|Require all keywords
固定开头|Fixed prefix
字段分隔符|Field separator
主题包含哪些项目|Required subject fields
填写示例|Example
填“姓名”表示发件人须在主题中写自己的姓名。例如：工程实践-张三-00123。若还要求班级，填“姓名,班级”。学号在下方“结尾校验”中设置。|Field names describe what senders must enter, e.g. Name. Example: Practice-Alice-00123. Add fields with commas. Configure the final ID or key below. Existing Chinese field names remain unchanged.
结尾校验|Final field validation
名单|Roster
固定秘钥|Fixed key
批量名单|Import roster
主题示例|Subject example
命中关键词即可处理，无需校验主题结构。|Matching keywords is sufficient; subject structure is not checked.
本组下载目录（必填）|Download folder (required)
选择该组下载目录…|Choose download folder…
下载内容|Save content
下载正文（文本及 HTML）|Save body (text and HTML)
下载附件（含云附件链接说明）|Save attachments and cloud links
附件存放方式|Attachment layout
所有附件集中到本组目录的“全部附件”文件夹|Put all attachments in one shared folder
不勾选：按邮件分别存放。勾选：附件集中存放，正文及记录仍按邮件分开；文件名带主题和唯一编号，避免同名覆盖。|Unchecked: one folder per message. Checked: attachments share a folder; body and metadata stay separate. Unique filenames prevent overwrites.
单个附件上限（MB）|Attachment limit (MB)
超限处理|Oversized attachments
默认 20 MB；超过时仅记录文件名、大小和原因，不导出该文件，也不保存包含它的完整 EML。其他附件照常导出。当前需接收邮件后判断附件大小；若要避免接收整封大邮件，请同时设置主界面的邮件上限。|Default: 20 MB. Oversized files are recorded but not saved; their full EML is also skipped. Other attachments are saved. This check happens after receiving the message. Use the message limit to avoid fetching large messages.
保存原始邮件 EML（包含完整正文和随信附件）|Save original EML (full body and attachments)
保存说明|Save options
三项可独立选择；全部取消时仅保存发件人、时间等记录。若不想保存正文或附件的任何副本，也请取消原始邮件 EML。修改后需重新导出才能应用到历史邮件。|Select options independently. With none selected, only metadata is saved. EML contains the full body and attachments. Export again to apply changes to previously processed messages.
自动回复|Automatic replies
完整匹配时回复；错误主题按统一策略回复|Reply to valid subjects; use the error policy for invalid subjects
完整匹配回复正文|Success reply body
异常检查|Anomaly checks
检查同主题、不同发件邮箱的正文与附件差异|Detect differing content sent under the same subject by different senders
错误与停收策略|Errors and blocking
测试主题（不发送邮件）|Test subject (no email sent)
完整匹配：下载并按设置回复|Match: download and reply as configured
未命中关键词：忽略，不回复|No keyword match: ignore without replying
请完善规则|Complete the rule
邮箱|Email
协议|Protocol
服务器|Server
状态|Status
规则数|Rules
名称|Name
自动回复|Automatic replies
下载目录|Folder
收件邮箱|Receiving account
发件邮箱|Sender
主题|Subject
规则|Rule
接收时间|Received
保存时间|Saved
目录|Folder
时间|Time
类型|Type
连续错误次数|Consecutive errors
接收状态|Receiving status
更新时间|Updated
详情|Details
停用|Disabled
已停收|Blocked
正常|Normal
服务器已接受|Accepted by server
发送中或中断待核实|Sending or interrupted; verify manually
结果不确定|Uncertain
达到回复限额|Reply limit reached
反馈类型|Feedback type
使用问题|Problem
功能建议|Feature request
界面体验|UI feedback
其他|Other
标题|Title
具体说明|Details
联系方式（选填）|Contact (optional)
发送说明|What is sent
提交状态|Submission status
提交反馈|Send feedback
保存到本地|Save locally
反馈将直接提交给软件开发者，无需邮箱或 Gitee 账号。提交内容包括上方填写的信息和软件版本；不会自动附带邮箱配置、邮件、附件或日志。|Sends your input and app version directly to the developer. No email or Gitee account is needed. No email settings, messages, attachments or logs are included automatically.
请勿填写授权码、秘钥或他人的个人信息。|Do not include passwords, keys or other people's personal information.
请填写标题和具体说明。|Enter a title and details.
标题最多 120 字，说明最多 10000 字，联系方式最多 200 字。|Maximum lengths: title 120, details 10000, contact 200 characters.
正在提交，请稍候…|Sending, please wait…
已保存到本地，尚未提交。|Saved locally; not submitted.
本地保存失败，请检查目录权限或手动复制内容。|Could not save locally. Check folder permissions or copy the text manually.
提交较频繁，请一小时后再试。草稿已保留。|Too many submissions. Retry in an hour; your draft was kept.
暂未完成提交，草稿已保留。请稍后重试。|Submission failed. Your draft was kept; try again later.
反馈地址配置不正确，需要 HTTPS 地址。|The feedback endpoint must be a valid HTTPS URL.
请作者喝杯咖啡|Buy the developer a coffee
觉得好用？点个 Star 支持一下|Like this app? Give it a Star
你的 Star 能让更多人发现这个项目。选择一个平台，登录后点击仓库页面上的 Star 即可。|Help others discover this project. Choose a platform, sign in, and click Star on the repository page.
感谢你支持开发与维护。打赏完全自愿，不影响任何软件功能。|Thank you! Donations are optional; all features remain available.
GitHub 点 Star|Star on GitHub
Gitee 点 Star|Star on Gitee
微信支付|WeChat Pay
支付宝|Alipay
支付宝支付|Alipay
查看微信支付原图|Open WeChat QR image
查看支付宝原图|Open Alipay QR image
打开打赏页面|Open donation page
PayPal 赞助|Donate via PayPal
打赏信息暂时无法显示，请稍后再试。|Donation details are unavailable. Please try again later.
作者尚未提供打赏方式。你也可以通过点 Star 或反馈建议支持项目，谢谢！|No donation method is configured. You can also support the project with a Star or feedback. Thank you!
操作未完成|Action not completed
无法保存|Unable to save
导入失败|Import failed
事件详情|Event details
识别结果|Detection result
模板保存未完成|Template not saved
请先绑定邮箱。|Add an email account first.
请先绑定并选中邮箱。|Add and select an account first.
请先保存设置并开始。|Save your settings and start first.
请先暂停检查后导入。|Pause checking before importing.
请等待本轮结束后再检查或重新导出。|Wait for the current check to finish before checking or exporting again.
请先暂停并等待本轮处理结束，再重置。|Pause and wait for the current check to finish before resetting.
正在测试接收与发送登录（不发信）…|Testing incoming and outgoing sign-in (no email sent)…
已暂停。已进入发送阶段的结果可能需人工核实。|Paused. Replies that were already being sent may require manual verification.
记录已导出。|Records exported.
正在等待当前邮件处理结束，以便安全退出…|Waiting for the current message to finish before exiting…
显示窗口|Show window
暂停检查|Pause checking
退出|Exit
界面语言|Language
单个附件上限必须为 1 到 500 MB。|The attachment limit must be between 1 and 500 MB.
请填写规则名称。|Enter a rule name.
请为此组规则选择完整下载目录。|Choose an absolute download folder for this rule.
至少填写一个关键词。|Enter at least one keyword.
固定开头、分隔符和中间字段不能为空。|The prefix, separator and required fields cannot be empty.
请导入学号或秘钥名单。|Import a roster of student IDs or keys.
固定秘钥不能为空，且不能含分隔符。|The fixed key cannot be empty or contain the separator.
请填写成功回复内容。|Enter the success reply body.
名单没有有效数据。|The roster contains no valid records.
模板已套用|Template applied
下载方式、大小限制、主题结构等已填好。请核对本次规则名称、关键词和检查频率，选择下载目录，并导入本次名单或填写秘钥。回复内容如有任务名称，也请相应修改。|Reusable settings are filled in. Review the rule name, keywords and check interval, choose a folder, and import a roster or enter a key. Update any task names in the reply body.
错误统一回复：主题的格式或内容不符合要求。默认连续 3 次错误后通知联系管理员并停收；次数可在“运行设置”调整。完整主题校验成功后清零，已停收须管理员重置。|Invalid subjects receive a generic rejection. After 3 consecutive errors by default, notify and block the sender. Change the threshold in Settings. A valid subject clears the count; blocked senders require an administrator reset.
已保存并开始。开始日期之后的历史匹配邮件也会处理和回复。|Saved and started. Matching messages since the start date will also be processed and replied to.
""";
}
