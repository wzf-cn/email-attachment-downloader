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
                form.Show();Application.DoEvents();
                using var account=new AccountEditor();account.Show(form);Application.DoEvents();account.Close();
                string? capture=Environment.GetEnvironmentVariable("MAILINTAKE_CAPTURE");
                using var rule=new RuleEditor(LocalSettings.Load().Accounts[0].Rules[0]);rule.Show(form);Application.DoEvents();
                if(!string.IsNullOrEmpty(capture)){using var bitmap=new Bitmap(rule.Width,rule.Height);rule.DrawToBitmap(bitmap,new Rectangle(Point.Empty,rule.Size));bitmap.Save(Path.ChangeExtension(capture,"rule.png"));}
                rule.Close();
                if(!string.IsNullOrEmpty(capture)){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(capture);}
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
