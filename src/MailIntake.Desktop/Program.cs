namespace MailIntake.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Contains("--update")){Updater.Run();return;}
        bool smoke=args.Contains("--smoke-test");
        using var mutex=new Mutex(true,smoke?"Local\\MailIntakeSmoke":"Local\\KeywordMailDownloader",out bool created);
        if(!created){MessageBox.Show("邮件软件已运行，请从托盘打开；升级前请先退出旧版。","邮件接收管理");return;}
        try
        {
            if(smoke)SmokeChecks.Run();
            using var form=new MainForm(smoke);
            if(smoke)
            {
                AntdUI.Config.Animation=false;
                form.Show();Application.DoEvents();
                string? capture=Environment.GetEnvironmentVariable("MAILINTAKE_CAPTURE");
                void Capture(Form target,string suffix)
                {
                    Application.DoEvents();
                    if(string.IsNullOrEmpty(capture))return;
                    using var bitmap=new Bitmap(target.Width,target.Height);target.DrawToBitmap(bitmap,new Rectangle(Point.Empty,target.Size));
                    bitmap.Save(suffix==""?capture:Path.ChangeExtension(capture,suffix+".png"));
                }
                using var account=new AccountEditor();account.Show(form);Capture(account,"account");
                account.Sections!.SelectPage(1);Capture(account,"servers");account.Close();
                using var feedback=new FeedbackForm();feedback.Show(form);Capture(feedback,"feedback");feedback.Close();
                using var stars=new SupportForm(false);stars.Show(form);Capture(stars,"stars");stars.Close();
                using var sponsor=new SupportForm(true);sponsor.Show(form);Capture(sponsor,"sponsor");sponsor.Close();
                using var rule=new RuleEditor(LocalSettings.Load().Accounts[0].Rules[0]);rule.Show(form);
                for(int i=0;i<rule.Sections!.PageCount;i++){rule.Sections.SelectPage(i);Capture(rule,"rule"+i);}
                rule.Size=rule.MinimumSize;rule.Sections.SelectPage(1);Capture(rule,"rule-small");rule.Close();
                for(int i=0;i<form.Navigation.PageCount;i++){form.Navigation.SelectPage(i);Capture(form,"page"+i);}
                form.Navigation.SelectPage(0);Capture(form,"");form.Size=form.MinimumSize;Capture(form,"small");
                File.WriteAllText(Path.Combine(LocalSettings.Root,"smoke-result.txt"),"WINDOWS_FORMS_SMOKE_OK");return;
            }
            if(args.Contains("--background"))form.Shown+=(_,_)=>form.Hide();
            Application.Run(form);
        }
        catch(Exception error)
        {
            if(smoke){Directory.CreateDirectory(LocalSettings.Root);File.WriteAllText(Path.Combine(LocalSettings.Root,"smoke-result.txt"),error.ToString());}
            else MessageBox.Show("启动未完成："+error.GetType().Name+"。请保留本机配置并检查运行环境。","邮件接收管理");
            Environment.ExitCode=1;
        }
    }
}
