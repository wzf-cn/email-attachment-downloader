using Microsoft.VisualBasic.FileIO;
using System.Text;

namespace MailIntake.Core;

public static class RuleValidator
{
    public static Validation Match(string subject, MailRule rule)
    {
        var words = rule.Keywords.Count > 0 ? rule.Keywords : [rule.Prefix];
        var hits = words.Select(w => !string.IsNullOrWhiteSpace(w) && subject.Contains(w,StringComparison.OrdinalIgnoreCase));
        if (rule.Mode == "关键词") return (rule.MatchAll ? hits.All(x=>x) : hits.Any(x=>x)) ? Validation.Success : Validation.Ignore;
        if (!hits.Any(x=>x)) return Validation.Ignore;
        if (!subject.StartsWith(rule.Prefix,StringComparison.Ordinal)) return Validation.Structure;
        var tail = subject[rule.Prefix.Length..];
        if (string.IsNullOrEmpty(rule.Separator) || !tail.StartsWith(rule.Separator,StringComparison.Ordinal)) return Validation.Structure;
        var parts = tail[rule.Separator.Length..].Split(rule.Separator,StringSplitOptions.None);
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
        if(rule.MaxAttachmentMb<1||rule.MaxAttachmentMb>500)throw new ArgumentException("单个附件上限必须为 1 到 500 MB。");
        if (string.IsNullOrWhiteSpace(rule.Name)) throw new ArgumentException("请填写规则名称。");
        if (string.IsNullOrWhiteSpace(rule.Output) || !Path.IsPathFullyQualified(rule.Output)) throw new ArgumentException("请为此组规则选择完整下载目录。");
        if (rule.Mode == "关键词")
        {
            if (rule.Keywords.Count == 0) throw new ArgumentException("至少填写一个关键词。");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rule.Prefix) || string.IsNullOrEmpty(rule.Separator) || rule.Fields.Count==0) throw new ArgumentException("固定开头、分隔符和中间字段不能为空。");
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
