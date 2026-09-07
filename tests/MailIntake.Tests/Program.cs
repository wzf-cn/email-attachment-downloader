using MailIntake.Core;
using MimeKit;
using System.Text;

if(args.Length==2&&args[0]=="--summarize-links"){CloudAttachmentSummary.Write(args[1]);Console.WriteLine("CLOUD_SUMMARY_OK");return;}
if(args.Length==3&&args[0]=="--repair-export")
{
    int count=0,attachments=0,cloud=0;
    foreach(string path in Directory.GetFiles(args[1],"original.eml",SearchOption.AllDirectories))
    {
        var message=await MimeMessage.LoadAsync(path);
        string metaPath=Path.Combine(Path.GetDirectoryName(path)!,"metadata.json");
        var old=System.Text.Json.JsonSerializer.Deserialize<ArchiveRecord>(File.ReadAllText(metaPath))!;
        string id=Constants.Hash(path);
        var incoming=new Incoming(id,message.Subject??"无主题",old.Sender,old.ReceivedAt,new FileInfo(path).Length,message,_=>Task.FromResult(message));
        var record=await IntakeEngine.ArchiveAsync(new(){Address=old.Account},incoming,message,new(){Name=old.Rule,Output=args[2]},old.Id,CancellationToken.None);
        count++;attachments+=record.Attachments.Count;if(AttachmentExport.CloudLinks(message).Count>0)cloud++;
    }
    CloudAttachmentSummary.Write(args[2]);
    Console.WriteLine($"LOCAL_EXPORT_OK messages={count} attachments={attachments} cloudLinkMessages={cloud}");return;
}
var suite=new Suite();await suite.Run();

sealed class FakeSender : IReplySender
{
    public List<(string To,string Body,string Account)> Sent=[];
    public bool Fail;
    public Task SendAsync(MailAccount account,Incoming incoming,string target,string body,string id,CancellationToken token)
    { Sent.Add((target,body,account.Address));if(Fail)throw new IOException("Synthetic send interruption");return Task.CompletedTask; }
}

sealed class Fixture
{
    public string Root=Path.Combine(Path.GetTempPath(),"MailIntakeTests-"+Guid.NewGuid().ToString("N"));
    public StateStore Store;
    public FakeSender Sender=new();
    public Settings Settings=new();
    public MailAccount Account;
    public MailRule Rule;
    public IntakeEngine Engine;
    public int Loads;
    public Fixture()
    {
        Directory.CreateDirectory(Root);Store=new(Path.Combine(Root,"test.sqlite3"));
        Rule=new(){Name="工程实践",Prefix="工程实践",Fields=["姓名"],Keywords=["工程实践","实践提交"],KeyMode="名单",Roster=new(){{"00123","张三"}},Output=Path.Combine(Root,"downloads")};
        Account=new(){Address="receiver@example.test",Rules=[Rule]};Settings.Accounts.Add(Account);Engine=new(Store,Sender);
    }
    public Incoming Mail(string id,string subject="工程实践-张三-00123",string from="student@example.test",string text="这是工程实践提交。",bool attach=false)
    {
        var message=new MimeMessage();message.From.Add(MailboxAddress.Parse(from));message.To.Add(MailboxAddress.Parse(Account.Address));message.Subject=subject;
        var builder=new BodyBuilder{TextBody=text};if(attach)builder.Attachments.Add("../附件.txt",Encoding.UTF8.GetBytes("附件测试"));message.Body=builder.ToMessageBody();
        return new(Constants.Hash(id),subject,from,DateTimeOffset.Now,100,message,ct=>{Loads++;return Task.FromResult(message);});
    }
    public Task<bool> Process(Incoming mail)=>Engine.ProcessAsync(Account,mail,Settings,_=>{},CancellationToken.None);
}

sealed class Suite
{
    int passed,failed;
    static void Eq<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
    async Task Test(string name,Func<Task> body)
    {try{await body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
    public async Task Run()
    {
        await Test("provider detection uses exact domains and secure protocol defaults",()=>
        {
            Eq("QQ",MailProviders.Find(" user@FOXMAIL.COM ")!.Name);
            Eq(true,MailProviders.Find("user@qq.com.evil.example") is null);
            Eq(true,MailProviders.Find("user@@qq.com") is null);
            Eq(true,MailProviders.Find("user@example.org") is null);
            Eq("pop3.aliyun.com",MailProviders.Find("user@aliyun.com")!.Incoming("POP3").Host);
            Eq(993,MailProviders.Find("user@sina.cn")!.Incoming("IMAP").Port);
            Eq(true,MailProviders.Find("user@outlook.com")!.RequiresOAuth);
            Eq("STARTTLS",MailProviders.Find("user@outlook.com")!.SmtpSecurity);
            var f=new Fixture();f.Rule.Fields=["Name"];f.Rule.Roster["00123"]="Alice";
            Eq(Validation.Structure,RuleValidator.Match(f.Rule.Prefix+"-Bob-00123",f.Rule));
            Eq(Validation.Success,RuleValidator.Match(f.Rule.Prefix+"-Alice-00123",f.Rule));
            return Task.CompletedTask;
        });
        await Test("rule templates preserve reusable settings and create independent task rules",()=>
        {
            var f=new Fixture();f.Rule.FlatAttachments=true;f.Rule.DownloadBody=false;f.Rule.MaxAttachmentMb=7;f.Rule.SubjectKey="private-test-key";
            string path=Path.Combine(f.Root,"template.mailrule.json");RuleTemplates.Save(path,f.Rule);
            string saved=File.ReadAllText(path);Eq(false,saved.Contains("private-test-key"));Eq(false,saved.Contains("00123"));
            var a=RuleTemplates.Load(path);var b=RuleTemplates.Load(path);
            Eq(false,a.Id==b.Id);Eq(false,a.Id==f.Rule.Id);Eq(0,a.Roster.Count);Eq("",a.Output);Eq("",a.SubjectKey);
            Eq(true,a.FlatAttachments);Eq(false,a.DownloadBody);Eq(7,a.MaxAttachmentMb);Eq(f.Rule.Prefix,a.Prefix);
            a.Keywords.Clear();Eq(true,b.Keywords.Count>0);Eq(true,f.Rule.Keywords.Count>0);Eq(1,f.Rule.Roster.Count);
            Eq(saved,File.ReadAllText(path));
            a.Output=Path.Combine(f.Root,"new-task");a.Roster=new(){{"777","李四"}};RuleValidator.Check(a);
            Eq(Validation.Success,RuleValidator.Match("工程实践-李四-777",a));return Task.CompletedTask;
        });
        await Test("shared attachment folder keeps same names distinct and records their paths",async()=>
        {
            var f=new Fixture();f.Rule.FlatAttachments=true;
            await f.Process(f.Mail("flat1",attach:true));await f.Process(f.Mail("flat2",attach:true));
            var records=f.Store.Archives().ToList();Eq(2,records.Count);
            var paths=records.SelectMany(r=>r.Attachments).ToList();Eq(2,paths.Distinct().Count());
            foreach(var path in paths){Eq(Path.Combine(f.Rule.Output,"全部附件"),Path.GetDirectoryName(path));Eq("附件测试",File.ReadAllText(path));}
            foreach(var record in records){Eq(0,Directory.GetFiles(record.Directory,"attachment_*").Length);Eq(true,File.Exists(Path.Combine(record.Directory,"附件位置.txt")));}
            Eq(true,f.Settings.Snapshot().Accounts[0].Rules[0].FlatAttachments);
            Eq(false,System.Text.Json.JsonSerializer.Deserialize<MailRule>("{}")!.FlatAttachments);
        });
        await Test("attachment limit keeps exact boundary skips larger file and original EML",async()=>
        {
            var f=new Fixture();f.Rule.MaxAttachmentMb=1;var m=f.Mail("size-limit");
            var builder=new BodyBuilder{TextBody="正文保留"};builder.Attachments.Add("刚好上限.bin",new byte[1024*1024]);builder.Attachments.Add("超过上限.bin",new byte[1024*1024+1]);m.Header.Body=builder.ToMessageBody();
            await f.Process(m);var r=f.Store.Archives().Single();Eq(1,r.Attachments.Count);Eq(1,r.SkippedAttachments.Count);Eq(1024L*1024+1,r.SkippedAttachments[0].SizeBytes);
            Eq(false,File.Exists(Path.Combine(r.Directory,"original.eml")));Eq(true,File.Exists(Path.Combine(r.Directory,"body.txt")));Eq(true,File.Exists(Path.Combine(r.Directory,"附件跳过记录.json")));Eq(true,f.Sender.Sent.Single().Body.Contains("未保存"));
            Eq(1,Directory.GetFiles(r.Directory,"attachment_*").Length);
        });
        await Test("all export option combinations and legacy defaults",async()=>
        {
            var legacy=System.Text.Json.JsonSerializer.Deserialize<MailRule>("{}")!;
            Eq(true,legacy.DownloadBody&&legacy.DownloadAttachments&&legacy.SaveOriginal);
            for(int bits=0;bits<8;bits++)
            {
                var f=new Fixture();f.Rule.DownloadBody=(bits&1)!=0;f.Rule.DownloadAttachments=(bits&2)!=0;f.Rule.SaveOriginal=(bits&4)!=0;
                var snapshot=f.Settings.Snapshot().Accounts[0].Rules[0];Eq(f.Rule.DownloadBody,snapshot.DownloadBody);Eq(f.Rule.DownloadAttachments,snapshot.DownloadAttachments);Eq(f.Rule.SaveOriginal,snapshot.SaveOriginal);
                await f.Process(f.Mail("options",attach:true));var r=f.Store.Archives().Single();
                Eq(f.Rule.DownloadBody,File.Exists(Path.Combine(r.Directory,"body.txt")));Eq(f.Rule.SaveOriginal,File.Exists(Path.Combine(r.Directory,"original.eml")));
                Eq(f.Rule.DownloadAttachments?1:0,r.Attachments.Count);Eq(true,File.Exists(Path.Combine(r.Directory,"metadata.json")));
            }
        });
        await Test("same directory combines rule export choices",async()=>
        {
            var f=new Fixture();f.Rule.DownloadBody=false;f.Rule.DownloadAttachments=true;f.Rule.SaveOriginal=false;
            var other=System.Text.Json.JsonSerializer.Deserialize<MailRule>(System.Text.Json.JsonSerializer.Serialize(f.Rule))!;
            other.Id=Guid.NewGuid().ToString();other.DownloadBody=true;other.DownloadAttachments=false;f.Account.Rules.Add(other);
            await f.Process(f.Mail("union",attach:true));var r=f.Store.Archives().Single();Eq(1,r.Attachments.Count);Eq(true,File.Exists(Path.Combine(r.Directory,"body.txt")));Eq(false,File.Exists(Path.Combine(r.Directory,"original.eml")));
        });
        await Test("re-export preserves previous files and never sends replies or counts errors",async()=>
        {
            var f=new Fixture();var m=f.Mail("reexport",attach:true);await f.Process(m);
            string old=f.Store.Archives().Single().Directory;
            await f.Engine.ProcessAsync(f.Account,m,f.Settings,_=>{},CancellationToken.None,true);
            var current=f.Store.Archives().Single();Eq(true,Directory.Exists(old));Eq(false,old==current.Directory);Eq(true,Path.GetFileName(current.Directory).StartsWith(m.Subject));Eq(1,f.Sender.Sent.Count);
            await f.Engine.ProcessAsync(f.Account,f.Mail("bad-reexport","工程实践-张三"),f.Settings,_=>{},CancellationToken.None,true);
            Eq(0,f.Store.Senders().Count());
        });
        await Test("inline named and unnamed binary attachments exported; cloud links explicit",async()=>
        {
            var f=new Fixture();var m=f.Mail("inline");
            m.Header.Body=new Multipart("mixed") {new TextPart("html"){Text="超大附件 <a href=\"https://wx.mail.qq.com/download?x=1&amp;y=2\">下载</a>"},new MimePart("application","pdf"){Content=new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("pdf-data"))),ContentDisposition=new ContentDisposition("inline"),FileName="报告.pdf"},new MimePart("application","octet-stream"){Content=new MimeContent(new MemoryStream([1,2,3]))}};
            await f.Process(m);var record=f.Store.Archives().Single();Eq(2,record.Attachments.Count);Eq(true,File.ReadAllText(Path.Combine(record.Directory,"云附件下载链接.txt")).Contains("x=1&y=2"));
            string summary=Path.Combine(f.Rule.Output,"云附件汇总.csv");Eq(true,File.ReadAllText(summary).Contains("x=1&y=2"));
            string before=File.ReadAllText(summary);CloudAttachmentSummary.Write(f.Rule.Output);Eq(before,File.ReadAllText(summary));
        });
        await Test("subject parsing and roster/name validation",()=>
        {
            var f=new Fixture();
            foreach(var entry in new Dictionary<string,Validation>{{"工程实践-张三-00123",Validation.Success},{"工程实践-张三",Validation.MissingKey},{"工程实践-张三-999",Validation.WrongKey},{"工程实践-李四-00123",Validation.Structure},{"实践提交-张三-00123",Validation.Structure},{"无关邮件",Validation.Ignore}})Eq(entry.Value,RuleValidator.Match(entry.Key,f.Rule));
            f.Rule.KeyMode="固定秘钥";f.Rule.SubjectKey="ABC";Eq(Validation.Success,RuleValidator.Match("工程实践-张三-ABC",f.Rule));return Task.CompletedTask;
        });
        await Test("mandatory destination and CSV leading zero",()=>
        {
            var f=new Fixture();f.Rule.Output="";bool threw=false;try{RuleValidator.Check(f.Rule);}catch(ArgumentException){threw=true;}Eq(true,threw);
            string path=Path.Combine(f.Root,"roster.csv");File.WriteAllText(path,"学号,姓名\n00123,张三\n",new UTF8Encoding(true));Eq("张三",RuleValidator.ImportRoster(path)["00123"]);return Task.CompletedTask;
        });
        await Test("archive Unicode attachments and metadata, reply exactly once",async()=>
        {
            var f=new Fixture();var m=f.Mail("a1",attach:true);await f.Process(m);await f.Process(m);
            Eq(1,f.Loads);Eq(1,f.Sender.Sent.Count);var a=f.Store.Archives().Single();Eq(true,File.Exists(Path.Combine(a.Directory,"original.eml")));Eq("附件测试",File.ReadAllText(a.Attachments.Single()));Eq(true,Path.GetFullPath(a.Attachments[0]).StartsWith(a.Directory));
        });
        await Test("all matching directories, single reply",async()=>
        {
            var f=new Fixture();f.Account.Rules.Add(new(){Name="第二组",Mode="关键词",Keywords=["工程实践"],Output=Path.Combine(f.Root,"second")});await f.Process(f.Mail("a2"));Eq(2,f.Store.Archives().Count);Eq(1,f.Sender.Sent.Count);
        });
        await Test("two generic replies then one admin notice, durable block",async()=>
        {
            var f=new Fixture();for(int i=1;i<=3;i++)await f.Process(f.Mail("err"+i,"工程实践-张三"));
            Eq(0,f.Loads);Eq(3,f.Sender.Sent.Count);Eq(true,f.Sender.Sent.Take(2).All(x=>x.Body==Constants.ErrorReply));Eq(Constants.BlockReply,f.Sender.Sent[2].Body);
            f.Store=new(Path.Combine(f.Root,"test.sqlite3"));f.Engine=new(f.Store,f.Sender);
            await f.Process(f.Mail("valid-blocked"));Eq(0,f.Loads);Eq(3,f.Sender.Sent.Count);Eq(true,f.Store.IsBlocked("student@example.test"));Eq(1,f.Store.Events().Count(x=>x.Kind=="停收"));
        });
        await Test("success breaks error streak; duplicate checks do not count; custom threshold",async()=>
        {
            var f=new Fixture();f.Settings.ErrorThreshold=4;
            var bad=f.Mail("repeat","工程实践-张三");await f.Process(bad);await f.Process(bad);
            Eq(1,f.Store.Senders().Single().Errors);
            await f.Process(f.Mail("success"));Eq(0,f.Store.Senders().Single().Errors);
            for(int i=0;i<3;i++)await f.Process(f.Mail("again"+i,"工程实践-张三"));
            Eq(false,f.Store.IsBlocked("student@example.test"));
            await f.Process(f.Mail("fourth","工程实践-张三"));Eq(true,f.Store.IsBlocked("student@example.test"));
        });
        await Test("success retry and historical reexport cannot erase newer errors",async()=>
        {
            var f=new Fixture();var good=f.Mail("good");await f.Process(good);
            await f.Process(f.Mail("bad","工程实践-张三"));
            await f.Engine.ProcessAsync(f.Account,good,f.Settings,_=>{},CancellationToken.None,true);
            f.Store.RegisterSuccess(good.Id,"student@example.test");
            Eq(1,f.Store.Senders().Single().Errors);
        });
        await Test("legacy cumulative counters migrate once and keep blocked senders",()=>
        {
            var f=new Fixture();f.Store.ImportSender("student@example.test",4,false);f.Store.ImportSender("blocked@example.test",6,true);
            using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(f.Root,"test.sqlite3")))
            {db.Open();using var command=db.CreateCommand();command.CommandText="DELETE FROM policy_migrations";command.ExecuteNonQuery();}
            f.Store=new(Path.Combine(f.Root,"test.sqlite3"));
            Eq(0,f.Store.Senders().Single(s=>s.Sender=="student@example.test").Errors);Eq(true,f.Store.IsBlocked("blocked@example.test"));
            f.Store.RegisterError("new-error","student@example.test",f.Account.Address,"test");
            f.Store=new(Path.Combine(f.Root,"test.sqlite3"));Eq(1,f.Store.Senders().Single(s=>s.Sender=="student@example.test").Errors);
            Eq(3,System.Text.Json.JsonSerializer.Deserialize<Settings>("{}")!.ErrorThreshold);
            return Task.CompletedTask;
        });
        await Test("administrator resets one sender; skipped old mail not replayed",async()=>
        {
            var f=new Fixture();f.Store.ImportSender("student@example.test",6,true);f.Store.ImportSender("other@example.test",6,true);
            var old=f.Mail("old");await f.Process(old);f.Store.Reset("student@example.test");await f.Process(old);Eq(0,f.Loads);await f.Process(f.Mail("new"));Eq(1,f.Loads);Eq(true,f.Store.IsBlocked("other@example.test"));Eq(0,f.Store.Senders().Single(x=>x.Sender=="student@example.test").Errors);
        });
        await Test("counters shared across receiving mailboxes",async()=>
        {
            var f=new Fixture();for(int i=0;i<2;i++)await f.Process(f.Mail("one"+i,"工程实践-张三"));
            f.Account.Address="second@example.test";for(int i=0;i<1;i++)await f.Process(f.Mail("two"+i,"工程实践-张三"));Eq(true,f.Store.IsBlocked("student@example.test"));Eq(Constants.BlockReply,f.Sender.Sent.Last().Body);
        });
        await Test("uncertain block notice never retried and block retained",async()=>
        {
            var f=new Fixture();f.Store.ImportSender("student@example.test",2,false);f.Sender.Fail=true;var mail=f.Mail("fail","工程实践-张三");
            try{await f.Process(mail);}catch(IOException){}
            await f.Process(mail);Eq(1,f.Sender.Sent.Count);Eq("Uncertain",f.Store.Replies().Single().Status);Eq(true,f.Store.IsBlocked("student@example.test"));
        });
        await Test("automated messages never replied or counted",async()=>
        {
            var f=new Fixture();var mail=f.Mail("auto","工程实践-张三");mail.Header.Headers.Add("Auto-Submitted","auto-replied");await f.Process(mail);Eq(0,f.Sender.Sent.Count);Eq(0,f.Store.Senders().Count);Eq(0,f.Loads);
        });
        await Test("global reply quota is durable",async()=>
        {
            var f=new Fixture();f.Settings.MaxRepliesPerHour=1;await f.Process(f.Mail("q1","工程实践-张三"));await f.Process(f.Mail("q2","工程实践-张三",from:"other@example.test"));Eq(1,f.Sender.Sent.Count);Eq(1,f.Store.Replies().Count(x=>x.Status=="RateLimited"));
        });
        await Test("size limit prevents body fetch",async()=>
        {
            var f=new Fixture();var mail=f.Mail("large") with{Size=100*1024L*1024};await f.Process(mail);Eq(0,f.Loads);Eq(0,f.Sender.Sent.Count);Eq("大小限制",f.Store.Events().Single().Kind);
        });
        await Test("same subject with very different bodies from different senders alerts",async()=>
        {
            var f=new Fixture();await f.Process(f.Mail("first",text:"太阳能电池的转换效率研究"));await f.Process(f.Mail("second",from:"other@example.test",text:"园林花卉修剪与艺术设计分析"));Eq(1,f.Store.Events().Count(x=>x.Kind=="内容异常"));
        });
        Console.WriteLine($"RESULT {passed} passed, {failed} failed");Environment.ExitCode=failed==0?0:1;
    }
}
