using MimeKit;
using System.Net;
using System.Text.RegularExpressions;

namespace MailIntake.Core;

public static class AttachmentExport
{
    public static IEnumerable<MimeEntity> Parts(MimeMessage message)=>Walk(message.Body);
    private static IEnumerable<MimeEntity> Walk(MimeEntity? entity)
    {
        if(entity is null)yield break;
        if(entity is Multipart multi){foreach(var child in multi)foreach(var item in Walk(child))yield return item;}
        else if(entity.IsAttachment||entity is MessagePart||!string.IsNullOrWhiteSpace(entity.ContentDisposition?.FileName??entity.ContentType.Name)
            ||entity is MimePart and not TextPart)yield return entity;
    }
    public static List<string> CloudLinks(MimeMessage message)
    {
        string html=message.HtmlBody??"";
        if(!Regex.IsMatch(html+message.TextBody,"超大附件|中转站|云附件|微云"))return [];
        return Regex.Matches(html,"href\\s*=\\s*[\"']([^\"']+)[\"']",RegexOptions.IgnoreCase)
            .Select(m=>WebUtility.HtmlDecode(m.Groups[1].Value))
            .Where(s=>Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.Scheme=="https"&&
                (u.Host.Equals("wx.mail.qq.com",StringComparison.OrdinalIgnoreCase)||u.Host.EndsWith(".weiyun.com",StringComparison.OrdinalIgnoreCase)||u.Host.Equals("mail.qq.com",StringComparison.OrdinalIgnoreCase)))
            .Distinct().ToList();
    }
}
