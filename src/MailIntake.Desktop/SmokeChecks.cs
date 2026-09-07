using System.Security.Cryptography;
using System.Text.Json;
using MailIntake.Core;
using Microsoft.Data.Sqlite;

namespace MailIntake.Desktop;

internal static class SmokeChecks
{
    public static void Run()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAILINTAKE_TEST_HOME")))throw new InvalidOperationException("Smoke tests require an isolated data directory.");
        using(var editor=new AccountEditor())
        {
            T Find<T>(string name) where T:Control=>(T)editor.Controls.Find(name,true).Single();
            var address=Find<AntdUI.Input>("accountAddress");var host=Find<AntdUI.Input>("incomingHost");var smtp=Find<AntdUI.Input>("smtpHost");var protocol=Find<ComboBox>("incomingProtocol");
            address.Text="test@163.com";
            if(host.Text!="imap.163.com"||smtp.Text!="smtp.163.com")throw new Exception("Provider IMAP defaults failed");
            protocol.SelectedItem="POP3";
            if(host.Text!="pop.163.com"||Find<NumericUpDown>("incomingPort").Value!=995)throw new Exception("Provider POP defaults failed");
            address.Text="test@unknown.example";
            if(host.Text!=""||smtp.Text!="")throw new Exception("Unknown provider kept stale defaults");
            address.Text="test@sina.cn";host.Text="custom.example";smtp.Text="outgoing.example";address.Text="test@gmail.com";
            if(host.Text!="custom.example"||smtp.Text!="outgoing.example")throw new Exception("Manual server edits were overwritten");
            UiLanguage.Change(true);UiLanguage.Apply(editor);
            if(editor.Text!="Add account"||address.Text!="test@gmail.com"||protocol.Text!="POP3")throw new Exception("Localization changed account data");
            UiLanguage.Change(false);UiLanguage.Apply(editor);
            if(editor.Text!="绑定邮箱"||host.Text!="custom.example")throw new Exception("Language round trip failed");
        }
        using(var editor=new AccountEditor(new(){Address="test@163.com",Host="private.example",SmtpHost="private-smtp.example"}))
        {
            ((AntdUI.Input)editor.Controls.Find("accountAddress",true).Single()).Text="test@qq.com";
            if(((AntdUI.Input)editor.Controls.Find("incomingHost",true).Single()).Text!="private.example")throw new Exception("Existing account overwritten");
        }
        string protectedValue=LocalSettings.Encrypt("synthetic-test-password");
        if(LocalSettings.Decrypt(protectedValue)!="synthetic-test-password")throw new Exception("DPAPI round-trip failed");
        string legacy=Path.Combine(LocalSettings.Root,"legacy");Directory.CreateDirectory(legacy);
        var config=new{accounts=new[]{new{account="teacher@example.test",host="imap.qq.com",port="993",smtp_host="smtp.qq.com",smtp_port="465",mailbox="INBOX",since="2026-08-07",secret=protectedValue,
            rules=new[]{new{name="工程实践 · 学号校验",mode="structured",prefix="工程实践",separator="-",fields=new[]{"姓名"},keywords=new[]{"工程实践"},key_mode="roster",roster=new Dictionary<string,string>{{"20260001","张三"}},output=Path.Combine(LocalSettings.Root,"downloads"),reply_enabled=true,success_reply="已收到，正文及附件已保存。"}}}}};
        string configPath=Path.Combine(legacy,"config.json");File.WriteAllText(configPath,JsonSerializer.Serialize(config));
        byte[] before=SHA256.HashData(File.ReadAllBytes(configPath));
        using(var db=new SqliteConnection($"Data Source={Path.Combine(legacy,"records.sqlite3")};Pooling=False"))
        {
            db.Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE sender_limits(sender TEXT,errors INTEGER,paused INTEGER); INSERT INTO sender_limits VALUES ('student@example.test',6,1); CREATE TABLE replies(id TEXT); INSERT INTO replies VALUES ('legacy-message');";c.ExecuteNonQuery();
        }
        var store=new StateStore(LocalSettings.Database);
        var accounts=LocalSettings.ImportPython(configPath,store);
        if(accounts.Count!=1||accounts[0].Rules[0].Roster["20260001"]!="张三"||!store.IsBlocked("student@example.test")||!store.IsHandled("legacy-message"))throw new Exception("Legacy migration failed");
        if(!before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(configPath))))throw new Exception("Legacy configuration was changed");
        LocalSettings.Save(new Settings{Accounts=accounts,RunOnLaunch=false});
        if(LocalSettings.Load().Accounts.Count!=1)throw new Exception("Configuration roundtrip failed");
        string updateSource=Path.Combine(LocalSettings.Root,"update-source"),updateTarget=Path.Combine(LocalSettings.Root,"update-target");
        Directory.CreateDirectory(updateSource);Directory.CreateDirectory(updateTarget);
        File.WriteAllText(Path.Combine(updateSource,"program.bin"),"new");
        File.WriteAllText(Path.Combine(updateTarget,"program.bin"),"old");
        File.WriteAllText(Path.Combine(updateTarget,"user-data.txt"),"preserve");
        Updater.Install(updateSource,updateTarget,["program.bin"]);
        if(File.ReadAllText(Path.Combine(updateTarget,"program.bin"))!="new"||File.ReadAllText(Path.Combine(updateTarget,"user-data.txt"))!="preserve")throw new Exception("Update preservation failed");
        File.WriteAllText(Path.Combine(updateSource,"program.bin"),"next");
        try{Updater.Install(updateSource,updateTarget,["program.bin","missing.bin"]);throw new Exception("Expected update failure");}
        catch(IOException){}
        if(File.ReadAllText(Path.Combine(updateTarget,"program.bin"))!="new"||File.ReadAllText(Path.Combine(updateTarget,"user-data.txt"))!="preserve")throw new Exception("Update rollback failed");
    }
}
