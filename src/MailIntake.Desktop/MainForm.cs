using System.Diagnostics;
using System.Text;
using MailIntake.Core;

namespace MailIntake.Desktop;

internal sealed class MainForm : Form
{
    private readonly PageDeck navigation;
    internal PageDeck Navigation=>navigation;
    private Settings settings;
    private Settings activeSettings;
    private readonly StateStore store;
    private readonly MailGateway gateway=new(LocalSettings.Decrypt);
    private readonly System.Windows.Forms.Timer timer=new(){Interval=1000};
    private readonly NotifyIcon tray=new(){Icon=SystemIcons.Information,Text="邮件接收管理",Visible=true};
    private readonly DataGridView accounts=Grid(), rules=Grid(), archives=Grid(), replies=Grid(), senders=Grid(), events=Grid();
    private readonly TextBox logs=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=Color.White,BorderStyle=BorderStyle.None};
    private readonly Label status=new(){Text="尚未开始 · 请先绑定邮箱并设置规则",AutoSize=true,ForeColor=Color.FromArgb(36,90,120),Padding=new Padding(8)};
    private readonly NumericUpDown interval=new(){Minimum=1,Maximum=1440,Width=75};
    private readonly NumericUpDown maxSize=new(){Minimum=1,Maximum=500,Width=75};
    private readonly NumericUpDown errorThreshold=new(){Minimum=1,Maximum=20,Width=75};
    private readonly NumericUpDown maxReplies=new(){Minimum=1,Maximum=10000,Width=75};
    private readonly CheckBox autoStart=new(){Text="登录 Windows 后自动运行",AutoSize=true};
    private readonly CheckBox reexport=new(){Text="忽略之前的记录，重新导出（仅本次，不回复）",AutoSize=true};
    private CancellationTokenSource? cancellation;
    private bool busy, running, exiting;
    private int cycleAnomalies;
    private DateTime next=DateTime.MinValue;
    private readonly bool smoke;
    private readonly EventWaitHandle updateExit=new(false,EventResetMode.AutoReset,"Local\\MailIntakeUpdateExit");
    public MainForm(bool smoke=false)
    {
        this.smoke=smoke; settings=LocalSettings.Load();activeSettings=settings.Snapshot();store=new(LocalSettings.Database);
        Text="邮件接收管理";Size=new Size(1240,880);MinimumSize=new Size(1100,760);
        StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Design.Background;
        var header=new Panel{Dock=DockStyle.Top,Height=62,Padding=new Padding(24,4,16,4),BackColor=Color.White};
        var title=Design.Heading("邮件接收管理",14);title.Dock=DockStyle.Fill;header.Controls.Add(title);
        var feedback=new ActionButton{Text="意见反馈",Dock=DockStyle.Right,Width=106,BorderWidth=0};
        feedback.Click+=(_,_)=>{using var form=new FeedbackForm();form.ShowDialog(this);};header.Controls.Add(feedback);title.BringToFront();
        var tabs=new PageDeck(true);navigation=tabs;
        var split=new SplitContainer{Size=new Size(1000,550),Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=230,Panel1MinSize=170,Panel2MinSize=180,SplitterWidth=12,BackColor=Design.Background};
        split.Panel1.Controls.Add(Design.Card("绑定邮箱 · 选中邮箱后管理其规则",accounts,Toolbar(("绑定邮箱",AddAccount),("编辑邮箱",EditAccount),("移除",RemoveAccount),("测试连接",TestAccount),("导入旧版配置",ImportLegacy))));
        split.Panel2.Controls.Add(Design.Card("收件规则 · 每组规则使用独立下载目录",rules,Toolbar(("新增规则",AddRule),("从模板新增",AddFromTemplate),("保存为模板",SaveRuleTemplate),("编辑规则",EditRule),("删除规则",RemoveRule),("上移",MoveRule),("打开下载目录",OpenRuleFolder))));
        tabs.AddPage("邮箱与规则",split);
        AddPage(tabs,"归档记录",archives,Toolbar(("刷新",RefreshRecords),("打开选中目录",OpenArchive),("导出 CSV",()=>ExportGrid(archives,"归档记录"))));
        AddPage(tabs,"回复记录",replies,Toolbar(("刷新",RefreshRecords),("导出 CSV",()=>ExportGrid(replies,"回复记录"))));
        AddPage(tabs,"停收管理",senders,Toolbar(("刷新",RefreshRecords),("重置并恢复接收",ResetSender)));
        AddPage(tabs,"异常与审计",events,Toolbar(("刷新",RefreshRecords),("查看详情",ShowEvent),("导出 CSV",()=>ExportGrid(events,"异常记录"))));
        AddPage(tabs,"运行日志",logs,null);
        errorThreshold.Value=Math.Clamp(settings.ErrorThreshold,1,20);interval.Value=settings.IntervalMinutes;maxSize.Value=settings.MaxMessageMb;maxReplies.Value=settings.MaxRepliesPerHour;autoStart.Checked=settings.AutoStart;
        var options=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(8,16,8,16)};
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220));options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        void Option(string label,Control control){int row=options.RowCount++;control.Margin=new Padding(4,12,4,12);options.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=Design.Ink},0,row);options.Controls.Add(control,1,row);}
        Option("自动检查间隔（分钟）",interval);Option("整封邮件上限（MB）",maxSize);Option("每小时最多自动回复（封）",maxReplies);Option("连续错误几次后停收",errorThreshold);Option("开机启动",autoStart);
        Option("历史邮件",reexport);
        Option("设置生效",new Label{AutoSize=true,MaximumSize=new Size(570,0),ForeColor=Design.Muted,Text="修改后点击下方“保存并开始”。重新导出仅用于下一次检查，不重复自动回复。邮件上限用于跳过整封大邮件；附件大小在各组规则中设置。"});
        AddPage(tabs,"运行设置",options,null);
        var bottom=new Panel{Dock=DockStyle.Bottom,Height=120,Padding=new Padding(20,4,12,4),BackColor=Color.White};
        status.AutoSize=false;status.Dock=DockStyle.Top;status.Height=46;
        var controls=Toolbar(("保存并开始",SaveStart),("立即检查",CheckNow),("暂停",Pause),("退出软件",ExitApp));controls.Dock=DockStyle.Bottom;
        bottom.Controls.Add(status);bottom.Controls.Add(controls);
        Controls.Add(tabs);Controls.Add(bottom);Controls.Add(header);
        accounts.SelectionChanged+=(_,_)=>RefreshRules();
        accounts.CellDoubleClick+=(_,_)=>EditAccount();rules.CellDoubleClick+=(_,_)=>EditRule();events.CellDoubleClick+=(_,_)=>ShowEvent();
        RefreshAccounts();RefreshRecords();
        var menu=new ContextMenuStrip();menu.Items.Add("显示窗口",null,(_,_)=>ShowWindow());menu.Items.Add("暂停检查",null,(_,_)=>Pause());menu.Items.Add("退出",null,(_,_)=>ExitApp());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>ShowWindow();
        FormClosing+=(_,e)=>{if(!exiting&&!smoke){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"邮件接收管理","已转到托盘运行。右键托盘图标可退出。",ToolTipIcon.Info);}};
        timer.Tick+=async(_,_)=>{if(updateExit.WaitOne(0)){ExitApp();return;}if(!exiting&&running&&!busy&&DateTime.Now>=next)await RunCycle();};
        if(!smoke){timer.Start();if(settings.RunOnLaunch){running=true;Log("已恢复保存的自动检查设置。");}}
    }
    private static DataGridView Grid()
    {
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AutoGenerateColumns=true,EnableHeadersVisualStyles=false,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,GridColor=Color.FromArgb(237,240,245),ColumnHeadersHeight=42,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing};
        grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(247,249,252),ForeColor=Design.Muted,Padding=new Padding(10,0,4,0),SelectionBackColor=Color.FromArgb(247,249,252),Font=new Font("Microsoft YaHei UI",9,FontStyle.Bold)};
        grid.DefaultCellStyle=new DataGridViewCellStyle{ForeColor=Design.Ink,SelectionBackColor=Color.FromArgb(231,241,255),SelectionForeColor=Color.FromArgb(22,90,190),Padding=new Padding(10,4,4,4),Font=new Font("Microsoft YaHei UI",9)};
        grid.RowTemplate.Height=42;return grid;
    }
    private FlowLayoutPanel Toolbar(params (string Label,Action Action)[] items)
    {
        var panel=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(0,6,0,8)};
        foreach(var item in items){var button=new ActionButton{Text=item.Label,Width=Math.Max(78,TextRenderer.MeasureText(item.Label,Font).Width+30),Height=36};if(item.Label is "保存并开始" or "绑定邮箱" or "新增规则")button.Type=AntdUI.TTypeMini.Primary;button.Click+=(_,_)=>{try{item.Action();}catch(Exception e){MessageBox.Show(this,e.Message,"操作未完成");}};panel.Controls.Add(button);}return panel;
    }
    private static void AddPage(PageDeck tabs,string name,Control content,Control? tools)
    {tabs.AddPage(name,Design.Card(name,content,tools));}
    private int AccountIndex=>accounts.CurrentRow?.Index??-1;
    private int RuleIndex=>rules.CurrentRow?.Index??-1;
    private MailAccount? SelectedAccount=>AccountIndex>=0&&AccountIndex<settings.Accounts.Count?settings.Accounts[AccountIndex]:null;
    private MailRule? SelectedRule=>SelectedAccount is {} a&&RuleIndex>=0&&RuleIndex<a.Rules.Count?a.Rules[RuleIndex]:null;
    private void RefreshAccounts(int selected=0)
    {
        accounts.DataSource=settings.Accounts.Select(a=>new{邮箱=a.Address,协议=a.Protocol,服务器=a.Host,状态=a.Enabled?"启用":"停用",规则数=a.Rules.Count}).ToList();
        if(accounts.Rows.Count>0)accounts.CurrentCell=accounts.Rows[Math.Clamp(selected,0,accounts.Rows.Count-1)].Cells[0];RefreshRules();
    }
    private void RefreshRules()=>rules.DataSource=SelectedAccount?.Rules.Select(r=>new{名称=r.Name,模式=r.Mode,固定开头=r.Prefix,下载目录=r.Output,自动回复=r.ReplyEnabled?"启用":"关闭"}).ToList();
    private void AddAccount(){using var form=new AccountEditor();if(form.ShowDialog(this)!=DialogResult.OK)return;CheckDuplicate(form.Result,-1);settings.Accounts.Add(form.Result);RefreshAccounts(settings.Accounts.Count-1);}
    private void EditAccount(){if(SelectedAccount is not {} a)return;int i=AccountIndex;using var form=new AccountEditor(a);if(form.ShowDialog(this)!=DialogResult.OK)return;CheckDuplicate(form.Result,i);settings.Accounts[i]=form.Result;RefreshAccounts(i);}
    private void CheckDuplicate(MailAccount a,int except){if(settings.Accounts.Where((_,i)=>i!=except).Any(x=>x.Address.Equals(a.Address,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("该邮箱已绑定。");}
    private void RemoveAccount(){if(SelectedAccount is null)return;settings.Accounts.RemoveAt(AccountIndex);RefreshAccounts();}
    private void AddRule(){if(SelectedAccount is not {} a){MessageBox.Show(this,"请先绑定并选中邮箱。");return;}using var form=new RuleEditor();if(form.ShowDialog(this)!=DialogResult.OK)return;a.Rules.Add(form.Result);RefreshRules();}
    private void SaveRuleTemplate(){if(SelectedRule is not {} rule){MessageBox.Show(this,"请先选中一条规则。");return;}TemplateFiles.Save(this,rule);}
    private void AddFromTemplate()
    {
        if(SelectedAccount is not {} account){MessageBox.Show(this,"请先绑定并选中邮箱。");return;}
        var rule=TemplateFiles.Load(this);if(rule is null)return;
        using var editor=new RuleEditor(rule,true);
        if(editor.ShowDialog(this)!=DialogResult.OK)return;
        account.Rules.Add(editor.Result);RefreshRules();Log("已从模板创建独立规则，请点击“保存并开始”应用。原模板保持不变。");
    }
    private void EditRule(){if(SelectedAccount is not {} a||SelectedRule is not {} r)return;int i=RuleIndex;using var form=new RuleEditor(r);if(form.ShowDialog(this)!=DialogResult.OK)return;a.Rules[i]=form.Result;RefreshRules();}
    private void RemoveRule(){if(SelectedAccount is not {} a||SelectedRule is null)return;a.Rules.RemoveAt(RuleIndex);RefreshRules();}
    private void MoveRule(){if(SelectedAccount is not {} a||RuleIndex<1)return;int i=RuleIndex;(a.Rules[i-1],a.Rules[i])=(a.Rules[i],a.Rules[i-1]);RefreshRules();rules.CurrentCell=rules.Rows[i-1].Cells[0];}
    private static void OpenDirectory(string path){if(Directory.Exists(path))Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
    private void OpenRuleFolder(){if(SelectedRule is {} r){Directory.CreateDirectory(r.Output);OpenDirectory(r.Output);}}
    private void OpenArchive(){if(archives.CurrentRow?.Cells["目录"].Value is string path)OpenDirectory(path);}
    private async void TestAccount()
    {
        if(SelectedAccount is not {} a)return;try{status.Text="正在测试接收与发送登录（不发信）…";using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));await gateway.TestAsync(a,timeout.Token);Log(a.Address+"：收发登录测试通过，未发送邮件。");}catch(Exception e){Log("连接测试失败："+e.GetType().Name+"。请核对授权码、服务器与网络。");}
    }
    private void ImportLegacy()
    {
        if(busy||running){MessageBox.Show(this,"请先暂停检查后导入。");return;}
        using var picker=new OpenFileDialog{Filter="旧版配置|config.json",InitialDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KeywordMailDownloader")};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        var added=LocalSettings.ImportPython(picker.FileName,store);
        int count=0;foreach(var a in added){if(settings.Accounts.Any(x=>x.Address.Equals(a.Address,StringComparison.OrdinalIgnoreCase)))continue;settings.Accounts.Add(a);count++;}
        RefreshAccounts();RefreshRecords();Log($"导入 {count} 个邮箱，旧去重与停收状态已保留。请检查规则和日期，再保存开始。");
    }
    private void SaveStart()
    {
        if(settings.Accounts.Count==0)throw new ArgumentException("请先绑定邮箱。");
        foreach(var a in settings.Accounts)foreach(var r in a.Rules)RuleValidator.Check(r);
        settings.ErrorThreshold=(int)errorThreshold.Value;settings.IntervalMinutes=(int)interval.Value;settings.MaxMessageMb=(int)maxSize.Value;settings.MaxRepliesPerHour=(int)maxReplies.Value;settings.AutoStart=autoStart.Checked;settings.RunOnLaunch=true;
        LocalSettings.Save(settings);LocalSettings.Startup(settings.AutoStart);activeSettings=settings.Snapshot();requestedReexport=reexport.Checked;reexport.Checked=false;running=true;next=DateTime.MinValue;Log(requestedReexport?"本次重新导出：按开始日期扫描，不重复回复，停收限制仍有效。":"已保存并开始。开始日期之后的历史匹配邮件也会处理和回复。");
    }
    private void CheckNow(){if(busy){MessageBox.Show(this,"请等待本轮结束后再检查或重新导出。");return;}if(!settings.RunOnLaunch){MessageBox.Show(this,"请先保存设置并开始。");return;}requestedReexport=reexport.Checked;reexport.Checked=false;running=true;next=DateTime.MinValue;}
    private bool requestedReexport;
    private void Pause(){running=false;cancellation?.Cancel();settings.RunOnLaunch=false;activeSettings.RunOnLaunch=false;LocalSettings.Save(activeSettings);Log("已暂停。已进入发送阶段的结果可能需人工核实。");}
    private async Task RunCycle()
    {
        busy=true;cycleAnomalies=0;cancellation=new();var snapshot=activeSettings.Snapshot();int processed=0;
        bool exportAgain=requestedReexport;requestedReexport=false;
        try
        {
            var engine=new IntakeEngine(store,gateway);
            foreach(var account in snapshot.Accounts.Where(a=>a.Enabled&&a.Rules.Count>0))
            {
                if(cancellation.IsCancellationRequested)break;
                try
                {
                    await foreach(var incoming in gateway.Scan(account,cancellation.Token))
                    {
                        if(await engine.ProcessAsync(account,incoming,snapshot,Log,cancellation.Token,exportAgain))processed++;
                        if(!exportAgain&&processed>=snapshot.MaxPerCycle)break;
                    }
                }
                catch(OperationCanceledException){break;}
                catch(Exception e){store.Event("连接或处理失败","",account.Address,e.GetType().Name+"；请检查配置、网络及回复记录。");Log("异常："+account.Address+"："+e.GetType().Name+"，其他邮箱将继续检查。");}
                if(!exportAgain&&processed>=snapshot.MaxPerCycle)break;
            }
        }
        finally
        {
            busy=false;cancellation.Dispose();cancellation=null;next=DateTime.Now.AddMinutes(snapshot.IntervalMinutes);RefreshRecords();
            Log($"本轮处理 {processed} 封；"+(running?"下一次 "+next.ToString("HH:mm:ss"):"已暂停"));
            if(cycleAnomalies>0)
            {
                string summary=$"本轮检查发现 {cycleAnomalies} 条异常提示，请到“异常与审计”查看详情。";
                Log(summary);
                if(!exiting&&!smoke)tray.ShowBalloonTip(5000,"邮件接收管理 · 异常汇总",summary,ToolTipIcon.Warning);
            }
            cycleAnomalies=0;
        }
    }
    private void RefreshRecords()
    {
        archives.DataSource=store.Archives().Select(a=>new{收件邮箱=a.Account,发件邮箱=a.Sender,主题=a.Subject,规则=a.Rule,接收时间=a.ReceivedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")??"未披露（POP3）",保存时间=a.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),目录=a.Directory}).ToList();
        replies.DataSource=store.Replies().Select(r=>new{时间=r.Time,收件邮箱=r.Account,发件邮箱=r.Sender,类型=r.Kind,状态=r.Status switch{"Sent"=>"服务器已接受","Sending"=>"发送中或中断待核实","Uncertain"=>"结果不确定","RateLimited"=>"达到回复限额",_=>r.Status}}).ToList();
        senders.DataSource=store.Senders().Select(s=>new{发件邮箱=s.Sender,连续错误次数=s.Errors,接收状态=s.Blocked?"已停收":"正常",更新时间=s.UpdatedAt}).ToList();
        events.DataSource=store.Events().Select(e=>new{时间=e.Time,类型=e.Kind,发件邮箱=e.Sender,收件邮箱=e.Account,详情=e.Detail}).ToList();
    }
    private void ResetSender()
    {
        if(busy){MessageBox.Show(this,"请先暂停并等待本轮处理结束，再重置。");return;}
        if(senders.CurrentRow?.Cells["发件邮箱"].Value is not string from)return;
        store.Reset(from);RefreshRecords();Log("管理员已恢复 "+from+"，请对方重新发送邮件。");
    }
    private void ShowEvent(){if(events.CurrentRow?.Cells["详情"].Value is string detail)MessageBox.Show(this,detail,"事件详情");}
    private void ExportGrid(DataGridView grid,string name)
    {
        using var picker=new SaveFileDialog{Filter="CSV|*.csv",FileName=name+".csv"};if(picker.ShowDialog(this)!=DialogResult.OK)return;
        static string Cell(object? value){string s=value?.ToString()??"";if(s.TrimStart().StartsWith('=')||s.TrimStart().StartsWith('+')||s.TrimStart().StartsWith('-')||s.TrimStart().StartsWith('@'))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
        using var writer=new StreamWriter(picker.FileName,false,new UTF8Encoding(true));writer.WriteLine(string.Join(',',grid.Columns.Cast<DataGridViewColumn>().Select(c=>Cell(c.HeaderText))));
        foreach(DataGridViewRow row in grid.Rows)writer.WriteLine(string.Join(',',row.Cells.Cast<DataGridViewCell>().Select(c=>Cell(c.Value))));
        Log("记录已导出。");
    }
    private void Log(string text)
    {
        if(IsDisposed)return;
        if(InvokeRequired){BeginInvoke(()=>Log(text));return;}
        status.Text=text;logs.AppendText(DateTime.Now.ToString("HH:mm:ss ")+text+Environment.NewLine);
        if(logs.TextLength>100000)logs.Text=logs.Text[^80000..];
        if(text.StartsWith("异常：")){if(busy)cycleAnomalies++;Text="【有异常】邮件接收管理 · MailKit";}
    }
    public void ShowWindow(){Show();WindowState=FormWindowState.Normal;Activate();}
    private async void ExitApp()
    {
        if(exiting)return;
        exiting=true;running=false;timer.Stop();cancellation?.Cancel();
        Enabled=false;status.Text="正在等待当前邮件处理结束，以便安全退出…";
        while(busy)await Task.Delay(100);
        tray.Visible=false;Close();
    }
    protected override void Dispose(bool disposing){if(disposing){tray.Dispose();timer.Dispose();updateExit.Dispose();cancellation?.Cancel();}base.Dispose(disposing);}
}
