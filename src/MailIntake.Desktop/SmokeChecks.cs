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
    }
}
