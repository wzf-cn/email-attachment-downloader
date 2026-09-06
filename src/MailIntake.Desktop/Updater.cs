using System.Diagnostics;
using System.Text.Json;

namespace MailIntake.Desktop;

internal static class Updater
{
    public static void Run()
    {
        using var gate=new Mutex(true,"Local\\MailIntakeUpdater",out bool created);
        if(!created){MessageBox.Show("另一个更新窗口已打开。","软件更新");return;}
        try
        {
            string source=Path.GetFullPath(AppContext.BaseDirectory);
            using var picker=new FolderBrowserDialog{Description="选择原软件所在文件夹（包含 MailIntake.exe）",UseDescriptionForTitle=true};
            if(picker.ShowDialog()!=DialogResult.OK)return;
            string target=Path.GetFullPath(picker.SelectedPath);
            if(Path.TrimEndingDirectorySeparator(source).Equals(Path.TrimEndingDirectorySeparator(target),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请将新版解压到另一个文件夹，再从新版文件夹运行更新。");
            if(!File.Exists(Path.Combine(target,"MailIntake.exe")))throw new InvalidOperationException("所选文件夹中没有 MailIntake.exe。");
            string[] files=JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(source,"update-files.json")))??[];
            if(files.Length==0)throw new InvalidOperationException("更新文件清单为空。");
            foreach(string file in files)
            {
                if(Path.IsPathRooted(file)||file.Split('/','\\').Any(p=>p is ".." or "." or ""))throw new InvalidOperationException("更新文件路径无效。");
                if(!File.Exists(Path.Combine(source,file)))throw new InvalidOperationException("更新包文件不完整："+file);
            }
            var processes=Process.GetProcessesByName("MailIntake").Where(p=>p.Id!=Environment.ProcessId).ToArray();
            try
            {
                var active=processes.Where(p=>string.Equals(p.MainModule?.FileName,Path.Combine(target,"MailIntake.exe"),StringComparison.OrdinalIgnoreCase)).ToArray();
                string question=active.Length>0
                    ?"原软件正在运行。是否退出原软件并继续更新？\n请选择“是”前确认已保存界面中的修改。邮件处理结束后才会替换程序文件。"
                    :"是否更新所选文件夹中的软件？邮箱配置、记录及下载文件将保留。";
                if(MessageBox.Show(question,"软件更新",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
                if(active.Length>0)
                {
                    if(!EventWaitHandle.TryOpenExisting("Local\\MailIntakeUpdateExit",out var signal))
                        throw new InvalidOperationException("当前运行的旧版本尚不支持自动退出。请从托盘退出旧版后重新运行更新；安装本版后即可自动退出。");
                    using(signal)signal.Set();
                    foreach(var process in active)if(!process.WaitForExit(60000))throw new InvalidOperationException("原软件尚未退出，本次未替换文件。请等待处理完成后重新更新。");
                }
            }
            finally{foreach(var process in processes)process.Dispose();}
            // Hold the application's single-instance gate throughout file replacement.
            using var appGate=new Mutex(false,"Local\\KeywordMailDownloader");
            bool acquired;
            try{acquired=appGate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
            if(!acquired)throw new InvalidOperationException("邮件软件仍在运行，本次未替换文件。请退出后重新更新。");
            try{Install(source,target,files);}finally{appGate.ReleaseMutex();}
            MessageBox.Show("更新完成。配置与数据已保留，可从原位置启动软件。","软件更新");
        }
        catch(Exception error){MessageBox.Show("更新未完成："+error.Message,"软件更新",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    internal static void Install(string source,string target,string[] files)
    {
        string backup=Path.Combine(Path.GetTempPath(),"MailIntake-update-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var copied=new List<(string File,bool Existed)>();
        try
        {
            foreach(string file in files)
            {
                string destination=Path.Combine(target,file), saved=Path.Combine(backup,file);
                bool existed=File.Exists(destination);
                if(existed){Directory.CreateDirectory(Path.GetDirectoryName(saved)!);File.Copy(destination,saved);}
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                copied.Add((file,existed));File.Copy(Path.Combine(source,file),destination,true);
            }
        }
        catch
        {
            foreach(var item in copied.AsEnumerable().Reverse())
            {
                string destination=Path.Combine(target,item.File);
                if(item.Existed)File.Copy(Path.Combine(backup,item.File),destination,true);
                else File.Delete(destination);
            }
            throw new IOException("文件替换失败，已恢复原程序。备份保留在："+backup);
        }
    }
}
