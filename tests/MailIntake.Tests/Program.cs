using MailIntake.Core;
using MimeKit;
using System.Text;

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
        await Test("five generic replies then one admin notice, durable block",async()=>
        {
            var f=new Fixture();for(int i=1;i<=6;i++)await f.Process(f.Mail("err"+i,"工程实践-张三"));
            Eq(0,f.Loads);Eq(6,f.Sender.Sent.Count);Eq(true,f.Sender.Sent.Take(5).All(x=>x.Body==Constants.ErrorReply));Eq(Constants.BlockReply,f.Sender.Sent[5].Body);
            f.Store=new(Path.Combine(f.Root,"test.sqlite3"));f.Engine=new(f.Store,f.Sender);
            await f.Process(f.Mail("valid-blocked"));Eq(0,f.Loads);Eq(6,f.Sender.Sent.Count);Eq(true,f.Store.IsBlocked("student@example.test"));Eq(1,f.Store.Events().Count(x=>x.Kind=="停收"));
        });
        await Test("administrator resets one sender; skipped old mail not replayed",async()=>
        {
            var f=new Fixture();f.Store.ImportSender("student@example.test",6,true);f.Store.ImportSender("other@example.test",6,true);
            var old=f.Mail("old");await f.Process(old);f.Store.Reset("student@example.test");await f.Process(old);Eq(0,f.Loads);await f.Process(f.Mail("new"));Eq(1,f.Loads);Eq(true,f.Store.IsBlocked("other@example.test"));Eq(0,f.Store.Senders().Single(x=>x.Sender=="student@example.test").Errors);
        });
        await Test("counters shared across receiving mailboxes",async()=>
        {
            var f=new Fixture();for(int i=0;i<3;i++)await f.Process(f.Mail("one"+i,"工程实践-张三"));
            f.Account.Address="second@example.test";for(int i=0;i<3;i++)await f.Process(f.Mail("two"+i,"工程实践-张三"));Eq(true,f.Store.IsBlocked("student@example.test"));Eq(Constants.BlockReply,f.Sender.Sent.Last().Body);
        });
        await Test("uncertain block notice never retried and block retained",async()=>
        {
            var f=new Fixture();f.Store.ImportSender("student@example.test",5,false);f.Sender.Fail=true;var mail=f.Mail("fail","工程实践-张三");
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
