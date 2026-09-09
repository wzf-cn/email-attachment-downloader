using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailIntake.Core;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;

namespace MailIntake.Desktop;

internal static class LocalSettings
{
    public static string Root => Environment.GetEnvironmentVariable("MAILINTAKE_TEST_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MailIntake");
    public static string Config => Path.Combine(Root,"settings.json");
    public static string Database => Path.Combine(Root,"records.sqlite3");
    public static string Encrypt(string plain) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain),null,DataProtectionScope.CurrentUser));
    public static string Decrypt(string encrypted) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted),null,DataProtectionScope.CurrentUser));
    public static Settings Load()
    {
        if(!File.Exists(Config))return new();
        string json=File.ReadAllText(Config);var settings=JsonSerializer.Deserialize<Settings>(json)??new();
        using var doc=JsonDocument.Parse(json);
        if(doc.RootElement.TryGetProperty("Accounts",out var accounts))
            for(int a=0;a<settings.Accounts.Count;a++)
                if(accounts[a].TryGetProperty("Rules",out var rules))
                    for(int r=0;r<settings.Accounts[a].Rules.Count;r++)
                        if(!rules[r].TryGetProperty("IntervalMinutes",out _))settings.Accounts[a].Rules[r].IntervalMinutes=Math.Clamp(settings.IntervalMinutes,1,1440);
        foreach(var account in settings.Accounts)
            foreach(var rule in account.Rules)UseKeywords(rule);
        if(settings.Version<2){if(settings.MaxMessageMb==30)settings.MaxMessageMb=150;settings.Version=2;}
        return settings;
    }
    private static void UseKeywords(MailRule rule)
    {
        rule.Mode="关键词";
        if(rule.Keywords.Count==0&&!string.IsNullOrWhiteSpace(rule.Prefix))rule.Keywords=[rule.Prefix];
    }
    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Root);
        if(File.Exists(Config)) File.Copy(Config,Path.Combine(Root,"settings-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json"));
        File.WriteAllText(Config+".tmp",JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(Config+".tmp",Config,true);
    }
    public static void Startup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(enabled) key.SetValue("KeywordMailDownloader",$"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue("KeywordMailDownloader",false);
    }
    public static List<MailAccount> ImportPython(string path,StateStore store)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        var root=document.RootElement;
        var items=root.TryGetProperty("accounts",out var collection)?collection.EnumerateArray().ToList():[root];
        static string S(JsonElement obj,string name,string fallback="") => obj.TryGetProperty(name,out var p)?p.ValueKind==JsonValueKind.String?p.GetString()??fallback:p.ToString():fallback;
        static bool B(JsonElement obj,string name,bool fallback=false) => obj.TryGetProperty(name,out var p)?p.ValueKind==JsonValueKind.True:fallback;
        static List<string> L(JsonElement obj,string name) => obj.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.Array?p.EnumerateArray().Select(x=>x.GetString()??"").ToList():[];
        var result=new List<MailAccount>();
        foreach(var old in items)
        {
            var account=new MailAccount{Address=S(old,"account"),Host=S(old,"host","imap.qq.com"),Port=int.Parse(S(old,"port","993")),
                SmtpHost=S(old,"smtp_host","smtp.qq.com"),SmtpPort=int.Parse(S(old,"smtp_port","465")),Folder=S(old,"mailbox","INBOX"),
                Since=DateTime.Parse(S(old,"since",DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"))),EncryptedPassword=S(old,"secret")};
            if(!MailboxValid(account.Address)) throw new ArgumentException("旧配置中邮箱地址无效。");
            // Confirm the current Windows account can decrypt, without displaying the secret.
            _=Decrypt(account.EncryptedPassword);
            if(old.TryGetProperty("rules",out var rules)) foreach(var r in rules.EnumerateArray())
            {
                var rule=new MailRule{Name=S(r,"name","导入规则"),Mode="关键词",Prefix=S(r,"prefix"),
                    Separator=S(r,"separator","-"),Fields=L(r,"fields"),Keywords=L(r,"keywords"),KeyMode=S(r,"key_mode","fixed")=="roster"?"名单":"固定秘钥",
                    SubjectKey=S(r,"subject_key"),Output=S(r,"output"),ReplyEnabled=B(r,"reply_enabled"),SuccessReply=S(r,"success_reply","已收到"),
                    MatchAll=B(r,"match_all",B(root,"match_all")),DetectAnomaly=B(r,"anomaly_enabled",true)};
                if(r.TryGetProperty("roster",out var roster)) foreach(var entry in roster.EnumerateObject()) rule.Roster[entry.Name]=entry.Value.GetString()??"";
                UseKeywords(rule); RuleValidator.Check(rule); account.Rules.Add(rule);
            }
            result.Add(account);
        }
        // Preserve old stop-list and handled identities so migration doesn't resend notifications.
        string oldDb=Path.Combine(Path.GetDirectoryName(path)!,"records.sqlite3");
        if(File.Exists(oldDb))
        {
            using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=oldDb,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()); db.Open();
            bool Has(string table) { using var c=db.CreateCommand();c.CommandText="SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n";c.Parameters.AddWithValue("$n",table);return c.ExecuteScalar()!=null; }
            if(Has("sender_limits"))
            {
                using var c=db.CreateCommand();c.CommandText="SELECT sender,errors,paused FROM sender_limits";using var reader=c.ExecuteReader();
                while(reader.Read()) store.ImportSender(reader.GetString(0),reader.GetInt32(1),reader.GetInt32(2)==1);
            }
            foreach(string table in new[]{"replies","invalid_messages","blocked_messages"}) if(Has(table))
            {
                using var c=db.CreateCommand();c.CommandText=$"SELECT id FROM {table}"; using var reader=c.ExecuteReader();
                while(reader.Read()) store.Mark(reader.GetString(0),"ImportedLegacy","","");
            }
            if(Has("messages"))
            {
                using var c=db.CreateCommand();c.CommandText="SELECT metadata FROM messages";using var reader=c.ExecuteReader();
                while(reader.Read())
                {
                    using var meta=JsonDocument.Parse(reader.GetString(0));var m=meta.RootElement;
                    var account=result.FirstOrDefault(a=>a.Address==S(m,"account"));if(account==null)continue;
                    string id=Constants.Hash($"{account.Host}|{account.Address}|{account.Folder}|{S(m,"uidvalidity")}|{S(m,"uid")}");
                    store.Mark(id,"ImportedLegacy",S(m,"From"),account.Address);
                }
            }
        }
        store.Event("迁移","","","导入 Python 配置及旧去重/停收状态；原文件保持不变。旧归档继续保留在原目录。");
        return result;
    }
    public static bool MailboxValid(string address) => MimeKit.MailboxAddress.TryParse(address,out var value)&&value.Address.Contains('@');
}
