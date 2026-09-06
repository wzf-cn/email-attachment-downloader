using MailIntake.Core;

namespace MailIntake.Desktop;

internal class EditorForm : Form
{
    protected TableLayoutPanel Fields = new(){Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(18)};
    protected FlowLayoutPanel Footer=new(){Dock=DockStyle.Bottom,Height=55,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10)};
    protected Panel Body=new(){Dock=DockStyle.Fill,AutoScroll=true};
    public EditorForm(string title)
    {
        Text=title;Size=new Size(800,700);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);
        MinimumSize=new Size(700,500);Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,195));Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Body.Controls.Add(Fields);Controls.Add(Body);Controls.Add(Footer);
        var cancel=new Button{Text="取消",Width=90,Height=34,DialogResult=DialogResult.Cancel};Footer.Controls.Add(cancel);CancelButton=cancel;
    }
    protected T Field<T>(string label,T control) where T:Control
    {
        int row=Fields.RowCount++;control.Dock=DockStyle.Fill;control.Margin=new Padding(4,5,4,5);
        if(control is CheckBox check)check.AutoSize=true;
        Fields.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left,MaximumSize=new Size(190,0)},0,row);
        Fields.Controls.Add(control,1,row);return control;
    }
    protected TextBox TextField(string title,string value,bool secret=false,int lines=1) => Field(title,new TextBox{Text=value,UseSystemPasswordChar=secret,Multiline=lines>1,Height=lines>1?lines*24:28,ScrollBars=lines>1?ScrollBars.Vertical:ScrollBars.None});
    protected ComboBox Choice(string title,string value,params string[] choices)
    { var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};box.Items.AddRange(choices);box.SelectedItem=value;return Field(title,box); }
    protected NumericUpDown Number(string title,int value,int min,int max) => Field(title,new NumericUpDown{Minimum=min,Maximum=max,Value=Math.Clamp(value,min,max)});
    protected void SaveButton(Action action)
    {
        var save=new Button{Text="保存",Width=110,Height=34};save.Click+=(_,_)=>{try{action();DialogResult=DialogResult.OK;Close();}catch(Exception e){MessageBox.Show(this,e.Message,"无法保存");}};Footer.Controls.Add(save);AcceptButton=save;
    }
}

internal sealed class AccountEditor : EditorForm
{
    public MailAccount Result {get;private set;}
    public AccountEditor(MailAccount? old=null):base(old is null?"绑定邮箱":"编辑邮箱")
    {
        Result=old??new();var source=Result;
        var address=TextField("邮箱地址",source.Address);
        var protocol=Choice("收信协议",source.Protocol,"IMAP","POP3");
        var host=TextField("收信服务器",source.Host);var port=Number("收信端口",source.Port,1,65535);
        var security=Choice("收信加密",source.Security,"SSL/TLS","STARTTLS");
        var folder=TextField("IMAP 文件夹",source.Folder);
        var smtp=TextField("SMTP 发信服务器",source.SmtpHost);var smtpPort=Number("SMTP 端口",source.SmtpPort,1,65535);
        var smtpSecurity=Choice("发信加密",source.SmtpSecurity,"SSL/TLS","STARTTLS");
        var preset=Choice("SMTP 默认配置","自定义 / 保留现值","自定义 / 保留现值","QQ","163","126");
        var applyPreset=Field("",new Button{Text="应用所选 SMTP 默认值",Height=32});
        void ApplySmtp(string provider)
        {
            string? server=provider switch{"QQ"=>"smtp.qq.com","163"=>"smtp.163.com","126"=>"smtp.126.com",_=>null};
            if(server is null)return;
            smtp.Text=server;smtpPort.Value=465;smtpSecurity.SelectedItem="SSL/TLS";
        }
        applyPreset.Click+=(_,_)=>ApplySmtp(preset.Text);
        var lastDefault=(Host:source.SmtpHost,Port:source.SmtpPort,Security:source.SmtpSecurity);
        address.Leave+=(_,_)=>
        {
            string provider=address.Text.Trim().Split('@').Last().ToLowerInvariant() switch{"qq.com"=>"QQ","163.com"=>"163","126.com"=>"126",_=>"自定义 / 保留现值"};
            preset.SelectedItem=provider;
            // Existing accounts and manually changed settings are never silently overwritten.
            if(old is null && (smtp.Text,(int)smtpPort.Value,smtpSecurity.Text)==lastDefault)
            {
                ApplySmtp(provider);
                lastDefault=(smtp.Text,(int)smtpPort.Value,smtpSecurity.Text);
            }
        };
        var password=TextField("授权码（留空保留原值）","",true);
        var since=Field("开始日期",new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",Value=source.Since});
        var enabled=Field("启用",new CheckBox{Checked=source.Enabled,Text="启用此邮箱"});
        Field("说明",new Label{AutoSize=true,Text="QQ、163、126 的 SMTP 默认值为对应服务器 / 465 / SSL/TLS。新建邮箱填写地址后自动填入；已有配置可点击应用。收信参数请单独填写。POP3 按邮件 Date 筛选；此版本使用授权码登录。"});
        protocol.SelectedIndexChanged+=(_,_)=>{if(source.Host=="imap.qq.com"||source.Host=="pop.qq.com"){host.Text=protocol.Text=="POP3"?"pop.qq.com":"imap.qq.com";port.Value=protocol.Text=="POP3"?995:993;}};
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
    public MailRule Result{get;private set;}
    public RuleEditor(MailRule? old=null):base(old is null?"新增规则 · 关键词与目录成组保存":"编辑规则")
    {
        Result=old??new();var source=Result;Height=820;
        var name=TextField("规则名称",source.Name);
        var mode=Choice("模式",source.Mode,"结构校验","关键词");
        var keywords=TextField("触发关键词（逗号分隔）",string.Join(',',source.Keywords));
        var matchAll=Field("关键词模式",new CheckBox{Text="要求全部关键词匹配",Checked=source.MatchAll});
        var prefix=TextField("固定开头",source.Prefix);
        var separator=TextField("字段分隔符",source.Separator);
        var fields=TextField("主题中需填写的信息",string.Join(',',source.Fields));
        Field("填写示例",new Label{AutoSize=true,Text="例如主题为“工程实践-张三-00123”：固定开头填“工程实践”，此处填“姓名”，结尾用学号名单校验。此处填写信息名称，不是具体姓名；多个名称用逗号分隔，例如“姓名,班级”。"});
        var keyMode=Choice("结尾校验",source.KeyMode,"名单","固定秘钥");
        var subjectKey=TextField("固定秘钥",source.SubjectKey,true);
        var roster=new Dictionary<string,string>(source.Roster);
        var rosterButton=Field("批量名单",new Button{Text=$"导入 CSV · 已有 {roster.Count} 条",Height=32});
        rosterButton.Click+=(_,_)=>{using var picker=new OpenFileDialog{Filter="学号名单 CSV|*.csv"};if(picker.ShowDialog(this)!=DialogResult.OK)return;try{var data=RuleValidator.ImportRoster(picker.FileName);roster=data;keyMode.Text="名单";rosterButton.Text=$"导入 CSV · 已有 {roster.Count} 条";}catch(Exception e){MessageBox.Show(this,e.Message,"导入失败");}};
        var output=TextField("本组下载目录（必填）",source.Output);
        var browse=Field("",new Button{Text="选择该组下载目录…",Height=32});browse.Click+=(_,_)=>{using var picker=new FolderBrowserDialog();if(picker.ShowDialog(this)==DialogResult.OK)output.Text=picker.SelectedPath;};
        var downloadBody=Field("下载内容",new CheckBox{Text="下载正文（文本及 HTML）",Checked=source.DownloadBody});
        var downloadAttachments=Field("",new CheckBox{Text="下载附件（含云附件链接说明）",Checked=source.DownloadAttachments});
        var attachmentLimit=Number("单个附件上限（MB）",source.MaxAttachmentMb,1,500);
        Field("超限处理",new Label{AutoSize=true,Text="默认 20 MB；超过时仅记录文件名、大小和原因，不导出该文件，也不保存包含它的完整 EML。其他附件照常导出。当前需接收邮件后判断附件大小；若要避免接收整封大邮件，请同时设置主界面的邮件上限。"});
        var saveOriginal=Field("",new CheckBox{Text="保存原始邮件 EML（包含完整正文和随信附件）",Checked=source.SaveOriginal});
        Field("保存说明",new Label{AutoSize=true,Text="三项可独立选择；全部取消时仅保存发件人、时间等记录。若不想保存正文或附件的任何副本，也请取消原始邮件 EML。修改后需重新导出才能应用到历史邮件。"});
        var replies=Field("自动回复",new CheckBox{Text="完整匹配时回复；错误主题按统一策略回复",Checked=source.ReplyEnabled});
        var success=TextField("完整匹配回复正文",source.SuccessReply,false,3);
        var detect=Field("异常检查",new CheckBox{Text="检查同主题、不同发件邮箱的正文与附件差异",Checked=source.DetectAnomaly});
        Field("错误与停收策略",new Label{AutoSize=true,Text="错误统一回复："+Constants.ErrorReply+"。第 6 次错误通知联系管理员并停收，重置后恢复。"});
        var sample=TextField("测试主题（不发送邮件）","");
        var test=Field("",new Button{Text="测试识别",Height=32});
        static List<string> Split(string s)=>s.Replace('，',',').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct().ToList();
        MailRule Read()=>new(){Id=source.Id,Name=name.Text.Trim(),Mode=mode.Text,Keywords=Split(keywords.Text),MatchAll=matchAll.Checked,Prefix=prefix.Text,Separator=separator.Text,Fields=Split(fields.Text),
            KeyMode=keyMode.Text,SubjectKey=subjectKey.Text,Roster=roster,Output=output.Text.Trim(),DownloadBody=downloadBody.Checked,DownloadAttachments=downloadAttachments.Checked,SaveOriginal=saveOriginal.Checked,MaxAttachmentMb=(int)attachmentLimit.Value,ReplyEnabled=replies.Checked,SuccessReply=success.Text,DetectAnomaly=detect.Checked};
        test.Click+=(_,_)=>{try{var rule=Read();RuleValidator.Check(rule);var result=RuleValidator.Match(sample.Text,rule);MessageBox.Show(this,result switch{Validation.Success=>"完整匹配：下载并按设置回复",Validation.Ignore=>"未命中关键词：忽略，不回复",_=>"校验未通过：统一错误提示（内部原因："+result+"）"},"识别结果");}catch(Exception e){MessageBox.Show(this,e.Message,"请完善规则");}};
        SaveButton(()=>{var rule=Read();RuleValidator.Check(rule);Result=rule;});
    }
}
