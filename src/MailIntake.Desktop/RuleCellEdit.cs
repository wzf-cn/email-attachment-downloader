using MailIntake.Core;
using System.Globalization;
using System.Text.Json;

namespace MailIntake.Desktop;

internal static class RuleCellEdit
{
    internal static MailRule Apply(MailRule source,string field,string text)
    {
        var rule=JsonSerializer.Deserialize<MailRule>(JsonSerializer.Serialize(source))!;
        text=text.Trim();
        DateTime Date()=>DateTime.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:throw new ArgumentException("日期请使用 yyyy-MM-dd 格式。");
        switch(field)
        {
            case "名称":rule.Name=text;break;
            case "关键词":rule.Keywords=text.Replace('，',',').Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();break;
            case "检索范围":rule.SearchSubject=text.Contains("主题");rule.SearchBody=text.Contains("正文");rule.SearchAttachmentNames=text.Contains("附件名");break;
            case "检索主题":rule.SearchSubject=bool.Parse(text);break;
            case "检索正文":rule.SearchBody=bool.Parse(text);break;
            case "检索附件名":rule.SearchAttachmentNames=bool.Parse(text);break;
            case "开始日期":rule.StartDate=Date();break;
            case "结束日期":
                if(string.IsNullOrEmpty(text)||text=="不限"){rule.EndAt=null;rule.EndDate=null;}
                else if(DateTime.TryParseExact(text,"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var end)){rule.EndAt=end;rule.EndDate=null;}
                else {rule.EndDate=Date();rule.EndAt=null;}
                break;
            case "检查频率":rule.IntervalMinutes=int.TryParse(text,out var minutes)?minutes:throw new ArgumentException("检查频率请输入整数分钟。");break;
            case "下载目录":rule.Output=text;break;
            case "自动回复":rule.ReplyEnabled=text=="已开启";break;
        }
        RuleValidator.Check(rule);return rule;
    }
}
