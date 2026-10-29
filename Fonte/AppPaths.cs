using System;
using System.Diagnostics;
using System.IO;

namespace Raiz {
public static class AppPaths {
    public static string DataFolder(string applicationFolder,string environmentFolder) {
        if(!string.IsNullOrWhiteSpace(environmentFolder))return Path.GetFullPath(environmentFolder);
        string reference=Path.Combine(applicationFolder,"dados-local.txt");
        if(File.Exists(reference)) {
            string value=File.ReadAllText(reference,System.Text.Encoding.UTF8).Trim();
            if(!Path.IsPathRooted(value)||!Directory.Exists(value))throw new ApiException("A pasta dos seus dados não foi encontrada. Preserve a pasta da versão anterior ou extraia o pacote portátil completo.");
            return Path.GetFullPath(value);
        }
        return Path.Combine(applicationFolder,"Dados");
    }
    public static bool PreviousInstanceRunning(string folder,string currentExe) {
        string oldExe=Path.Combine(Directory.GetParent(Path.GetFullPath(folder)).FullName,"Raiz.exe");int current=Process.GetCurrentProcess().Id;
        foreach(var process in Process.GetProcessesByName("Raiz"))using(process) {
            if(process.Id==current)continue;
            try{string path=process.MainModule.FileName;if(string.Equals(path,oldExe,StringComparison.OrdinalIgnoreCase)||string.Equals(path,currentExe,StringComparison.OrdinalIgnoreCase))return true;}catch{}
        }
        return false;
    }
    public static string MutexName(string folder){return "Local\\RaizData_"+WhatsAppRules.Hash(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant());}
}
}
