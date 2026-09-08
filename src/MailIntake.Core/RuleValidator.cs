using Microsoft.VisualBasic.FileIO;
using System.Text;

namespace MailIntake.Core;

public static class RuleValidator
{
    public static string HtmlText(string html)
    {
        string Strip(string text,string pattern,string replacement)=>System.Text.RegularExpressions.Regex.Replace(text,pattern,replacement,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Singleline,TimeSpan.FromSeconds(2));
        html=Strip(html,@"<(script|style)\b[^>]*>.*?</\1\s*>","");
        html=Strip(html,@"<(br|p|div|li|tr|h[1-6])\b[^>]*>|</(p|div|li|tr|h[1-6])\s*>","\n");
        return System.Net.WebUtility.HtmlDecode(Strip(html,@"<[^>]+>",""));
    }
    public static Validation Match(string subject, MailRule rule,string body="",IEnumerable<string>? attachmentNames=null)
    {
        var words = rule.Keywords.Count > 0 ? rule.Keywords : [rule.Prefix];
        var sources=new List<string>();
        if(rule.SearchSubject)sources.Add(subject);
        if(rule.SearchBody)sources.Add(body);
        if(rule.SearchAttachmentNames&&attachmentNames!=null)sources.AddRange(attachmentNames);
        var hits = words.Select(w => !string.IsNullOrWhiteSpace(w) && sources.Any(text=>text.Contains(w,StringComparison.OrdinalIgnoreCase)));
        if (rule.Mode == "关键词") return (rule.MatchAll ? hits.All(x=>x) : hits.Any(x=>x)) ? Validation.Success : Validation.Ignore;
        if (!hits.Any(x=>x)) return Validation.Ignore;
        if (string.IsNullOrEmpty(rule.Separator)) return Validation.Structure;
        var parts = subject.Split(rule.Separator,StringSplitOptions.None).Skip(1).ToArray();
        int n = rule.Fields.Count;
        if (parts.Length == n) return parts.All(p=>!string.IsNullOrWhiteSpace(p)) ? Validation.MissingKey : Validation.Structure;
        if (parts.Length != n+1 || parts.Take(n).Any(string.IsNullOrWhiteSpace)) return Validation.Structure;
        if (parts[^1].Length == 0) return Validation.MissingKey;
        if (rule.KeyMode == "名单")
        {
            if (!rule.Roster.TryGetValue(parts[^1],out var name)) return Validation.WrongKey;
            int index = rule.Fields.FindIndex(f=>f=="姓名"||f.Equals("Name",StringComparison.OrdinalIgnoreCase));
            if (name.Length>0 && index>=0 && parts[index]!=name) return Validation.Structure;
        }
        else if (parts[^1] != rule.SubjectKey) return Validation.WrongKey;
        return Validation.Success;
    }

    public static void Check(MailRule rule)
    {
        if(rule.StartDate.HasValue&&rule.EndDate.HasValue&&rule.EndDate.Value.Date<rule.StartDate.Value.Date)throw new ArgumentException("结束日期不能早于开始日期。");
        if(!rule.SearchSubject&&!rule.SearchBody&&!rule.SearchAttachmentNames)throw new ArgumentException("请至少选择一个检索范围。");
        if(rule.IntervalMinutes<1||rule.IntervalMinutes>1440)throw new ArgumentException("检查频率必须为 1 到 1440 分钟。");
        if(rule.MaxAttachmentMb<1||rule.MaxAttachmentMb>500)throw new ArgumentException("单个附件上限必须为 1 到 500 MB。");
        if(rule.Keywords.Count==0&&string.IsNullOrWhiteSpace(rule.Prefix))throw new ArgumentException("至少填写一个关键词。");
        if (string.IsNullOrWhiteSpace(rule.Name)) throw new ArgumentException("请填写规则名称。");
        if (string.IsNullOrWhiteSpace(rule.Output) || !Path.IsPathFullyQualified(rule.Output)) throw new ArgumentException("请为此组规则选择完整下载目录。");
        if (rule.Mode == "关键词")
        {
            if (rule.Keywords.Count == 0) throw new ArgumentException("至少填写一个关键词。");
        }
        else
        {
            if (string.IsNullOrEmpty(rule.Separator) || rule.Fields.Count==0) throw new ArgumentException("分隔符和主题项目不能为空。");
            if (rule.KeyMode == "名单" && rule.Roster.Count == 0) throw new ArgumentException("请导入学号或秘钥名单。");
            if (rule.KeyMode != "名单" && (string.IsNullOrEmpty(rule.SubjectKey) || rule.SubjectKey.Contains(rule.Separator))) throw new ArgumentException("固定秘钥不能为空，且不能含分隔符。");
        }
        if (rule.ReplyEnabled && string.IsNullOrWhiteSpace(rule.SuccessReply)) throw new ArgumentException("请填写成功回复内容。");
    }

    public static Dictionary<string,string> ImportRoster(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = File.ReadAllBytes(path);
        string text;
        try { text = new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { text=Encoding.GetEncoding(54936).GetString(bytes); }
        using var parser = new TextFieldParser(new StringReader(text));
        parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes=true;
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        int line=0;
        while (!parser.EndOfData)
        {
            var row=parser.ReadFields(); line++;
            if (row is null || row.Length==0) continue;
            var id=row[0].Trim();
            if (line==1 && new[]{"学号","秘钥","密钥","student_id","key"}.Contains(id)) continue;
            if (id.Length==0) throw new ArgumentException($"第 {line} 行缺少学号。");
            string name=row.Length>1?row[1].Trim():"";
            if (result.TryGetValue(id,out var old) && old!=name) throw new ArgumentException($"第 {line} 行学号重复且姓名不同。");
            result[id]=name;
        }
        if (result.Count==0) throw new ArgumentException("名单没有有效数据。");
        return result;
    }
}
