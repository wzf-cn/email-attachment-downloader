using System.Text;

namespace MailIntake.Core;

public static class SubjectInstructions
{
    public static string Generate(MailRule rule,bool english=false)
    {
        if(!rule.SearchSubject&&!rule.SearchBody&&!rule.SearchAttachmentNames)throw new ArgumentException("请至少选择一个检索范围。");
        var words=rule.Keywords.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(words.Count==0&&!string.IsNullOrWhiteSpace(rule.Prefix))words.Add(rule.Prefix.Trim());
        if(words.Count==0)throw new ArgumentException("请先填写触发关键词。");
        if(words.Any(x=>x.IndexOfAny(['\r','\n'])>=0))throw new ArgumentException("关键词不能包含换行。");
        bool structured=rule.Mode=="结构校验";
        bool all=rule.Mode=="关键词"&&rule.MatchAll;
        string topic=string.Join(" ",words);
        var fields=rule.Fields.Select(f=>f.Trim()).ToList();
        string pattern=topic;
        if(structured)
        {
            if(string.IsNullOrEmpty(rule.Separator)||topic.Contains(rule.Separator)||fields.Count==0||fields.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("请填写主题项目和分隔符；关键词不要包含字段分隔符。");
            pattern=string.Join(rule.Separator,new[]{topic}.Concat(fields.Select(f=>"{"+f+"}")).Append(rule.KeyMode=="名单"?(english?"{registered ID / key}":"{名单编号或秘钥}"):(english?"{provided key}":"{管理员提供的秘钥}")));
        }
        var text=new StringBuilder();
        text.Append(english?$"Please use this email subject: {pattern}. ":$"请将邮件主题填写为：{pattern}。");
        if(structured)
        {
            text.Append(english?"Replace every {...} placeholder with your own information and remove the braces. ":"请将大括号中的项目替换为你自己的对应信息，删除大括号，不要直接照抄占位文字。");
            text.Append(english?$"Keep “{topic}”, the field order and separator “{rule.Separator}” unchanged; do not add fields or use this separator inside a field. ":$"保留“{topic}”、项目顺序和分隔符“{rule.Separator}”，不要增删项目，填写内容中不要再使用该分隔符。");
            text.Append(rule.KeyMode=="名单"?(english?"Use the ID/key registered with the administrator, preserving leading zeros; if your name is requested, use the registered name. ":"编号或秘钥须与管理员名单一致，保留开头的 0；要求填写姓名时，请使用名单登记的姓名。"):(english?"Obtain the key separately from the administrator and enter it exactly. ":"秘钥请向管理员单独获取并原样填写。"));
        }
        else text.Append(english?"Keep these keywords in the subject. ":"请保留主题中的这些关键词。");
        var scopes=new List<string>();if(rule.SearchSubject)scopes.Add(english?"subject":"主题");if(rule.SearchBody)scopes.Add(english?"body":"正文");if(rule.SearchAttachmentNames)scopes.Add(english?"attachment filenames":"附件名");
        text.Append(english?$"The {string.Join(" / ",scopes)} must contain {(all?"all of":"at least one of")} these keywords: {string.Join(", ",words)}. ":$"关键词检索范围为{string.Join("、",scopes)}，需包含{(all?"全部":"至少一个")}关键词：{string.Join("、",words)}。");
        if(rule.SearchAttachmentNames)text.Append(english?"Attachment contents are not searched. ":"附件内部内容不参与关键词检索。");
        text.Append(english?"Check the subject and attachments before sending.":"发送前请核对主题及附件。");
        return text.ToString();
    }
}
