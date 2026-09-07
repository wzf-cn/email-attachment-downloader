namespace MailIntake.Core;

public sealed record MailProvider(string Name,string[] Domains,string Imap,string Pop,string Smtp,int SmtpPort=465,string SmtpSecurity="SSL/TLS",bool RequiresOAuth=false)
{
    public (string Host,int Port,string Security) Incoming(string protocol)=>
        (protocol=="POP3"?Pop:Imap,protocol=="POP3"?995:993,"SSL/TLS");
}

// Curated offline defaults. Sources and verification date: docs/MAIL_PROVIDERS.md.
public static class MailProviders
{
    public static IReadOnlyList<MailProvider> All {get;} = new MailProvider[]
    {
        new("QQ",["qq.com","foxmail.com"],"imap.qq.com","pop.qq.com","smtp.qq.com"),
        new("163",["163.com"],"imap.163.com","pop.163.com","smtp.163.com"),
        new("126",["126.com"],"imap.126.com","pop.126.com","smtp.126.com"),
        new("Yeah",["yeah.net"],"imap.yeah.net","pop.yeah.net","smtp.yeah.net"),
        new("Sina.com",["sina.com"],"imap.sina.com","pop.sina.com","smtp.sina.com"),
        new("Sina.cn",["sina.cn"],"imap.sina.cn","pop.sina.cn","smtp.sina.cn"),
        new("Sina VIP",["vip.sina.com"],"imap.vip.sina.com","pop.vip.sina.com","smtp.vip.sina.com"),
        new("Sina VIP CN",["vip.sina.cn"],"imap.vip.sina.cn","pop.vip.sina.cn","smtp.vip.sina.cn"),
        new("Aliyun",["aliyun.com"],"imap.aliyun.com","pop3.aliyun.com","smtp.aliyun.com"),
        new("139",["139.com"],"imap.139.com","pop.139.com","smtp.139.com"),
        new("Gmail",["gmail.com","googlemail.com"],"imap.gmail.com","pop.gmail.com","smtp.gmail.com"),
        new("Yahoo",["yahoo.com","ymail.com","rocketmail.com"],"imap.mail.yahoo.com","pop.mail.yahoo.com","smtp.mail.yahoo.com"),
        new("Outlook",["outlook.com","hotmail.com","live.com","msn.com"],"outlook.office365.com","outlook.office365.com","smtp-mail.outlook.com",587,"STARTTLS",true),
    };
    public static MailProvider? Find(string address)
    {
        address=address.Trim();int at=address.IndexOf('@');
        if(at<=0||at!=address.LastIndexOf('@')||address.Any(char.IsWhiteSpace))return null;
        string domain=address[(at+1)..];
        return All.FirstOrDefault(p=>p.Domains.Contains(domain,StringComparer.OrdinalIgnoreCase));
    }
}
