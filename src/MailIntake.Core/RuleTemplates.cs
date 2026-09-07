using System.Text.Json;
using System.Text.Encodings.Web;

namespace MailIntake.Core;

public sealed class RuleTemplate
{
    public string Format { get; set; } = "MailIntake.RuleTemplate";
    public int Version { get; set; } = 1;
    public MailRule Rule { get; set; } = new();
}

public static class RuleTemplates
{
    public static MailRule Instantiate(MailRule source)
    {
        var copy=JsonSerializer.Deserialize<MailRule>(JsonSerializer.Serialize(source))!;
        copy.Id=Guid.NewGuid().ToString("N");
        copy.Roster=[];copy.SubjectKey="";copy.Output="";
        return copy;
    }
    public static void Save(string path,MailRule source)
    {
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(new RuleTemplate{Rule=Instantiate(source)},new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        File.Move(temporary,path,true);
    }
    public static MailRule Load(string path)
    {
        if(new FileInfo(path).Length>1024*1024)throw new ArgumentException("模板文件过大，请选择规则模板文件。");
        var template=JsonSerializer.Deserialize<RuleTemplate>(File.ReadAllText(path));
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        if(!document.RootElement.TryGetProperty("Format",out _)||template is null||template.Format!="MailIntake.RuleTemplate"||template.Version!=1||template.Rule is null)
            throw new ArgumentException("文件不是支持的规则模板。");
        var rule=template.Rule;
        if(rule.Mode is not ("关键词" or "结构校验")||rule.KeyMode is not ("名单" or "固定秘钥")||rule.Fields is null||rule.Keywords is null||rule.IntervalMinutes<1||rule.IntervalMinutes>1440||rule.MaxAttachmentMb<1||rule.MaxAttachmentMb>500)
            throw new ArgumentException("模板中的设置无效。");
        return Instantiate(rule);
    }
}
