using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MailIntake.Desktop;

internal sealed class FeedbackForm : EditorForm
{
    private sealed record Draft(string Id,string Kind,string Title,string Detail,string Contact);
    private const string DefaultEndpoint="https://wuzhuofei.com/mail-feedback/api/feedback";
    public FeedbackForm():base("意见反馈")
    {
        string folder=Path.Combine(LocalSettings.Root,"Feedback"),draftPath=Path.Combine(folder,"draft.json");
        Draft draft=new(Guid.NewGuid().ToString(),"使用问题","","","");
        if(File.Exists(draftPath))try{draft=JsonSerializer.Deserialize<Draft>(File.ReadAllText(draftPath))??draft;}catch{}
        string requestId=draft.Id;
        var kind=Choice("反馈类型",draft.Kind,"使用问题","功能建议","界面体验","其他");
        var title=TextField("标题",draft.Title);
        var detail=TextField("具体说明",draft.Detail,false,6);
        var contact=TextField("联系方式（选填）",draft.Contact);
        Field("发送说明",new Label{AutoSize=true,Text="反馈将直接提交给软件开发者，无需邮箱或 Gitee 账号。提交内容包括上方填写的信息和软件版本；不会自动附带邮箱配置、邮件、附件或日志。"});
        var result=Field("提交状态",new Label{AutoSize=true,ForeColor=Design.Muted,Text="请勿填写授权码、秘钥或他人的个人信息。"});
        var save=new ActionButton{Text="保存到本地",Width=150,Height=38};
        var send=new ActionButton{Text="提交反馈",Type=AntdUI.TTypeMini.Primary,Width=140,Height=38};
        Footer.Controls.Add(save);Footer.Controls.Add(send);
        bool sending=false,submitted=false,edited=false;
        void Changed(object? sender,EventArgs args){if(!edited){requestId=Guid.NewGuid().ToString();edited=true;}submitted=false;if(!sending)send.Enabled=true;}
        kind.SelectedIndexChanged+=Changed;title.TextChanged+=Changed;detail.TextChanged+=Changed;contact.TextChanged+=Changed;
        Draft Current()=>new(requestId,kind.Text,title.Text,detail.Text,contact.Text);
        void SaveDraft(){Directory.CreateDirectory(folder);File.WriteAllText(draftPath+".tmp",JsonSerializer.Serialize(Current()));File.Move(draftPath+".tmp",draftPath,true);}
        void Validate()
        {
            if(string.IsNullOrWhiteSpace(title.Text)||string.IsNullOrWhiteSpace(detail.Text))throw new ArgumentException("请填写标题和具体说明。");
            if(title.Text.Length>120||detail.Text.Length>10000||contact.Text.Length>200)throw new ArgumentException("标题最多 120 字，说明最多 10000 字，联系方式最多 200 字。");
        }
        save.Click+=(_,_)=>
        {
            try
            {
                SaveDraft();
                using var picker=new SaveFileDialog{Title="保存意见反馈",Filter="反馈文件|*.txt",FileName="意见反馈-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt",InitialDirectory=folder,OverwritePrompt=true};
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                File.WriteAllText(picker.FileName,$"标题：{title.Text}\n类型：{kind.Text}\n联系方式：{contact.Text}\n版本：{Application.ProductVersion.Split('+')[0]}\n\n{detail.Text}",new UTF8Encoding(true));
                result.Text="已保存到本地，尚未提交。";
            }
            catch{result.Text="本地保存失败，请检查目录权限或手动复制内容。";}
        };
        send.Click+=async(_,_)=>
        {
            if(sending)return;
            try{Validate();SaveDraft();}catch(Exception e){result.Text=e.Message;return;}
            sending=true;edited=false;send.Enabled=false;save.Enabled=false;Body.Enabled=false;
            result.Text="正在提交，请稍候…";
            try
            {
                var endpointPath=Path.Combine(LocalSettings.Root,"feedback-endpoint.txt");
                string address=File.Exists(endpointPath)?File.ReadAllText(endpointPath).Trim():DefaultEndpoint;
                if(!Uri.TryCreate(address,UriKind.Absolute,out var endpoint)||endpoint.Scheme!="https"||!string.IsNullOrEmpty(endpoint.UserInfo))throw new ArgumentException("反馈地址配置不正确，需要 HTTPS 地址。");
                using var handler=new HttpClientHandler{AllowAutoRedirect=false};
                using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(25)};
                var response=await client.PostAsJsonAsync(endpoint,new{id=requestId,kind=kind.Text,title=title.Text,detail=detail.Text,contact=contact.Text,version=Application.ProductVersion.Split('+')[0]});
                using(response)
                {
                    if(response.StatusCode==HttpStatusCode.TooManyRequests){result.Text="提交较频繁，请一小时后再试。草稿已保留。";return;}
                    if(!response.IsSuccessStatusCode){result.Text="暂未完成提交，草稿已保留。请稍后重试。";return;}
                    var body=await response.Content.ReadFromJsonAsync<JsonElement>();
                    if(!body.TryGetProperty("accepted",out var accepted)||!accepted.GetBoolean()||body.GetProperty("id").GetString()!=requestId)throw new InvalidOperationException();
                    submitted=true;
                    result.Text="提交成功。反馈编号："+requestId;
                    try{File.Delete(draftPath);}catch{}
                }
            }
            catch(ArgumentException e){result.Text=e.Message;}
            catch{result.Text="未能确认提交结果，草稿已保留。重试相同内容不会重复登记。";}
            finally{sending=false;send.Enabled=!submitted;save.Enabled=true;Body.Enabled=true;}
        };
        FormClosing+=(_,e)=>
        {
            if(sending){e.Cancel=true;return;}
            if(!submitted&&(!string.IsNullOrWhiteSpace(title.Text)||!string.IsNullOrWhiteSpace(detail.Text)))
                try{SaveDraft();}catch{e.Cancel=true;result.Text="草稿保存失败，请先复制内容或清空后关闭。";}
        };
    }
}
