using System.Text;

namespace MailIntake.Core;

public static class SubjectInstructions
{
    public static List<string> SuggestKeywords(string topic)=>topic.Replace('，',',').Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public static string Generate(string topic,MailRule rule,bool english=false)
    {
        topic=topic.Trim();
        if(topic.Length==0)throw new ArgumentException("请先填写本次邮件主题，例如：工程实践报告。");
        if(topic.IndexOfAny(['\r','\n'])>=0)throw new ArgumentException("邮件主题不能包含换行。");
        if(!rule.SearchSubject&&!rule.SearchBody&&!rule.SearchAttachmentNames)throw new ArgumentException("请至少选择一个检索范围。");
        var words=rule.Keywords.Where(x=>!string.IsNullOrWhiteSpace(x)).ToList();
        if(words.Count==0&&!string.IsNullOrWhiteSpace(rule.Prefix))words.Add(rule.Prefix);
        if(words.Count==0)throw new ArgumentException("请先填写触发关键词。");
        bool structured=rule.Mode=="结构校验";
        bool all=rule.Mode=="关键词"&&rule.MatchAll;
        if(rule.SearchSubject&&!rule.SearchBody&&!rule.SearchAttachmentNames)
        {
            var matches=words.Select(w=>topic.Contains(w,StringComparison.OrdinalIgnoreCase));
            if(!(all?matches.All(x=>x):matches.Any(x=>x)))throw new ArgumentException("本次邮件主题未满足关键词要求，请修改主题或关键词后再生成。");
        }
        var fields=rule.Fields.Select(f=>f.Trim()).ToList();
        string pattern=topic;
        if(structured)
        {
            if(string.IsNullOrEmpty(rule.Separator)||topic.Contains(rule.Separator)||fields.Count==0||fields.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("请填写主题项目和分隔符；本次邮件主题不要包含字段分隔符。");
            pattern=string.Join(rule.Separator,new[]{topic}.Concat(fields.Select(f=>"{"+f+"}")).Append(rule.KeyMode=="名单"?(english?"{student ID / key}":"{学号或名单编号}"):(english?"{provided key}":"{管理员提供的秘钥}")));
        }
        var text=new StringBuilder();
        text.Append(english?$"Please use this email subject: {pattern}. ":$"请将邮件主题填写为：{pattern}。");
        if(structured)
        {
            text.Append(english?"Replace every {...} placeholder with your own information and remove the braces. ":"请将大括号中的项目替换为你自己的对应信息，删除大括号，不要直接照抄占位文字。");
            text.Append(english?$"Keep “{topic}”, the field order and separator “{rule.Separator}” unchanged; do not add fields or use this separator inside a field. ":$"保留“{topic}”、项目顺序和分隔符“{rule.Separator}”，不要增删项目，填写内容中不要再使用该分隔符。");
            text.Append(rule.KeyMode=="名单"?(english?"Use the ID/key registered with the administrator, preserving leading zeros; if your name is requested, use the registered name. ":"学号或编号须与管理员名单一致，保留开头的 0；要求填写姓名时，请使用名单登记的姓名。"):(english?"Obtain the key separately from the administrator and enter it exactly. ":"秘钥请向管理员单独获取并原样填写。"));
        }
        else text.Append(english?"This rule does not require a name or ID structure. ":"本规则不要求额外的姓名、学号结构。");
        var scopes=new List<string>();if(rule.SearchSubject)scopes.Add(english?"subject":"主题");if(rule.SearchBody)scopes.Add(english?"body":"正文");if(rule.SearchAttachmentNames)scopes.Add(english?"attachment filenames":"附件名");
        text.Append(english?$"The {string.Join(" / ",scopes)} must contain {(all?"all of":"at least one of")} these keywords: {string.Join(", ",words)}. ":$"关键词检索范围为{string.Join("、",scopes)}，需包含{(all?"全部":"至少一个")}关键词：{string.Join("、",words)}。");
        if(rule.SearchAttachmentNames)text.Append(english?"Attachment contents are not searched. ":"附件内部内容不参与关键词检索。");
        text.Append(english?"Check the subject and attachments before sending.":"发送前请核对主题及附件。");
        return text.ToString();
    }
}
