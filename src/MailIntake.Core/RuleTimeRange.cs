namespace MailIntake.Core;

public static class RuleTimeRange
{
    public static DateTime ScanStart(MailAccount account)=>account.Rules.Count==0?account.Since.Date:account.Rules.Min(r=>(r.StartDate??account.Since).Date);
    public static bool Contains(MailRule rule,MailAccount account,DateTimeOffset time)=>time.Date>=(rule.StartDate??account.Since).Date&&(!rule.EndDate.HasValue||time.Date<=rule.EndDate.Value.Date);
}
