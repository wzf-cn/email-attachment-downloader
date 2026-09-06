using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MimeKit;

namespace MailIntake.Core;

public sealed class Settings
{
    public int Version { get; set; } = 1;
    public List<MailAccount> Accounts { get; set; } = [];
    public int IntervalMinutes { get; set; } = 5;
    public bool AutoStart { get; set; } = true;
    public bool RunOnLaunch { get; set; }
    public int MaxMessageMb { get; set; } = 30;
    public int MaxPerCycle { get; set; } = 200;
    public int MaxRepliesPerHour { get; set; } = 100;
    public Settings Snapshot() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
}

public sealed class MailAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Address { get; set; } = "";
    public string Protocol { get; set; } = "IMAP";
    public string Host { get; set; } = "imap.qq.com";
    public int Port { get; set; } = 993;
    public string SmtpHost { get; set; } = "smtp.qq.com";
    public int SmtpPort { get; set; } = 465;
    public string Security { get; set; } = "SSL/TLS";
    public string SmtpSecurity { get; set; } = "SSL/TLS";
    public string Folder { get; set; } = "INBOX";
    public DateTime Since { get; set; } = DateTime.Today.AddDays(-30);
    public string EncryptedPassword { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<MailRule> Rules { get; set; } = [];
    public override string ToString() => Address;
}

public sealed class MailRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "结构校验";
    public string Prefix { get; set; } = "";
    public string Separator { get; set; } = "-";
    public List<string> Fields { get; set; } = ["姓名"];
    public List<string> Keywords { get; set; } = [];
    public bool MatchAll { get; set; }
    public string KeyMode { get; set; } = "名单";
    public string SubjectKey { get; set; } = "";
    public Dictionary<string,string> Roster { get; set; } = [];
    public string Output { get; set; } = "";
    public bool DownloadBody { get; set; } = true;
    public bool DownloadAttachments { get; set; } = true;
    public bool SaveOriginal { get; set; } = true;
    public bool ReplyEnabled { get; set; } = true;
    public string SuccessReply { get; set; } = "已收到，正文及附件已保存。";
    public bool DetectAnomaly { get; set; } = true;
    public override string ToString() => Name;
}

public enum Validation { Ignore, Success, MissingKey, WrongKey, Structure }
public sealed record Incoming(string Id, string Subject, string Sender, DateTimeOffset? ReceivedAt,
    long Size, MimeMessage Header, Func<CancellationToken,Task<MimeMessage>> Load);
public sealed record ArchiveRecord(string Id, string Account, string Sender, string Subject,
    string Rule, string Directory, DateTimeOffset? ReceivedAt, DateTimeOffset SentAt,
    DateTimeOffset SavedAt, string MessageId, List<string> Attachments);
public sealed record SenderState(string Sender, int Errors, bool Blocked, string UpdatedAt);
public sealed record EventRecord(long Id, string Time, string Kind, string Sender, string Account, string Detail);
public sealed record ReplyRecord(string Id,string Status,string Sender,string Account,string Kind,string MessageId,string Time);
public static class Constants
{
    public const string ErrorReply = "主题的格式或内容不符合要求";
    public const string BlockReply = "您的邮箱因多次提交不符合要求的主题，已暂停接收处理。请联系管理员重新开放接收，恢复后重新发送邮件。";
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
