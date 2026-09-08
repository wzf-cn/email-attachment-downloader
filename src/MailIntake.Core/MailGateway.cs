using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using System.Runtime.CompilerServices;

namespace MailIntake.Core;

public interface IReplySender
{
    Task SendAsync(MailAccount account,Incoming incoming,string target,string body,string messageId,CancellationToken token);
}

public sealed class MailGateway(Func<string,string> decrypt) : IReplySender
{
    private static SecureSocketOptions Security(string mode) => mode=="STARTTLS" ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
    private static MimeMessage HeaderMessage(HeaderList headers)
    {
        var message=new MimeMessage(); message.Headers.Clear();
        foreach(var header in headers) message.Headers.Add(header);
        return message;
    }
    public async IAsyncEnumerable<Incoming> Scan(MailAccount account,[EnumeratorCancellation] CancellationToken token)
    {
        if(account.Protocol=="POP3")
        {
            using var client=new Pop3Client { Timeout=40000 };
            await client.ConnectAsync(account.Host,account.Port,Security(account.Security),token);
            await client.AuthenticateAsync(account.Address,decrypt(account.EncryptedPassword),token);
            // UIDL is required for durable deduplication; do not substitute volatile indexes.
            var ids=await client.GetMessageUidsAsync(token);
            for(int i=0;i<ids.Count;i++)
            {
                int index=i; token.ThrowIfCancellationRequested();
                var header=HeaderMessage(await client.GetMessageHeadersAsync(index,token));
                if(header.Date.Date<RuleTimeRange.ScanStart(account)) continue;
                var key=Constants.Hash($"{account.Host}|{account.Address}|POP3|{ids[i]}");
                int size=await client.GetMessageSizeAsync(index,token);
                yield return new Incoming(key,header.Subject??"",header.From.Mailboxes.FirstOrDefault()?.Address??"",null,size,header,
                    ct=>client.GetMessageAsync(index,ct));
            }
            await client.DisconnectAsync(true,token);
        }
        else
        {
            using var client=new ImapClient { Timeout=40000 };
            await client.ConnectAsync(account.Host,account.Port,Security(account.Security),token);
            await client.AuthenticateAsync(account.Address,decrypt(account.EncryptedPassword),token);
            var folder=await client.GetFolderAsync(account.Folder,token);
            await folder.OpenAsync(FolderAccess.ReadOnly,token);
            var ids=await folder.SearchAsync(SearchQuery.DeliveredAfter(RuleTimeRange.ScanStart(account)),token);
            foreach(var batch in ids.Chunk(100))
            {
                var summaries=await folder.FetchAsync(batch,MessageSummaryItems.UniqueId|MessageSummaryItems.Envelope|MessageSummaryItems.InternalDate|MessageSummaryItems.Size|MessageSummaryItems.Headers,token);
                foreach(var summary in summaries)
                {
                    token.ThrowIfCancellationRequested();
                    var uid=summary.UniqueId;
                    var headers=summary.Headers??await folder.GetHeadersAsync(uid,token);
                    var header=HeaderMessage(headers);
                    var key=Constants.Hash($"{account.Host}|{account.Address}|{account.Folder}|{folder.UidValidity}|{uid.Id}");
                    yield return new Incoming(key,header.Subject??"",header.From.Mailboxes.FirstOrDefault()?.Address??"",summary.InternalDate,summary.Size??0,header,
                        ct=>folder.GetMessageAsync(uid,ct));
                }
            }
            await client.DisconnectAsync(true,token);
        }
    }
    public async Task TestAsync(MailAccount account,CancellationToken token)
    {
        if(account.Protocol=="POP3")
        {
            using var client=new Pop3Client { Timeout=15000 };
            await client.ConnectAsync(account.Host,account.Port,Security(account.Security),token);
            await client.AuthenticateAsync(account.Address,decrypt(account.EncryptedPassword),token);
            await client.DisconnectAsync(true,token);
        }
        else
        {
            using var client=new ImapClient { Timeout=15000 };
            await client.ConnectAsync(account.Host,account.Port,Security(account.Security),token);
            await client.AuthenticateAsync(account.Address,decrypt(account.EncryptedPassword),token);
            var folder=await client.GetFolderAsync(account.Folder,token);
            await folder.OpenAsync(FolderAccess.ReadOnly,token);
            await client.DisconnectAsync(true,token);
        }
        using var smtp=new SmtpClient { Timeout=15000 };
        await smtp.ConnectAsync(account.SmtpHost,account.SmtpPort,Security(account.SmtpSecurity),token);
        await smtp.AuthenticateAsync(account.Address,decrypt(account.EncryptedPassword),token);
        await smtp.DisconnectAsync(true,token);
    }
    public async Task SendAsync(MailAccount account,Incoming incoming,string target,string body,string messageId,CancellationToken token)
    {
        var reply=new MimeMessage();
        reply.From.Add(MailboxAddress.Parse(account.Address)); reply.To.Add(MailboxAddress.Parse(target));
        reply.Subject="邮件处理结果"; reply.MessageId=messageId; reply.Date=DateTimeOffset.Now;
        reply.Headers.Add("Auto-Submitted","auto-replied"); reply.Headers.Add("X-Auto-Response-Suppress","All");
        if(!string.IsNullOrEmpty(incoming.Header.MessageId)) { reply.InReplyTo=incoming.Header.MessageId; reply.References.Add(incoming.Header.MessageId); }
        reply.Body=new TextPart("plain") { Text=body };
        using var client=new SmtpClient { Timeout=40000 };
        await client.ConnectAsync(account.SmtpHost,account.SmtpPort,Security(account.SmtpSecurity),token);
        await client.AuthenticateAsync(account.Address,decrypt(account.EncryptedPassword),token);
        await client.SendAsync(reply,token);
        // Once SendAsync returns, delivery was accepted; QUIT failure must not invalidate that fact.
        try { await client.DisconnectAsync(true,token); } catch { }
    }
}
