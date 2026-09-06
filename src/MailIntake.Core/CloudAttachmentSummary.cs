using System.Text;
using System.Text.Json;

namespace MailIntake.Core;

public static class CloudAttachmentSummary
{
    public static void Write(string root)
    {
        var rows=new List<string>{"邮件主题,发件人,收件邮箱,邮件时间,规则,文件夹,下载链接,状态"};
        static string Cell(string text)
        {
            if(text.TrimStart().StartsWith('=')||text.TrimStart().StartsWith('+')||text.TrimStart().StartsWith('-')||text.TrimStart().StartsWith('@'))text="'"+text;
            return "\""+text.Replace("\"","\"\"")+"\"";
        }
        foreach(string folder in Directory.EnumerateDirectories(root).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
        {
            if(Path.GetFileName(folder).StartsWith('.'))continue;
            string links=Path.Combine(folder,"云附件下载链接.txt"),meta=Path.Combine(folder,"metadata.json");
            if(!File.Exists(links)||!File.Exists(meta))continue;
            var record=JsonSerializer.Deserialize<ArchiveRecord>(File.ReadAllText(meta));
            if(record is null)continue;
            foreach(string link in File.ReadLines(links).Where(l=>Uri.TryCreate(l,UriKind.Absolute,out var u)&&u.Scheme=="https").Distinct())
                rows.Add(string.Join(',',new[]{record.Subject,record.Sender,record.Account,(record.ReceivedAt??record.SentAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),record.Rule,folder,link,"仅记录链接，尚未下载"}.Select(Cell)));
        }
        string path=Path.Combine(root,"云附件汇总.csv"),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllLines(temp,rows,new UTF8Encoding(true));
        File.Move(temp,path,true);
    }
}
