using MailIntake.Core;

namespace MailIntake.Desktop;

internal static class TemplateFiles
{
    public static string Folder
    {
        get{string path=Path.Combine(LocalSettings.Root,"RuleTemplates");Directory.CreateDirectory(path);return path;}
    }
    public static void Save(IWin32Window owner,MailRule rule)
    {
        string name=new string(rule.Name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray()).Trim(' ','.');
        using var picker=new SaveFileDialog{Title="保存规则模板",Filter="规则模板|*.mailrule.json",DefaultExt="mailrule.json",AddExtension=true,InitialDirectory=Folder,FileName=(name.Length==0?"规则模板":name)+".mailrule.json",OverwritePrompt=true};
        if(picker.ShowDialog(owner)!=DialogResult.OK)return;
        RuleTemplates.Save(picker.FileName,rule);
        MessageBox.Show(owner,"模板已保存。以后点击“从模板新增”即可复用。\n格式、下载设置及回复内容会保留；名单、固定秘钥和下载目录需按本次任务填写。","规则模板");
    }
    public static MailRule? Load(IWin32Window owner)
    {
        using var picker=new OpenFileDialog{Title="选择要使用的规则模板",Filter="规则模板|*.mailrule.json|JSON 文件|*.json",InitialDirectory=Folder};
        return picker.ShowDialog(owner)==DialogResult.OK?RuleTemplates.Load(picker.FileName):null;
    }
}
