using System.Diagnostics;
using System.Text;
using MailIntake.Core;

namespace MailIntake.Desktop;

internal sealed class MainForm : Form
{
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
    private readonly NumericUpDown maxReplies=new(){Minimum=1,Maximum=10000,Width=75};
    private readonly CheckBox autoStart=new(){Text="登录 Windows 后自动运行",AutoSize=true};
    private CancellationTokenSource? cancellation;
    private bool busy, running, exiting;
    private DateTime next=DateTime.MinValue;
    private readonly bool smoke;
    public MainForm(bool smoke=false)
    {
        this.smoke=smoke; settings=LocalSettings.Load();activeSettings=settings.Snapshot();store=new(LocalSettings.Database);
        Text="邮件接收管理 · MailKit";Size=new Size(1150,850);MinimumSize=new Size(960,700);
        StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(245,247,250);
        var header=new Panel{Dock=DockStyle.Top,Height=85,Padding=new Padding(20,12,20,8),BackColor=Color.FromArgb(25,48,75)};
        header.Controls.Add(new Label{Text="邮件接收管理",Dock=DockStyle.Top,Height=35,Font=new Font(Font.FontFamily,19,FontStyle.Bold),ForeColor=Color.White});
        header.Controls.Add(new Label{Text="多邮箱  /  规则归档  /  主题校验  /  异常停收",Dock=DockStyle.Bottom,Height=25,ForeColor=Color.FromArgb(189,212,232)});
        var tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(20,9)};
        var setup=new TabPage("邮箱与规则"){Padding=new Padding(12)};
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=220};
        split.Panel1.Controls.Add(accounts);split.Panel1.Controls.Add(Toolbar(("绑定邮箱",AddAccount),("编辑",EditAccount),("移除",RemoveAccount),("测试连接（不发信）",TestAccount),("导入旧版配置",ImportLegacy)));
        split.Panel2.Controls.Add(rules);split.Panel2.Controls.Add(Toolbar(("新增规则",AddRule),("编辑规则",EditRule),("删除规则",RemoveRule),("上移",MoveRule),("打开下载目录",OpenRuleFolder)));
        setup.Controls.Add(split);tabs.TabPages.Add(setup);
        AddPage(tabs,"归档记录",archives,Toolbar(("刷新",RefreshRecords),("打开选中目录",OpenArchive),("导出 CSV",()=>ExportGrid(archives,"归档记录"))));
        AddPage(tabs,"回复记录",replies,Toolbar(("刷新",RefreshRecords),("导出 CSV",()=>ExportGrid(replies,"回复记录"))));
        AddPage(tabs,"管理员 · 停收与重置",senders,Toolbar(("刷新",RefreshRecords),("重置选中发件邮箱并恢复接收",ResetSender)));
        AddPage(tabs,"异常与审计",events,Toolbar(("刷新",RefreshRecords),("查看详情",ShowEvent),("导出 CSV",()=>ExportGrid(events,"异常记录"))));
        AddPage(tabs,"运行日志",logs,null);
        var bottom=new Panel{Dock=DockStyle.Bottom,Height=145,Padding=new Padding(12,6,12,8)};
        interval.Value=settings.IntervalMinutes;maxSize.Value=settings.MaxMessageMb;maxReplies.Value=settings.MaxRepliesPerHour;autoStart.Checked=settings.AutoStart;
        var options=new FlowLayoutPanel{Dock=DockStyle.Top,Height=36};
        options.Controls.AddRange([new Label{Text="间隔(分钟)",AutoSize=true,Padding=new Padding(0,5,0,0)},interval,new Label{Text="邮件上限(MB)",AutoSize=true,Padding=new Padding(9,5,0,0)},maxSize,new Label{Text="每小时回复上限",AutoSize=true,Padding=new Padding(9,5,0,0)},maxReplies,autoStart]);
        var controls=Toolbar(("保存并开始",SaveStart),("立即检查",CheckNow),("暂停",Pause),("退出软件",ExitApp));controls.Dock=DockStyle.Bottom;
        bottom.Controls.Add(status);status.Dock=DockStyle.Fill;bottom.Controls.Add(options);bottom.Controls.Add(controls);
        Controls.Add(tabs);Controls.Add(bottom);Controls.Add(header);
        accounts.SelectionChanged+=(_,_)=>RefreshRules();
        accounts.CellDoubleClick+=(_,_)=>EditAccount();rules.CellDoubleClick+=(_,_)=>EditRule();events.CellDoubleClick+=(_,_)=>ShowEvent();
        RefreshAccounts();RefreshRecords();
        var menu=new ContextMenuStrip();menu.Items.Add("显示窗口",null,(_,_)=>ShowWindow());menu.Items.Add("暂停检查",null,(_,_)=>Pause());menu.Items.Add("退出",null,(_,_)=>ExitApp());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>ShowWindow();
        FormClosing+=(_,e)=>{if(!exiting&&!smoke){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"邮件接收管理","已转到托盘运行。右键托盘图标可退出。",ToolTipIcon.Info);}};
        timer.Tick+=async(_,_)=>{if(running&&!busy&&DateTime.Now>=next)await RunCycle();};
        if(!smoke){timer.Start();if(settings.RunOnLaunch){running=true;Log("已恢复保存的自动检查设置。");}}
    }
    private static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AutoGenerateColumns=true};
    private FlowLayoutPanel Toolbar(params (string Label,Action Action)[] items)
    {
        var panel=new FlowLayoutPanel{Dock=DockStyle.Top,Height=43,Padding=new Padding(0,4,0,4)};
        foreach(var item in items){var button=new Button{Text=item.Label,AutoSize=true,Height=32,Padding=new Padding(8,0,8,0)};button.Click+=(_,_)=>{try{item.Action();}catch(Exception e){MessageBox.Show(this,e.Message,"操作未完成");}};panel.Controls.Add(button);}return panel;
    }
    private static void AddPage(TabControl tabs,string name,Control content,Control? tools)
    {var page=new TabPage(name){Padding=new Padding(12)};page.Controls.Add(content);if(tools!=null)page.Controls.Add(tools);tabs.TabPages.Add(page);}
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
        settings.IntervalMinutes=(int)interval.Value;settings.MaxMessageMb=(int)maxSize.Value;settings.MaxRepliesPerHour=(int)maxReplies.Value;settings.AutoStart=autoStart.Checked;settings.RunOnLaunch=true;
        LocalSettings.Save(settings);LocalSettings.Startup(settings.AutoStart);activeSettings=settings.Snapshot();running=true;next=DateTime.MinValue;Log("已保存并开始。开始日期之后的历史匹配邮件也会处理和回复。");
    }
    private void CheckNow(){if(!settings.RunOnLaunch){MessageBox.Show(this,"请先保存设置并开始。");return;}running=true;next=DateTime.MinValue;}
    private void Pause(){running=false;cancellation?.Cancel();settings.RunOnLaunch=false;activeSettings.RunOnLaunch=false;LocalSettings.Save(activeSettings);Log("已暂停。已进入发送阶段的结果可能需人工核实。");}
    private async Task RunCycle()
    {
        busy=true;cancellation=new();var snapshot=activeSettings.Snapshot();int processed=0;
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
                        if(await engine.ProcessAsync(account,incoming,snapshot,Log,cancellation.Token))processed++;
                        if(processed>=snapshot.MaxPerCycle)break;
                    }
                }
                catch(OperationCanceledException){break;}
                catch(Exception e){store.Event("连接或处理失败","",account.Address,e.GetType().Name+"；请检查配置、网络及回复记录。");Log(account.Address+"："+e.GetType().Name+"，其他邮箱将继续检查。");}
                if(processed>=snapshot.MaxPerCycle)break;
            }
        }
        finally{busy=false;cancellation.Dispose();cancellation=null;next=DateTime.Now.AddMinutes(snapshot.IntervalMinutes);RefreshRecords();Log($"本轮处理 {processed} 封；"+(running?"下一次 "+next.ToString("HH:mm:ss"):"已暂停"));}
    }
    private void RefreshRecords()
    {
        archives.DataSource=store.Archives().Select(a=>new{收件邮箱=a.Account,发件邮箱=a.Sender,主题=a.Subject,规则=a.Rule,接收时间=a.ReceivedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")??"未披露（POP3）",保存时间=a.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),目录=a.Directory}).ToList();
        replies.DataSource=store.Replies().Select(r=>new{时间=r.Time,收件邮箱=r.Account,发件邮箱=r.Sender,类型=r.Kind,状态=r.Status switch{"Sent"=>"服务器已接受","Sending"=>"发送中或中断待核实","Uncertain"=>"结果不确定","RateLimited"=>"达到回复限额",_=>r.Status}}).ToList();
        senders.DataSource=store.Senders().Select(s=>new{发件邮箱=s.Sender,错误次数=s.Errors,接收状态=s.Blocked?"已停收":"正常",更新时间=s.UpdatedAt}).ToList();
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
        if(text.StartsWith("异常：")){tray.ShowBalloonTip(4000,"邮件接收管理 · 异常提醒",text,ToolTipIcon.Warning);Text="【有异常】邮件接收管理 · MailKit";}
    }
    public void ShowWindow(){Show();WindowState=FormWindowState.Normal;Activate();}
    private void ExitApp(){exiting=true;running=false;cancellation?.Cancel();timer.Stop();tray.Visible=false;Close();}
    protected override void Dispose(bool disposing){if(disposing){tray.Dispose();timer.Dispose();cancellation?.Cancel();}base.Dispose(disposing);}
}
