namespace MailIntake.Core;

public sealed class RuleSchedule
{
    private readonly Dictionary<string,DateTime> next=[];
    private static string Key(MailAccount account,MailRule rule)=>account.Id+"/"+rule.Id;
    public void Reset()=>next.Clear();
    public HashSet<string> Due(MailAccount account,DateTime now,bool force=false)=>account.Rules.Where(r=>force||!next.TryGetValue(Key(account,r),out var at)||at<=now).Select(r=>r.Id).ToHashSet();
    public void Complete(MailAccount account,ISet<string> ids,DateTime now)
    {foreach(var rule in account.Rules.Where(r=>ids.Contains(r.Id)))next[Key(account,rule)]=now.AddMinutes(Math.Clamp(rule.IntervalMinutes,1,1440));}
}
