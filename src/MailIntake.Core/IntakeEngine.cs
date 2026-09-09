using MimeKit;
using MimeKit.Utils;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MailIntake.Core;

public sealed class IntakeEngine(StateStore store,IReplySender sender)
{
    public static bool CanReply(Incoming incoming,ISet<string> own)
    {
        var m=incoming.Header;
        if(string.IsNullOrWhiteSpace(incoming.Sender) || !MailboxAddress.TryParse(incoming.Sender,out _)) return false;
        if(own.Contains(incoming.Sender.ToLowerInvariant())) return false;
        string auto=m.Headers["Auto-Submitted"]??"no";
        if(!auto.Equals("no",StringComparison.OrdinalIgnoreCase)) return false;
        if(m.Headers.Contains("List-Id") || m.Headers.Contains("X-Auto-Response-Suppress")) return false;
        if((m.Headers["Return-Path"]??"").Trim()=="<>") return false;
        if(new[]{"bulk","list","junk"}.Contains((m.Headers["Precedence"]??"").ToLowerInvariant())) return false;
        return !new[]{"no-reply","noreply","mailer-daemon","postmaster"}.Contains(incoming.Sender.Split('@')[0].ToLowerInvariant());
    }
    public async Task<bool> ProcessAsync(MailAccount account,Incoming incoming,Settings settings,Action<string> log,CancellationToken token,bool reexport=false,ISet<string>? dueRules=null)
    {
        token.ThrowIfCancellationRequested();
        incoming=incoming with {Subject=SubjectEncoding.Repair(incoming.Subject)};
        var eligible=account.Rules.Where(r=>RuleTimeRange.Contains(r,account,incoming.ReceivedAt??incoming.Header.Date)).ToList();
        if(eligible.Count==0){log("跳过（不在规则时间范围）："+incoming.Subject);return false;}
        string from=incoming.Sender.ToLowerInvariant();
        bool handled=store.IsHandled(incoming.Id);
        if(store.IsBlocked(from)) { if(handled&&!reexport)return false; if(!reexport)store.Mark(incoming.Id,"Blocked",from,account.Address); return !reexport; }
        MimeMessage? loaded=null;
        bool repaired=false;
        if(handled&&!reexport&&incoming.Size<=settings.MaxMessageMb*1024L*1024L)
        {
            foreach(var rule in eligible.Where(r=>r.DownloadAttachments&&(dueRules is null||dueRules.Contains(r.Id))))
            {
                string id=Constants.Hash(incoming.Id+"|"+Path.GetFullPath(rule.Output).ToLowerInvariant());
                var record=store.FindArchive(id);
                if(record is null||record.Attachments.All(File.Exists))continue;
                loaded??=await incoming.Load(token);
                int count=await RestoreMissingAttachments(record,loaded,rule,token);
                if(count>0){repaired=true;log($"已补下载 {count} 个缺失附件：{incoming.Subject}");}
            }
        }
        if(!reexport && handled && (dueRules is null || !store.IsScheduled(incoming.Id))) return repaired;
        string searchBody="";string[] searchNames=[];
        if(eligible.Any(r=>r.SearchBody||r.SearchAttachmentNames))
        {
            if(incoming.Size>settings.MaxMessageMb*1024L*1024L)
            {
                if(!handled){store.Mark(incoming.Id,"Oversize",from,account.Address);store.StopScheduled(incoming.Id);store.Event("大小限制",from,account.Address,"邮件超过大小上限，无法检索正文或附件名："+incoming.Subject);log("异常：邮件超过大小上限，未接收内容进行关键词检索。");}
                return !handled;
            }
            loaded??=await incoming.Load(token);
            searchBody=loaded.TextBody??(loaded.HtmlBody is {} html?RuleValidator.HtmlText(html):"");
            searchNames=AttachmentExport.Parts(loaded).Select(p=>p.ContentDisposition?.FileName??p.ContentType.Name??"").Where(n=>n.Length>0).ToArray();
        }
        var candidates=eligible.Select(r=>(Rule:r,Result:RuleValidator.Match(incoming.Subject,r,searchBody,searchNames))).Where(x=>x.Result!=Validation.Ignore).ToList();
        if(candidates.Count==0){log("跳过（未满足关键词或检索范围）："+incoming.Subject);return false;}
        var allCandidates=candidates;
        if(dueRules!=null)
        {
            bool anyValid=candidates.Any(x=>x.Result==Validation.Success);
            candidates=candidates.Where(x=>dueRules.Contains(x.Rule.Id)&&(!anyValid||x.Result==Validation.Success)).ToList();
            if(candidates.Count==0)return false;
            if(handled&&!reexport)candidates=candidates.Where(x=>x.Result==Validation.Success&&!store.HasArchive(Constants.Hash(incoming.Id+"|"+Path.GetFullPath(x.Rule.Output).ToLowerInvariant()))).ToList();
            if(candidates.Count==0)return false;
            if(!reexport)store.TrackScheduled(incoming.Id);
        }
        var selected=candidates.FirstOrDefault(x=>x.Result==Validation.Success);
        if(selected.Rule is null) selected=candidates[0];
        bool valid=selected.Result==Validation.Success;
        if(reexport&&!valid)return false;
        bool canReply=!reexport&&!handled&&CanReply(incoming,settings.Accounts.Select(a=>a.Address.ToLowerInvariant()).ToHashSet());
        bool justBlocked=false;
        bool skippedLarge=false;
        if(!valid)
        {
            if(canReply) justBlocked=store.RegisterError(incoming.Id,from,account.Address,selected.Result.ToString(),settings.ErrorThreshold);
            else store.Mark(incoming.Id,"Suppressed",from,account.Address);
            if(justBlocked) log($"异常：{from} 连续错误主题达到 {Math.Clamp(settings.ErrorThreshold,1,20)} 次，已停收，需管理员重置。");
        }
        else
        {
            if(!reexport&&canReply)store.RegisterSuccess(incoming.Id,from);
            if(incoming.Size>settings.MaxMessageMb*1024L*1024L)
            {
                store.Mark(incoming.Id,"Oversize",from,account.Address);
                store.StopScheduled(incoming.Id);
                store.Event("大小限制",from,account.Address,$"邮件超过 {settings.MaxMessageMb} MB，未下载：{incoming.Subject}");
                log("异常：邮件超过大小上限，已记录，请管理员核对。"); return true;
            }
            var message=loaded??await incoming.Load(token);
            var directories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var match in candidates.Where(x=>x.Result==Validation.Success))
            {
                string output=Path.GetFullPath(match.Rule.Output);
                if(!directories.Add(output)) continue;
                string recordId=Constants.Hash(incoming.Id+"|"+output.ToLowerInvariant());
                if(!reexport && store.HasArchive(recordId)) continue;
                // Rules targeting the same directory contribute the union of their export choices.
                var effective=JsonSerializer.Deserialize<MailRule>(JsonSerializer.Serialize(match.Rule))!;
                var same=allCandidates.Where(x=>x.Result==Validation.Success&&Path.GetFullPath(x.Rule.Output).Equals(output,StringComparison.OrdinalIgnoreCase)).Select(x=>x.Rule).ToList();
                effective.DownloadBody=same.Any(r=>r.DownloadBody);
                effective.DownloadAttachments=same.Any(r=>r.DownloadAttachments);
                effective.SaveOriginal=same.Any(r=>r.SaveOriginal);
                effective.MaxAttachmentMb=same.Min(r=>r.MaxAttachmentMb);
                effective.FlatAttachments=same.Where(r=>r.DownloadAttachments).All(r=>r.FlatAttachments);
                var record=await ArchiveAsync(account,incoming,message,effective,recordId,token);
                store.Archive(record);
                if(record.SkippedAttachments.Count>0){skippedLarge=true;store.Event("附件超过上限",from,account.Address,record.Directory+"："+record.SkippedAttachments.Count+" 个附件未保存，请查看附件跳过记录.json。");log("异常：附件超过大小上限，仅记录，未保存超限文件及完整 EML。");}
                if(effective.DownloadAttachments&&AttachmentExport.CloudLinks(message).Count>0)
                {
                    try{CloudAttachmentSummary.Write(output);}
                    catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException)
                    {store.Event("云附件汇总失败",from,account.Address,output+"："+e.GetType().Name);log("异常：云附件汇总更新失败，请关闭占用汇总表的程序后重新导出；单封邮件的链接已保留。");}
                }
                if(!reexport&&match.Rule.DetectAnomaly) CheckAnomaly(incoming,message,record.Directory,account.Address,log);
                if(effective.DownloadAttachments&&AttachmentExport.CloudLinks(message).Count>0)log("异常：邮件包含云附件链接，文件本身不在邮件中；请查看导出目录中的云附件下载链接.txt。");
                log(account.Address+"：已保存至 "+record.Directory);
            }
        }
        if(canReply && (justBlocked || selected.Rule.ReplyEnabled))
        {
            string body=justBlocked?Constants.BlockReply:valid?selected.Rule.SuccessReply:Constants.ErrorReply;
            if(valid&&skippedLarge)body="邮件已收到，但有附件超过接收大小上限，未保存这些附件。请缩小附件后重新提交，或联系管理员。";
            string kind=justBlocked?"停收通知":valid?"成功回复":"统一错误回复";
            string messageId=MimeUtils.GenerateMessageId();
            string reserved=store.ReserveReply(incoming.Id,from,account.Address,kind,messageId,settings.MaxRepliesPerHour);
            if(reserved=="Sending")
            {
                // A crash/cancellation after this point is deliberately never auto-retried.
                store.Mark(incoming.Id,"ReplyReserved",from,account.Address);
                try
                {
                    await sender.SendAsync(account,incoming,incoming.Sender,body,messageId,token);
                    store.SetReplyStatus(incoming.Id,"Sent"); log(kind+"已由 SMTP 服务器接受。");
                }
                catch(Exception)
                {
                    store.SetReplyStatus(incoming.Id,"Uncertain");
                    store.Event("回复待核实",from,account.Address,kind+"发送结果不确定，不会自动重发；停收状态不受影响。");
                    throw;
                }
            }
            else if(reserved=="RateLimited")
            {
                store.Event("回复限额",from,account.Address,kind+"超过全局每小时限额，未发送且不会自动补发。");
                log("异常：达到全局回复限额，未发送，请管理员查看记录。");
            }
        }
        if(!reexport)store.Mark(incoming.Id,valid?"Archived":"Invalid",from,account.Address);
        return true;
    }

    private static string SafeName(string value)
    {
        var invalid=Path.GetInvalidFileNameChars();
        value=new string(value.Select(c=>invalid.Contains(c)||char.IsControl(c)?'_':c).ToArray()).Trim(' ','.');
        return string.IsNullOrEmpty(value)?"attachment.bin":value[..Math.Min(value.Length,90)];
    }
    private static async Task<int> RestoreMissingAttachments(ArchiveRecord record,MimeMessage message,MailRule rule,CancellationToken token)
    {
        string root=Path.GetFullPath(rule.Output).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        int index=0,count=0;
        foreach(var attachment in AttachmentExport.Parts(message))
        {
            string original=attachment.ContentDisposition?.FileName??attachment.ContentType.Name??"未命名附件";
            // Export numbering excludes attachments skipped under the original size limit.
            if(record.SkippedAttachments.Any(s=>s.Name==original))continue;
            string name=$"attachment_{++index:000}_"+SafeName(attachment.ContentDisposition?.FileName??attachment.ContentType.Name??"attachment.bin");
            var target=record.Attachments.FirstOrDefault(f=>Path.GetFileName(f)==name||Path.GetFileName(f).EndsWith("_"+name,StringComparison.Ordinal));
            if(target is null&&rule.NaturalLayout&&index<=record.Attachments.Count)target=record.Attachments[index-1];
            if(target is null||File.Exists(target))continue;
            target=Path.GetFullPath(target);
            if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))continue;
            if(await AttachmentExport.Size(attachment,token)>rule.MaxAttachmentMb*1024L*1024L)continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string temporary=target+"."+Guid.NewGuid().ToString("N")+".partial";
            try
            {
                await using(var stream=File.Create(temporary))
                {
                    if(attachment is MessagePart { Message: not null } embedded)await embedded.Message.WriteToAsync(stream,token);
                    else if(attachment is MimePart { Content: not null } part)await part.Content.DecodeToAsync(stream,token);
                }
                File.Move(temporary,target,false);count++;
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        return count;
    }
    public static async Task<ArchiveRecord> ArchiveAsync(MailAccount account,Incoming incoming,MimeMessage message,MailRule rule,string recordId,CancellationToken token)
    {
        incoming=incoming with {Subject=SubjectEncoding.Repair(incoming.Subject)};
        if(rule.NaturalLayout)return await NaturalArchiveAsync(account,incoming,message,rule,recordId,token);
        string parent=Path.GetFullPath(rule.Output); Directory.CreateDirectory(parent);
        string final=Path.Combine(parent,SafeName(incoming.Subject)+"_"+(incoming.ReceivedAt??message.Date).ToLocalTime().ToString("yyyyMMdd-HHmmss")+"_"+incoming.Id[..8]);
        string staging=Path.Combine(parent,"."+incoming.Id[..24]+"-"+Guid.NewGuid().ToString("N")+".partial");
        Directory.CreateDirectory(staging);
        var parts=AttachmentExport.Parts(message).ToList();
        var oversized=new Dictionary<MimeEntity,long>();
        foreach(var part in parts)
        {
            long size=await AttachmentExport.Size(part,token);
            if(size>Math.Max(1,rule.MaxAttachmentMb)*1024L*1024L)oversized[part]=size;
        }
        var skipped=oversized.Select(p=>new SkippedAttachment(p.Key.ContentDisposition?.FileName??p.Key.ContentType.Name??"未命名附件",p.Value,$"超过单个附件上限 {rule.MaxAttachmentMb} MB，未保存；完整 EML 同时跳过")).ToList();
        if(rule.SaveOriginal&&skipped.Count==0)await message.WriteToAsync(Path.Combine(staging,"original.eml"),token);
        if(skipped.Count>0)await File.WriteAllTextAsync(Path.Combine(staging,"附件跳过记录.json"),JsonSerializer.Serialize(skipped,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}),token);
        if(rule.DownloadBody)
        {
            await File.WriteAllTextAsync(Path.Combine(staging,"body.txt"),message.TextBody??"",token);
            if(message.HtmlBody is { } html) await File.WriteAllTextAsync(Path.Combine(staging,"body.html"),html,token);
        }
        var files=new List<string>(); int index=0;
        foreach(var attachment in rule.DownloadAttachments?AttachmentExport.Parts(message):[])
        {
            if(oversized.ContainsKey(attachment))continue;
            string name=$"attachment_{++index:000}_"+SafeName(attachment.ContentDisposition?.FileName??attachment.ContentType.Name??"attachment.bin");
            await using var stream=File.Create(Path.Combine(staging,name));
            if(attachment is MessagePart { Message: not null } embedded) await embedded.Message.WriteToAsync(stream,token);
            else if(attachment is MimePart { Content: not null } part) await part.Content.DecodeToAsync(stream,token);
            files.Add(Path.Combine(final,name));
        }
        var links=rule.DownloadAttachments?AttachmentExport.CloudLinks(message):[];
        if(links.Count>0)await File.WriteAllLinesAsync(Path.Combine(staging,"云附件下载链接.txt"),new[]{"此邮件包含云附件。文件本身不在 EML 中，尚未下载。请在浏览器中打开以下链接；可能需要登录或链接已过期。"}.Concat(links),token);
        if(rule.DownloadAttachments&&files.Count==0)await File.WriteAllTextAsync(Path.Combine(staging,"附件说明.txt"),skipped.Count>0?"存在超过上限的附件，未保存。请查看附件跳过记录.json。":links.Count>0?"本邮件没有随信文件附件，但包含云附件链接，请查看云附件下载链接.txt。":"本邮件未检测到随信文件附件。若邮箱界面显示下载入口，可能是在线链接，请查看正文或原始邮件。",token);
        // Never overwrite an existing incomplete or manually edited directory.
        if(Directory.Exists(final)) final+="-重新导出-"+Guid.NewGuid().ToString("N")[..8];
        files=files.Select(f=>Path.Combine(final,Path.GetFileName(f))).ToList();
        if(rule.FlatAttachments&&files.Count>0)
        {
            string shared=Path.Combine(parent,"全部附件");Directory.CreateDirectory(shared);
            var sharedFiles=new List<string>();
            string title=SafeName(incoming.Subject);title=title[..Math.Min(title.Length,40)];
            foreach(string file in files)
            {
                string name=Path.GetFileName(file);
                string destination=Path.Combine(shared,title+"_"+incoming.Id[..8]+"_"+Guid.NewGuid().ToString("N")[..8]+"_"+name);
                File.Move(Path.Combine(staging,name),destination);
                sharedFiles.Add(destination);
            }
            files=sharedFiles;
            await File.WriteAllLinesAsync(Path.Combine(staging,"附件位置.txt"),new[]{"本邮件附件集中存放在："}.Concat(files),token);
        }
        var record=new ArchiveRecord(recordId,account.Address,incoming.Sender,incoming.Subject,rule.Name,final,incoming.ReceivedAt,message.Date,DateTimeOffset.UtcNow,message.MessageId??"",files){SkippedAttachments=skipped};
        await File.WriteAllTextAsync(Path.Combine(staging,"metadata.json"),JsonSerializer.Serialize(record,new JsonSerializerOptions{WriteIndented=true}),token);
        Directory.Move(staging,final); return record;
    }
    private static async Task<ArchiveRecord> NaturalArchiveAsync(MailAccount account,Incoming incoming,MimeMessage message,MailRule rule,string id,CancellationToken token)
    {
        string root=Path.GetFullPath(rule.Output);
        var internalRule=JsonSerializer.Deserialize<MailRule>(JsonSerializer.Serialize(rule))!;
        internalRule.NaturalLayout=false;internalRule.FlatAttachments=false;internalRule.Output=Path.Combine(root,"收件记录");
        var record=await ArchiveAsync(account,incoming,message,internalRule,id,token);
        var files=new List<string>();
        foreach(var source in record.Attachments)
        {
            string name=Regex.Replace(Path.GetFileName(source),@"^attachment_\d+_","");
            name=SafeName(SubjectEncoding.Repair(name));
            string extension=Path.GetExtension(name).ToLowerInvariant();
            string folder=new[]{".zip",".7z",".rar"}.Contains(extension)?root:Path.Combine(root,SafeName(incoming.Subject));
            Directory.CreateDirectory(folder);
            byte[] hash;using(var stream=File.OpenRead(source))hash=SHA256.HashData(stream);
            string? destination=null;
            foreach(string existing in Directory.EnumerateFiles(folder,"*",SearchOption.TopDirectoryOnly))
            {
                if(new FileInfo(existing).Length!=new FileInfo(source).Length)continue;
                using var stream=File.OpenRead(existing);
                if(SHA256.HashData(stream).SequenceEqual(hash)){destination=existing;break;}
            }
            if(destination is null)
            {
                destination=Path.Combine(folder,name);
                if(File.Exists(destination))destination=Path.Combine(folder,Path.GetFileNameWithoutExtension(name)+"_"+Convert.ToHexStringLower(hash)[..12]+extension);
                int suffix=1;string basis=destination;while(File.Exists(destination))destination=Path.Combine(folder,Path.GetFileNameWithoutExtension(basis)+"_"+(suffix++)+extension);
                File.Move(source,destination);
            }
            else File.Delete(source);
            files.Add(destination);
        }
        record=record with {Attachments=files};
        await File.WriteAllTextAsync(Path.Combine(record.Directory,"metadata.json"),JsonSerializer.Serialize(record,new JsonSerializerOptions{WriteIndented=true}),token);
        await File.WriteAllLinesAsync(Path.Combine(record.Directory,"附件位置.txt"),files,token);
        return record;
    }
    private void CheckAnomaly(Incoming incoming,MimeMessage message,string directory,string account,Action<string> log)
    {
        string text=message.TextBody??Regex.Replace(message.HtmlBody??"","<[^>]+>"," ");
        text=Regex.Replace(text,"\\s+"," ").Trim(); text=text[..Math.Min(text.Length,20000)];
        var hashes=new List<string>();
        foreach(var attachment in AttachmentExport.Parts(message))
        {
            using var stream=new MemoryStream();
            if(attachment is MimePart { Content: not null } part) part.Content.DecodeTo(stream);
            else if(attachment is MessagePart { Message: not null } embedded) embedded.Message.WriteTo(stream);
            hashes.Add(Convert.ToHexStringLower(SHA256.HashData(stream.ToArray())));
        }
        hashes.Sort(StringComparer.Ordinal); string joined=string.Join(",",hashes);
        foreach(var previous in store.Previous(incoming.Subject.Trim()))
        {
            if(previous.Sender.Equals(incoming.Sender,StringComparison.OrdinalIgnoreCase)) continue;
            bool bodyChanged=Similarity(previous.Text,text)<0.55;
            if(!bodyChanged && previous.Hashes==joined) continue;
            string reason=bodyChanged?"正文相似度低于 55%":"附件内容发生变化，需人工核对";
            store.Event("内容异常",incoming.Sender,account,$"同主题由不同邮箱提交：{incoming.Subject}\n{reason}\n此前：{previous.Sender}\n{previous.Directory}\n本次：{directory}");
            log("异常：同主题不同邮箱提交存在内容差异，请查看异常记录。");
            break;
        }
        store.Fingerprint(incoming.Id,incoming.Subject.Trim(),incoming.Sender,text,joined,directory);
    }
    public static double Similarity(string a,string b)
    {
        if(a==b) return 1;
        static HashSet<string> Grams(string s) => s.Length<2?[s]:Enumerable.Range(0,s.Length-1).Select(i=>s.Substring(i,2)).ToHashSet();
        var x=Grams(a); var y=Grams(b); return 2.0*x.Intersect(y).Count()/(x.Count+y.Count);
    }
}
