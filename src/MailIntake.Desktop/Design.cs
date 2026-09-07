namespace MailIntake.Desktop;

internal sealed class ActionButton : AntdUI.Button
{
    private readonly ToolTip help=new(){InitialDelay=500,ReshowDelay=150,AutoPopDelay=10000,ShowAlways=true};
    public ActionButton()
    {
        Height=38; Width=112; Radius=6; BorderWidth=1; WaveSize=0;
        DefaultBorderColor=Color.FromArgb(220,226,235);
        Margin=new Padding(0,0,8,6);
    }
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);UpdateHelp();}
    protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);UpdateHelp();}
    protected override void OnMouseEnter(EventArgs e){UpdateHelp();base.OnMouseEnter(e);}
    private void UpdateHelp()
    {
        string description=ButtonHelp.Describe(Text??"");
        if(Text=="取消"&&FindForm() is FeedbackForm)description="关闭反馈窗口；未提交的填写内容会保留为本地草稿。";
        help?.SetToolTip(this,description);AccessibleDescription=description;
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)help.Dispose();}
}

internal static class ButtonHelp
{
    public static string Describe(string text)=>text switch
    {
        "邮箱与规则"=>"管理绑定的邮箱，以及每个邮箱的收件规则。",
        "归档记录"=>"查看已保存邮件的发件人、时间和文件位置。",
        "回复记录"=>"查看自动回复的发送状态及结果。",
        "停收管理"=>"查看停收名单，按发件邮箱恢复接收。",
        "异常与审计"=>"查看异常原因和管理员操作记录。",
        "运行日志"=>"查看收信、下载和连接过程的运行信息。",
        "运行设置"=>"设置检查间隔、大小上限、停收次数和开机启动。",
        "意见反馈"=>"向开发者提交问题或建议，无需邮箱或 Gitee 账号。",
        "绑定邮箱"=>"添加接收邮件的邮箱，填写授权码和收发服务器。",
        "编辑邮箱"=>"修改选中邮箱的连接参数或启用状态。",
        "移除"=>"从配置中移除选中邮箱及其规则；已下载文件保留。",
        "测试连接"=>"验证选中邮箱的收发登录是否正常，不发送邮件。",
        "导入旧版配置"=>"导入旧版邮箱配置和处理记录，请先暂停检查。",
        "新增规则"=>"为选中邮箱新增一组匹配条件和下载目录。",
        "从模板新增"=>"套用模板建立独立规则，再填写本次任务的信息。",
        "保存为模板"=>"保存可复用的规则设置；不包含名单、秘钥和下载目录。",
        "编辑规则"=>"修改选中规则，完成后点击主界面的“保存并开始”应用。",
        "删除规则"=>"移除选中规则，已下载文件和历史记录保留。",
        "上移"=>"将选中规则上移一位；多条规则命中时，顺序影响回复选择。",
        "打开下载目录"=>"打开选中规则的下载目录；目录不存在时会创建。",
        "刷新"=>"重新读取本地记录，不会连接邮箱收取新邮件。",
        "打开选中目录"=>"打开选中归档记录对应的邮件文件夹。",
        "导出 CSV"=>"将当前列表导出为可用 Excel 打开的 CSV 文件。",
        "重置并恢复接收"=>"恢复选中发件邮箱的接收并清零错误次数；旧邮件不自动补处理。",
        "查看详情"=>"显示选中异常或审计记录的完整说明。",
        "保存并开始"=>"保存当前配置并启动定时检查，使邮箱与规则修改生效。",
        "立即检查"=>"使用已保存配置检查邮件；如勾选重新导出，仅本轮应用。",
        "暂停"=>"停止自动检查并请求取消当前处理，软件保持打开。",
        "退出软件"=>"等待当前处理安全结束后退出，停止后台检查。",
        "保存"=>"保存本窗口的设置；邮箱和规则修改需在主界面点击“保存并开始”应用。",
        "取消"=>"关闭窗口，不应用本次未保存的设置。",
        "邮箱登录"=>"填写邮箱地址、授权码和开始接收的日期。",
        "收发服务器"=>"设置收信和发信的服务器、端口与加密方式。",
        "应用所选 SMTP 默认值"=>"用所选邮箱服务商的默认参数替换当前发信服务器设置。",
        "触发与主题"=>"设置邮件主题的关键词和格式校验要求。",
        "下载与存放"=>"选择下载目录、保存内容、附件大小和存放方式。",
        "回复与安全"=>"设置成功回复内容及异常检查。",
        "测试识别" or "测试主题"=>"用填写的主题预览匹配结果，不下载文件或发送邮件。",
        "选择该组下载目录…"=>"选择这组规则匹配成功后保存文件的位置。",
        "提交反馈"=>"将填写的反馈和软件版本提交给开发者；不附带邮箱配置或邮件。",
        "保存到本地"=>"保存反馈草稿并可导出文本文件，不会提交到服务器。",
        _ when text.StartsWith("导入 CSV")=>"导入学号或秘钥名单，用于校验邮件主题中的身份信息。",
        _=>"打开“"+text+"”。"
    };
}

internal static class Design
{
    public static void AttachOptionHelp(Form form)
    {
        var tips=new ToolTip{InitialDelay=500,ReshowDelay=150,AutoPopDelay=10000,ShowAlways=true};
        void Walk(Control root)
        {
            foreach(Control control in root.Controls)
            {
                if(control is CheckBox)
                {
                    string hint=control.Text switch
                    {
                        var t when t.StartsWith("忽略之前")=>"仅下一轮重新导出匹配的历史邮件；不重复回复或累计错误次数。",
                        var t when t.StartsWith("登录 Windows")=>"登录 Windows 后启动软件；保存并开始后应用。",
                        var t when t.StartsWith("所有附件")=>"勾选后附件集中保存；否则按邮件分别存放。",
                        var t when t.StartsWith("保存原始")=>"保存完整 EML，其中也包含正文和随信附件；超限时跳过。",
                        var t when t.StartsWith("下载正文")=>"将邮件正文保存为文本，存在 HTML 时同时保存 HTML。",
                        var t when t.StartsWith("下载附件")=>"保存随信附件；云附件仅记录下载链接。",
                        var t when t.StartsWith("检查同主题")=>"不同邮箱发送同一主题、内容差异较大时记录异常。",
                        var t when t.StartsWith("要求全部")=>"关键词模式下，主题须包含全部关键词才匹配。",
                        var t when t.StartsWith("完整匹配")=>"按规则发送成功或错误回复；停收通知仍受全局策略控制。",
                        "启用此邮箱"=>"启用后将按规则检查这个邮箱；取消勾选可暂停此邮箱。",
                        _=>"启用或关闭此选项。"
                    };
                    tips.SetToolTip(control,hint);control.AccessibleDescription=hint;
                }
                Walk(control);
            }
        }
        Walk(form);form.Disposed+=(_,_)=>tips.Dispose();
    }
    public static readonly Color Background=Color.FromArgb(244,246,250);
    public static readonly Color Ink=Color.FromArgb(30,41,59);
    public static readonly Color Muted=Color.FromArgb(100,116,139);
    public static Label Heading(string text,int size=15)=>new(){Text=text,AutoSize=false,Height=48,Dock=DockStyle.Top,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Microsoft YaHei UI",size,FontStyle.Bold),ForeColor=Ink};
    public static Panel Card(string title,Control content,Control? toolbar=null)
    {
        var card=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(18,6,18,14)};
        card.Controls.Add(content);
        if(toolbar!=null)card.Controls.Add(toolbar);
        card.Controls.Add(Heading(title,11));return card;
    }
}

// A single page is visible at a time; controls stay alive so unsaved edits survive navigation.
internal sealed class PageDeck : Panel
{
    private readonly FlowLayoutPanel navigation;
    private readonly Panel content=new(){Dock=DockStyle.Fill,Padding=new Padding(20),BackColor=Design.Background};
    private readonly List<(Control Page,ActionButton Button)> pages=[];
    public PageDeck(bool sidebar)
    {
        Dock=DockStyle.Fill;
        navigation=new(){Dock=sidebar?DockStyle.Left:DockStyle.Top,Width=176,Height=64,
            FlowDirection=sidebar?FlowDirection.TopDown:FlowDirection.LeftToRight,WrapContents=false,
            BackColor=Color.White,Padding=new Padding(12)};
        Controls.Add(content);Controls.Add(navigation);
    }
    public void AddPage(string title,Control page)
    {
        page.Dock=DockStyle.Fill;page.Visible=false;content.Controls.Add(page);
        var button=new ActionButton{Text=title,Width=navigation.Dock==DockStyle.Left?150:142,Height=40,BorderWidth=0};
        int index=pages.Count;button.Click+=(_,_)=>SelectPage(index);navigation.Controls.Add(button);pages.Add((page,button));
        if(index==0)SelectPage(0);
    }
    public void SelectPage(int index)
    {
        for(int i=0;i<pages.Count;i++)
        {
            pages[i].Page.Visible=i==index;
            pages[i].Button.Type=i==index?AntdUI.TTypeMini.Primary:AntdUI.TTypeMini.Default;
        }
        pages[index].Page.BringToFront();
    }
    public int PageCount=>pages.Count;
}

