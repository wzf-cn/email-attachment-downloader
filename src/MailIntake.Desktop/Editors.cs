using MailIntake.Core;

namespace MailIntake.Desktop;

internal class EditorForm : Form
{
    protected TableLayoutPanel Fields = new(){Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(18)};
    protected FlowLayoutPanel Footer=new(){Dock=DockStyle.Bottom,Height=66,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10)};
    protected Panel Body=new(){Dock=DockStyle.Fill,AutoScroll=true};
    public EditorForm(string title)
    {
        Text=title;Size=new Size(900,790);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);
        MinimumSize=new Size(820,650);Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,195));Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        BackColor=Design.Background;Body.BackColor=Color.White;Footer.BackColor=Color.White;ForeColor=Design.Ink;
        Body.Controls.Add(Fields);Controls.Add(Body);Controls.Add(Footer);
        var cancel=new ActionButton{Text="取消",Width=90,Height=38,DialogResult=DialogResult.Cancel};Footer.Controls.Add(cancel);CancelButton=cancel;
    }
    internal PageDeck? Sections {get;private set;}
    protected override void OnShown(EventArgs e){base.OnShown(e);Design.AttachOptionHelp(this);UiLanguage.Apply(this);}
    protected void Section(string title)
    {
        if(Sections is null)
        {
            Body.Controls.Remove(Fields);Fields.Dispose();Sections=new PageDeck(false);Body.Controls.Add(Sections);
        }
        Fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(16)};
        Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.White};scroll.Controls.Add(Fields);Sections.AddPage(title,scroll);
    }
    protected T Field<T>(string label,T control) where T:Control
    {
        int row=Fields.RowCount++;control.Dock=DockStyle.Fill;control.Margin=new Padding(4,5,4,5);
        if(control is CheckBox check)check.AutoSize=true;
        Fields.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left,MaximumSize=new Size(190,0)},0,row);
        Fields.Controls.Add(control,1,row);return control;
    }
    protected AntdUI.Input TextField(string title,string value,bool secret=false,int lines=1) => Field(title,new AntdUI.Input{Text=value,UseSystemPasswordChar=secret,Multiline=lines>1,Height=lines>1?lines*26+12:40,AutoScroll=true});
    protected ComboBox Choice(string title,string value,params string[] choices)
    { var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};box.Items.AddRange(choices);box.SelectedItem=value;return Field(title,box); }
    protected NumericUpDown Number(string title,int value,int min,int max) => Field(title,new ScrollSafeNumber{Minimum=min,Maximum=max,Value=Math.Clamp(value,min,max)});
    protected void SaveButton(Action action)
    {
        var save=new ActionButton{Type=AntdUI.TTypeMini.Primary,Text="保存",Width=110,Height=38};save.Click+=(_,_)=>{try{action();DialogResult=DialogResult.OK;Close();}catch(Exception e){MessageBox.Show(this,e.Message,"无法保存");}};Footer.Controls.Add(save);AcceptButton=save;
    }
}

internal sealed class AccountEditor : EditorForm
{
    public MailAccount Result {get;private set;}
    public AccountEditor(MailAccount? old=null):base(old is null?"绑定邮箱":"编辑邮箱")
    {
        Result=old??new();var source=Result;
        Section("邮箱登录");
        var address=TextField("邮箱地址",source.Address);
        address.Name="accountAddress";
        var password=TextField("授权码（留空保留原值）","",true);
        var since=Field("开始日期",new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",Value=source.Since});
        var enabled=Field("启用",new ToggleOption{Checked=source.Enabled,Text="启用此邮箱"});
        Section("收发服务器");
        var protocol=Choice("收信协议",source.Protocol,"IMAP","POP3");
        var host=TextField("收信服务器",source.Host);var port=Number("收信端口",source.Port,1,65535);
        var security=Choice("收信加密",source.Security,"SSL/TLS","STARTTLS");
        var folder=TextField("IMAP 文件夹",source.Folder);
        var smtp=TextField("SMTP 发信服务器",source.SmtpHost);var smtpPort=Number("SMTP 端口",source.SmtpPort,1,65535);
        var smtpSecurity=Choice("发信加密",source.SmtpSecurity,"SSL/TLS","STARTTLS");
        host.Name="incomingHost";port.Name="incomingPort";smtp.Name="smtpHost";smtpPort.Name="smtpPort";protocol.Name="incomingProtocol";
        var preset=Choice("邮箱服务商","自定义 / 保留现值",new[]{"自定义 / 保留现值"}.Concat(MailProviders.All.Select(p=>p.Name)).ToArray());
        var applyPreset=Field("",new ActionButton{Text="应用收发默认参数",Height=38});
        var providerNote=Field("识别结果",new Label{AutoSize=true,Text="输入完整邮箱地址后自动识别；未知域名请手动填写。"});
        Field("说明",new Label{AutoSize=true,Text="请先在邮箱网页开启 IMAP/POP3 和 SMTP，并使用授权码或应用专用密码。自动填入不会覆盖已有邮箱或手动修改的服务器；点击应用可主动替换。"});
        if(old is null){host.Text="";smtp.Text="";}
        var lastIncoming=(host.Text,(int)port.Value,security.Text);
        var lastOutgoing=(smtp.Text,(int)smtpPort.Value,smtpSecurity.Text);
        void ApplyProvider(MailProvider? provider,bool force=false,bool incomingOnly=false)
        {
            bool canIn=force||(old is null&&(host.Text,(int)port.Value,security.Text)==lastIncoming);
            bool canOut=!incomingOnly&&(force||(old is null&&(smtp.Text,(int)smtpPort.Value,smtpSecurity.Text)==lastOutgoing));
            if(canIn){var entry=provider?.Incoming(protocol.Text)??("",protocol.Text=="POP3"?995:993,"SSL/TLS");host.Text=entry.Item1;port.Value=entry.Item2;security.SelectedItem=entry.Item3;lastIncoming=(host.Text,(int)port.Value,security.Text);}
            if(canOut){smtp.Text=provider?.Smtp??"";smtpPort.Value=provider?.SmtpPort??465;smtpSecurity.SelectedItem=provider?.SmtpSecurity??"SSL/TLS";lastOutgoing=(smtp.Text,(int)smtpPort.Value,smtpSecurity.Text);}
            providerNote.Text=provider is null?"未识别此域名，请选择服务商或手动填写服务器。":provider.RequiresOAuth?"此服务商要求 OAuth2 登录；当前版本仅支持授权码，填入参数后仍无法直接登录。":(!canIn||(!incomingOnly&&!canOut))?"已识别服务商；保留已有或手动修改的参数。需要替换时点击应用。":"已填入收发默认参数，可按服务商要求修改。请使用授权码或应用专用密码。";
        }
        applyPreset.Click+=(_,_)=>{var provider=MailProviders.All.FirstOrDefault(p=>p.Name==preset.Text);if(provider!=null)ApplyProvider(provider,true);};
        address.TextChanged+=(_,_)=>
        {
            if(!LocalSettings.MailboxValid(address.Text.Trim()))return;
            var provider=MailProviders.Find(address.Text);preset.SelectedItem=provider?.Name??"自定义 / 保留现值";ApplyProvider(provider);
        };
        protocol.SelectedIndexChanged+=(_,_)=>{var provider=MailProviders.All.FirstOrDefault(p=>p.Name==preset.Text);if(provider!=null)ApplyProvider(provider,false,true);};
        if(old!=null){preset.SelectedItem=MailProviders.Find(source.Address)?.Name??"自定义 / 保留现值";ApplyProvider(MailProviders.Find(source.Address));}
        SaveButton(()=>
        {
            if(!LocalSettings.MailboxValid(address.Text.Trim())||string.IsNullOrWhiteSpace(host.Text)||string.IsNullOrWhiteSpace(smtp.Text))throw new ArgumentException("请填写有效的邮箱地址及服务器。");
            string encrypted=password.Text.Length>0?LocalSettings.Encrypt(password.Text):source.EncryptedPassword;
            if(encrypted.Length==0)throw new ArgumentException("请输入邮箱授权码。");
            Result=new MailAccount{Id=source.Id,Address=address.Text.Trim(),Protocol=protocol.Text,Host=host.Text.Trim(),Port=(int)port.Value,Security=security.Text,Folder=folder.Text.Trim(),
                SmtpHost=smtp.Text.Trim(),SmtpPort=(int)smtpPort.Value,SmtpSecurity=smtpSecurity.Text,EncryptedPassword=encrypted,Since=since.Value.Date,Enabled=enabled.Checked,Rules=source.Rules};
        });
    }
}

internal sealed class RuleEditor : EditorForm
{
    internal void FocusParameter(string name)
    {
        var control=Controls.Find(name,true).FirstOrDefault();
        if(control is null)return;
        Body.ScrollControlIntoView(control);control.Focus();
    }
    internal void ScrollToEnd()=>Body.AutoScrollPosition=new Point(0,Fields.Height);
    private void InlineHeading(string title)
    {
        int row=Fields.RowCount++;
        var heading=Design.Heading(title,11);heading.Dock=DockStyle.Fill;heading.Margin=new Padding(4,22,4,8);
        Fields.Controls.Add(heading,0,row);Fields.SetColumnSpan(heading,2);
    }
    public MailRule Result{get;private set;}
    public RuleEditor(MailRule? old=null,bool fromTemplate=false,DateTime? accountSince=null):base(fromTemplate?"从模板新增规则":old is null?"新增规则 · 关键词与目录成组保存":"编辑规则")
    {
        Result=old??new();var source=Result;Height=840;

        var name=TextField("规则名称",source.Name);
        var keywords=TextField("触发关键词（逗号分隔）",string.Join(',',source.Keywords.Count>0?source.Keywords:string.IsNullOrWhiteSpace(source.Prefix)?[]:[source.Prefix]));
        var searchSubject=new ToggleOption{Text="检索主题",AutoSize=true,Checked=source.SearchSubject};
        var searchBody=new ToggleOption{Text="检索正文",AutoSize=true,Checked=source.SearchBody};
        var searchAttachments=new ToggleOption{Text="检索附件名",AutoSize=true,Checked=source.SearchAttachmentNames};
        var scopes=new FlowLayoutPanel{AutoSize=true};scopes.Controls.AddRange([searchSubject,searchBody,searchAttachments]);Field("检索范围",scopes);
        var makeInstructions=Field("发给提交者",new ActionButton{Text="一键生成主题说明",Height=40});
        if(fromTemplate)Field("模板已套用",new Label{AutoSize=true,Text="请核对规则名称、关键词、检查频率及回复内容，并选择本组下载目录。"});
        var matchAll=Field("关键词模式",new ToggleOption{Text="要求全部关键词匹配",Checked=source.MatchAll});
        var frequency=Number("检查频率（分钟）",source.IntervalMinutes,1,1440);
        var start=Field("开始日期",new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",Value=source.StartDate??accountSince??DateTime.Today.AddDays(-30)});
        var continuous=new ToggleOption{Text="持续接收",Checked=!source.EndAt.HasValue&&!source.EndDate.HasValue,Name="continuousReceive"};
        var end=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm:ss",Width=260,Name="结束日期",Value=source.EndAt??source.EndDate?.Date.AddHours(23).AddMinutes(59).AddSeconds(59)??DateTime.Now};
        var endRow=new FlowLayoutPanel{AutoSize=true};endRow.Controls.Add(continuous);endRow.Controls.Add(end);Field("结束时间",endRow);
        void UpdateEnd(){end.Visible=!continuous.Checked;continuous.Text=continuous.Checked?"持续接收":"定时结束";}
        continuous.CheckedChanged+=(_,_)=>UpdateEnd();UpdateEnd();
        Field("",new Label{AutoSize=true,Text="结束时间使用电脑本地时间，精确到秒。IMAP 按收件时间，POP3 按邮件发送时间筛选。"});

        InlineHeading("下载与存放");
        var output=TextField("本组下载目录（必填）",source.Output);output.AutoScroll=false;
        var browse=Field("",new ActionButton{Text="选择该组下载目录…",Height=32});browse.Click+=(_,_)=>{using var picker=new FolderBrowserDialog();if(picker.ShowDialog(this)==DialogResult.OK)output.Text=picker.SelectedPath;};
        var downloadBody=Field("下载内容",new ToggleOption{Text="下载正文（文本及 HTML）",Checked=source.DownloadBody});
        var downloadAttachments=Field("",new ToggleOption{Text="下载附件（含云附件链接说明）",Checked=source.DownloadAttachments});
        var natural=Field("附件整理",new ToggleOption{Text="按原名整理：压缩包直接存放，普通文档按主题归组",Checked=source.NaturalLayout});
        var flatAttachments=Field("附件存放方式",new ToggleOption{Text="所有附件集中到本组目录的“全部附件”文件夹",Checked=source.FlatAttachments});
        flatAttachments.Enabled=!natural.Checked;natural.CheckedChanged+=(_,_)=>flatAttachments.Enabled=!natural.Checked;
        Field("",new Label{AutoSize=true,Text="不勾选：按邮件分别存放。勾选：附件集中存放，正文及记录仍按邮件分开；文件名带主题和唯一编号，避免同名覆盖。"});
        var attachmentLimit=Number("单个附件上限（MB）",source.MaxAttachmentMb,1,500);
        Field("超限处理",new Label{AutoSize=true,Text="默认 20 MB；超过时仅记录文件名、大小和原因，不导出该文件，也不保存包含它的完整 EML。其他附件照常导出。当前需接收邮件后判断附件大小；若要避免接收整封大邮件，请同时设置主界面的邮件上限。"});
        var saveOriginal=Field("",new ToggleOption{Text="保存原始邮件 EML（包含完整正文和随信附件）",Checked=source.SaveOriginal});
        Field("保存说明",new Label{AutoSize=true,Text="三项可独立选择；全部取消时仅保存发件人、时间等记录。若不想保存正文或附件的任何副本，也请取消原始邮件 EML。修改后需重新导出才能应用到历史邮件。"});
        InlineHeading("回复与安全");
        var replies=Field("自动回复",new ToggleOption{Text="关键词匹配成功后回复",Checked=source.ReplyEnabled});
        var success=TextField("完整匹配回复正文",source.SuccessReply,false,3);
        var detect=Field("异常检查",new ToggleOption{Text="检查同主题、不同发件邮箱的正文与附件差异",Checked=source.DetectAnomaly});
        InlineHeading("测试识别");
        var sample=TextField("测试主题（不发送邮件）","");
        var sampleBody=TextField("测试正文","",false,3);
        var sampleNames=TextField("测试附件名（逗号分隔）","");
        var test=Field("",new ActionButton{Text="测试识别",Height=32});
        static List<string> Split(string s)=>s.Replace('，',',').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct().ToList();
        MailRule Read()=>new(){Id=source.Id,Name=name.Text.Trim(),StartDate=start.Value.Date,EndAt=continuous.Checked?null:new DateTime(end.Value.Year,end.Value.Month,end.Value.Day,end.Value.Hour,end.Value.Minute,end.Value.Second),Mode="关键词",Keywords=Split(keywords.Text),MatchAll=matchAll.Checked,SearchSubject=searchSubject.Checked,SearchBody=searchBody.Checked,SearchAttachmentNames=searchAttachments.Checked,Prefix="",IntervalMinutes=(int)frequency.Value,Output=output.Text.Trim(),DownloadBody=downloadBody.Checked,DownloadAttachments=downloadAttachments.Checked,NaturalLayout=natural.Checked,FlatAttachments=flatAttachments.Checked,SaveOriginal=saveOriginal.Checked,MaxAttachmentMb=(int)attachmentLimit.Value,ReplyEnabled=replies.Checked,SuccessReply=success.Text,DetectAnomaly=detect.Checked};
        name.Name="名称";keywords.Name="关键词";scopes.Name="检索范围";frequency.Name="检查频率";start.Name="开始日期";end.Name="结束日期";output.Name="下载目录";replies.Name="自动回复";
        makeInstructions.Click+=(_,_)=>
        {
            try { using var dialog=new SubjectInstructionsForm(Read());dialog.ShowDialog(this); }
            catch(Exception e){MessageBox.Show(this,UiLanguage.T(e.Message),UiLanguage.T("请完善规则"));}
        };
        test.Click+=(_,_)=>{try{var rule=Read();RuleValidator.Check(rule);var result=RuleValidator.Match(sample.Text,rule,sampleBody.Text,Split(sampleNames.Text));MessageBox.Show(this,result switch{Validation.Success=>"完整匹配：下载并按设置回复",Validation.Ignore=>"未命中关键词：忽略，不回复",_=>"校验未通过：统一错误提示（内部原因："+result+"）"},"识别结果");}catch(Exception e){MessageBox.Show(this,e.Message,"请完善规则");}};
        SaveButton(()=>{var rule=Read();RuleValidator.Check(rule);Result=rule;});
        var saveTemplate=new ActionButton{Text="保存为模板",Width=130,Height=38};
        saveTemplate.Click+=(_,_)=>{try{TemplateFiles.Save(this,Read());}catch(Exception e){MessageBox.Show(this,e.Message,"模板保存未完成");}};
        Footer.Controls.Add(saveTemplate);
    }
}
