using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MailIntake.Desktop;

internal static class ReleaseUpdates
{
    internal record Release(Version Version,string Page,string Source);
    internal static readonly string[] Endpoints=["https://api.github.com/repos/wzf-cn/email-attachment-downloader/releases/latest","https://gitee.com/api/v5/repos/wzFeel/email-attachment-downloader/releases/latest"];
    internal static Version Current=>typeof(ReleaseUpdates).Assembly.GetName().Version!;
    private static string Preference=>Path.Combine(LocalSettings.Root,"disable-update-check");
    internal static bool Enabled=>!File.Exists(Preference);
    internal static void SetEnabled(bool value){Directory.CreateDirectory(LocalSettings.Root);if(value)File.Delete(Preference);else File.WriteAllText(Preference,"");}
    internal static Release? Parse(string json,bool github)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(root.TryGetProperty("draft",out var draft)&&draft.ValueKind==JsonValueKind.True)return null;
        if(root.TryGetProperty("prerelease",out var preview)&&preview.ValueKind==JsonValueKind.True)return null;
        string tag=root.GetProperty("tag_name").GetString()??"";
        if(!Regex.IsMatch(tag,@"^v?\d+\.\d+\.\d+$")||!Version.TryParse(tag.TrimStart('v'),out var version))return null;
        // Construct trusted repository URLs; never open a URL supplied by release text.
        return new Release(new Version(version.Major,version.Minor,version.Build,0),github?"https://github.com/wzf-cn/email-attachment-downloader/releases/tag/"+tag:"https://gitee.com/wzFeel/email-attachment-downloader/releases/tag/"+tag,github?"GitHub":"Gitee");
    }
    internal static async Task<(Release? Latest,int Available)> Check(CancellationToken token)
    {
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(12),MaxResponseContentBufferSize=1024*1024};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MailIntake/"+Current.ToString(3));
        async Task<(Release? Release,bool Available)> Fetch(int index)
        {
            try
            {
                using var response=await client.GetAsync(Endpoints[index],token);
                if(response.StatusCode==HttpStatusCode.NotFound)return (null,true);
                response.EnsureSuccessStatusCode();
                return (Parse(await response.Content.ReadAsStringAsync(token),index==0),true);
            }
            catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException){return (null,false);}
        }
        var results=await Task.WhenAll(Fetch(0),Fetch(1));
        return (results.Where(x=>x.Release!=null).Select(x=>x.Release).OrderByDescending(x=>x!.Version).FirstOrDefault(),results.Count(x=>x.Available));
    }
    internal static async Task Show(Form owner,bool manual,CancellationToken token)
    {
        var result=await Check(token);
        if(token.IsCancellationRequested||owner.IsDisposed)return;
        string L(string zh,string en)=>UiLanguage.English?en:zh;
        string title=L("软件更新","Software update");
        if(result.Latest is not {} release||release.Version<=Current)
        {
            if(manual)MessageBox.Show(owner,result.Available==0?L("暂时无法连接更新服务器，请稍后重试。","Update servers are unavailable. Please try again later."):result.Latest==null?L("尚未找到正式发布版本。","No published stable release was found."):L("当前已是最新版本。","You are up to date."),title);
            return;
        }
        string message=L($"发现新版本 {release.Version.ToString(3)}（当前 {Current.ToString(3)}）。\n\n是否打开 {release.Source} 官方发布页下载安装包？\n安装时会询问是否退出旧软件。邮箱配置和下载记录保留。",$"Version {release.Version.ToString(3)} is available (current: {Current.ToString(3)}).\n\nOpen the {release.Source} release page to download the update?\nThe installer will ask to close the app. Settings and records are preserved.");
        if(MessageBox.Show(owner,message,title,MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)
        {
            try{Process.Start(new ProcessStartInfo(release.Page){UseShellExecute=true});}
            catch{MessageBox.Show(owner,L("无法打开浏览器，请手动访问：","Could not open the browser. Please visit: ")+release.Page,title);}
        }
    }
}
