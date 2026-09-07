using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace MailIntake.Core;

public sealed class StateStore
{
    private readonly string connectionString;
    public StateStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource=path, DefaultTimeout=10, Pooling=false }.ToString();
        using var db=Open();
        using var command=db.CreateCommand();
        command.CommandText="""
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS scheduled_messages (id TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS handled (id TEXT PRIMARY KEY, status TEXT, sender TEXT, account TEXT, time TEXT);
            CREATE TABLE IF NOT EXISTS senders (sender TEXT PRIMARY KEY, errors INTEGER NOT NULL, blocked INTEGER NOT NULL, updated TEXT);
            CREATE TABLE IF NOT EXISTS replies (id TEXT PRIMARY KEY, status TEXT, sender TEXT, account TEXT, kind TEXT, messageId TEXT, time TEXT);
            CREATE TABLE IF NOT EXISTS archives (id TEXT PRIMARY KEY, metadata TEXT);
            CREATE TABLE IF NOT EXISTS events (id INTEGER PRIMARY KEY AUTOINCREMENT, time TEXT, kind TEXT, sender TEXT, account TEXT, detail TEXT);
            CREATE TABLE IF NOT EXISTS fingerprints (id TEXT PRIMARY KEY, subject TEXT, sender TEXT, text TEXT, hashes TEXT, directory TEXT);
            CREATE TABLE IF NOT EXISTS subject_success (id TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS policy_migrations (name TEXT PRIMARY KEY);
            BEGIN IMMEDIATE;
            UPDATE senders SET errors=0 WHERE blocked=0 AND NOT EXISTS (SELECT 1 FROM policy_migrations WHERE name='consecutive-errors-v1');
            INSERT OR IGNORE INTO policy_migrations VALUES ('consecutive-errors-v1');
            COMMIT;
            CREATE INDEX IF NOT EXISTS fingerprint_subject ON fingerprints(subject);
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db=new SqliteConnection(connectionString); db.Open(); return db; }
    private static string Now => DateTimeOffset.UtcNow.ToString("O");
    private static SqliteCommand Cmd(SqliteConnection db,string sql, params object?[] args)
    {
        var c=db.CreateCommand(); c.CommandText=sql;
        for(int i=0;i<args.Length;i++) c.Parameters.AddWithValue("$p"+i,args[i]??DBNull.Value);
        return c;
    }
    public void StopScheduled(string id)
    {using var db=Open();using var c=Cmd(db,"DELETE FROM scheduled_messages WHERE id=$p0",id);c.ExecuteNonQuery();}
    public bool IsScheduled(string id)
    {using var db=Open();using var c=Cmd(db,"SELECT 1 FROM scheduled_messages WHERE id=$p0",id);return c.ExecuteScalar()!=null;}
    public void TrackScheduled(string id)
    {using var db=Open();using var c=Cmd(db,"INSERT OR IGNORE INTO scheduled_messages VALUES ($p0)",id);c.ExecuteNonQuery();}
    public bool IsHandled(string id)
    {
        using var db=Open(); using var c=Cmd(db,"SELECT 1 FROM handled WHERE id=$p0",id); return c.ExecuteScalar()!=null;
    }
    public bool IsBlocked(string sender)
    {
        using var db=Open(); using var c=Cmd(db,"SELECT blocked FROM senders WHERE sender=$p0",sender.ToLowerInvariant());
        return Convert.ToInt32(c.ExecuteScalar()??0)==1;
    }
    public void Mark(string id,string status,string sender,string account)
    {
        using var db=Open(); using var c=Cmd(db,"INSERT OR IGNORE INTO handled VALUES ($p0,$p1,$p2,$p3,$p4)",id,status,sender,account,Now); c.ExecuteNonQuery();
    }
    public bool RegisterError(string id,string sender,string account,string reason,int threshold=3)
    {
        sender=sender.ToLowerInvariant();
        using var db=Open(); using var tx=db.BeginTransaction();
        using var seen=Cmd(db,"SELECT 1 FROM handled WHERE id=$p0",id);
        if(seen.ExecuteScalar()!=null) { tx.Commit(); return false; }
        using(var c=Cmd(db,"INSERT INTO senders VALUES ($p0,1,0,$p1) ON CONFLICT(sender) DO UPDATE SET errors=errors+1,updated=excluded.updated",sender,Now)) c.ExecuteNonQuery();
        using var get=Cmd(db,"SELECT errors FROM senders WHERE sender=$p0",sender);
        int count=Convert.ToInt32(get.ExecuteScalar());
        using(var c=Cmd(db,"INSERT INTO handled VALUES ($p0,$p1,$p2,$p3,$p4)",id,reason,sender,account,Now)) c.ExecuteNonQuery();
        if(count>=Math.Clamp(threshold,1,20))
        {
            using(var c=Cmd(db,"UPDATE senders SET blocked=1 WHERE sender=$p0",sender)) c.ExecuteNonQuery();
            using(var c=Cmd(db,"INSERT INTO events(time,kind,sender,account,detail) VALUES ($p0,$p1,$p2,$p3,$p4)",Now,"停收",sender,account,$"连续错误主题达到 {Math.Clamp(threshold,1,20)} 次，已停止处理该发件邮箱所有来信。")) c.ExecuteNonQuery();
        }
        tx.Commit(); return count>=Math.Clamp(threshold,1,20);
    }
    public void RegisterSuccess(string id,string sender)
    {
        using var db=Open();using var tx=db.BeginTransaction();
        using var insert=Cmd(db,"INSERT OR IGNORE INTO subject_success VALUES ($p0)",id);
        if(insert.ExecuteNonQuery()>0)
        {
            using var reset=Cmd(db,"UPDATE senders SET errors=0,updated=$p1 WHERE sender=$p0 AND blocked=0 AND errors>0",sender.ToLowerInvariant(),Now);
            reset.ExecuteNonQuery();
        }
        tx.Commit();
    }
    public void Reset(string sender)
    {
        sender=sender.ToLowerInvariant();
        using var db=Open(); using var tx=db.BeginTransaction();
        using(var c=Cmd(db,"UPDATE senders SET errors=0,blocked=0,updated=$p1 WHERE sender=$p0",sender,Now)) c.ExecuteNonQuery();
        using(var c=Cmd(db,"INSERT INTO events(time,kind,sender,account,detail) VALUES ($p0,$p1,$p2,$p3,$p4)",Now,"管理员重置",sender,"",$"本机用户 {Environment.UserName} 恢复接收并清零错误次数，保留历史记录。")) c.ExecuteNonQuery();
        tx.Commit();
    }
    public void Event(string kind,string sender,string account,string detail)
    {
        using var db=Open(); using var c=Cmd(db,"INSERT INTO events(time,kind,sender,account,detail) VALUES ($p0,$p1,$p2,$p3,$p4)",Now,kind,sender,account,detail); c.ExecuteNonQuery();
    }
    public string ReserveReply(string id,string sender,string account,string kind,string messageId,int hourlyLimit)
    {
        using var db=Open(); using var tx=db.BeginTransaction();
        using var prior=Cmd(db,"SELECT status FROM replies WHERE id=$p0",id);
        if(prior.ExecuteScalar()!=null) { tx.Commit(); return "AlreadyRecorded"; }
        using var count=Cmd(db,"SELECT COUNT(*) FROM replies WHERE time>=$p0 AND status <> 'RateLimited'",DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
        string status=Convert.ToInt64(count.ExecuteScalar())>=hourlyLimit?"RateLimited":"Sending";
        using(var c=Cmd(db,"INSERT INTO replies VALUES ($p0,$p1,$p2,$p3,$p4,$p5,$p6)",id,status,sender,account,kind,messageId,Now)) c.ExecuteNonQuery();
        tx.Commit(); return status;
    }
    public void SetReplyStatus(string id,string status)
    { using var db=Open(); using var c=Cmd(db,"UPDATE replies SET status=$p1 WHERE id=$p0",id,status); c.ExecuteNonQuery(); }
    public bool HasArchive(string id)
    {
        using var db=Open(); using var c=Cmd(db,"SELECT metadata FROM archives WHERE id=$p0",id);
        var json=c.ExecuteScalar() as string;
        return json!=null && File.Exists(Path.Combine(JsonSerializer.Deserialize<ArchiveRecord>(json)!.Directory,"metadata.json"));
    }
    public void Archive(ArchiveRecord record)
    { using var db=Open(); using var c=Cmd(db,"INSERT OR REPLACE INTO archives VALUES ($p0,$p1)",record.Id,JsonSerializer.Serialize(record)); c.ExecuteNonQuery(); }
    public List<ArchiveRecord> Archives()
    {
        using var db=Open(); using var c=Cmd(db,"SELECT metadata FROM archives ORDER BY rowid DESC"); using var r=c.ExecuteReader();
        var list=new List<ArchiveRecord>(); while(r.Read()) list.Add(JsonSerializer.Deserialize<ArchiveRecord>(r.GetString(0))!); return list;
    }
    public List<SenderState> Senders()
    {
        using var db=Open(); using var c=Cmd(db,"SELECT sender,errors,blocked,updated FROM senders ORDER BY blocked DESC,errors DESC"); using var r=c.ExecuteReader();
        var list=new List<SenderState>(); while(r.Read()) list.Add(new(r.GetString(0),r.GetInt32(1),r.GetInt32(2)==1,r.GetString(3))); return list;
    }
    public List<EventRecord> Events()
    {
        using var db=Open(); using var c=Cmd(db,"SELECT id,time,kind,sender,account,detail FROM events ORDER BY id DESC LIMIT 2000"); using var r=c.ExecuteReader();
        var list=new List<EventRecord>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5))); return list;
    }
    public List<ReplyRecord> Replies()
    {
        using var db=Open(); using var c=Cmd(db,"SELECT id,status,sender,account,kind,messageId,time FROM replies ORDER BY rowid DESC"); using var r=c.ExecuteReader();
        var list=new List<ReplyRecord>(); while(r.Read()) list.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6))); return list;
    }
    public List<(string Sender,string Text,string Hashes,string Directory)> Previous(string subject)
    {
        using var db=Open(); using var c=Cmd(db,"SELECT sender,text,hashes,directory FROM fingerprints WHERE subject=$p0 ORDER BY rowid DESC LIMIT 100",subject); using var r=c.ExecuteReader();
        var list=new List<(string,string,string,string)>(); while(r.Read()) list.Add((r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3))); return list;
    }
    public void Fingerprint(string id,string subject,string sender,string text,string hashes,string directory)
    { using var db=Open(); using var c=Cmd(db,"INSERT OR IGNORE INTO fingerprints VALUES ($p0,$p1,$p2,$p3,$p4,$p5)",id,subject,sender,text,hashes,directory); c.ExecuteNonQuery(); }
    public void ImportSender(string sender,int errors,bool blocked)
    {
        using var db=Open(); using var c=Cmd(db,"INSERT INTO senders VALUES ($p0,$p1,$p2,$p3) ON CONFLICT(sender) DO UPDATE SET errors=MAX(errors,excluded.errors),blocked=MAX(blocked,excluded.blocked)",sender.ToLowerInvariant(),errors,blocked?1:0,Now); c.ExecuteNonQuery();
    }
}
