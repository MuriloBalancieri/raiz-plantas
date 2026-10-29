using System;using System.Drawing;using System.IO;using System.Linq;using System.Text;using System.Threading.Tasks;using System.Windows.Forms;
namespace Raiz {
public partial class MainForm {
    DateTime? invalidSensorUtc;ReadableCard sensorHealthCard;bool transferBusy,reportBusy,sensorHealthOnlyWarning;
    void AddSensorHealth(TableLayoutPanel stack,bool onlyWarning=false){sensorHealthOnlyWarning=onlyWarning;sensorHealthCard=new ReadableCard();Add(stack,sensorHealthCard,112);UpdateSensorHealth();}
    void UpdateSensorHealth(){if(sensorHealthCard==null||sensorHealthCard.IsDisposed)return;var a=SensorHealth.Evaluate(samples,last,store.Settings.Calibration,serial!=null&&serial.IsOpen,demo,DateTime.UtcNow,invalidSensorUtc);sensorHealthCard.Visible=!sensorHealthOnlyWarning||a.Severity=="attention";sensorHealthCard.SetText(a.Title,a.Body);}
    void ObserveSensorLine(string line,Sample parsed,DateTime now){if(parsed!=null)return;if((line??"").TrimStart().StartsWith("{")||(line??"").StartsWith("Solo (")||(line??"").StartsWith("Luz (")){
        // The first line of the legacy two-line protocol is intentionally incomplete.
        if((line??"").StartsWith("Luz (")&&!line.Contains("-"))return;invalidSensorUtc=now;
    }}
    bool ExportPathAllowed(string path){string target=Path.GetFullPath(path),folder=Path.GetFullPath(store.Folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(target.StartsWith(folder,StringComparison.OrdinalIgnoreCase)){MessageBox.Show(this,"Escolha uma pasta fora dos dados ativos para não substituir arquivos do app.","Local de exportação");return false;}return true;}
    async void ExportCareReport(bool pdf){
        if(reportBusy)return;var plant=Active;if(plant==null){MessageBox.Show(this,"Escolha uma planta para gerar seu relatório.");return;}
        int days=historyPeriod==null||historyPeriod.IsDisposed?7:new[]{7,14,30}[historyPeriod.SelectedIndex];
        using(var dialog=new SaveFileDialog{Filter=pdf?"Relatório PDF|*.pdf":"Resumo CSV|*.csv",FileName="Raiz_relatorio_"+DateTime.Now.ToString("yyyy-MM-dd")+(pdf?".pdf":".csv")}){
            if(dialog.ShowDialog(this)!=DialogResult.OK||!ExportPathAllowed(dialog.FileName))return;
            string path=dialog.FileName;var copy=Json.Read<Plant>(Json.Write(plant));var changes=store.Settings.CalibrationChanges.ToArray();var now=DateTime.UtcNow;reportBusy=true;Status("Preparando relatório dos últimos "+days+" dias…");
            try{await Task.Run(()=>{var data=DailyHistory.Read(store.HistoryPath,copy.LocalId,copy.Care.Waterings,changes,days,now);if(pdf)CareReports.Pdf(path,copy,data,now);else CareReports.Csv(path,copy,data);});if(!closing)MessageBox.Show(this,"Relatório salvo em:\n"+path,"Relatório pronto");}
            catch(Exception ex){if(!closing)ShowError(ex);}finally{reportBusy=false;}
        }
    }
    void AddTransferSettings(TableLayoutPanel stack){
        Disclosure(stack,"Transferir dados para outro computador",details=>{
            Note(details,"Sua planta, seu histórico e suas conexões","Exporte um arquivo .raiz protegido por senha. Ele inclui plantas, calibrações, regas, medições e credenciais. A senha não fica salva no app. No destino, as credenciais voltam a ser protegidas pelo Windows.",140);
            Add(details,Buttons(Theme.Button("Exportar transferência",(s,e)=>ExportTransfer(),true),Theme.Button("Importar transferência",(s,e)=>ImportTransfer())),56);
            Note(details,"Antes de usar no destino","A importação preserva a pasta atual e cria outra, com os avisos pausados. Confira a porta e a calibração, valide a luz e só então retome o bot. Feche o Raiz no computador anterior para usar o mesmo bot.",140);
        });
    }
    string TransferPassword(bool confirm){
        using(var d=Dialog(confirm?"Proteger transferência":"Abrir transferência",610,confirm?370:300)){
            var label=Theme.Label("Digite uma senha de pelo menos 12 caracteres.\nEla é necessária para abrir o arquivo em outro computador.",10,Theme.Muted);label.SetBounds(22,24,550,58);label.AutoSize=false;d.Controls.Add(label);
            var first=Theme.Input("",540);first.UseSystemPasswordChar=true;first.AccessibleName="Senha da transferência";first.SetBounds(22,100,540,34);d.Controls.Add(first);TextBox second=null;
            if(confirm){var note=Theme.Label("Repita a senha",10,Theme.Muted);note.SetBounds(22,148,520,26);d.Controls.Add(note);second=Theme.Input("",540);second.UseSystemPasswordChar=true;second.AccessibleName="Confirme a senha";second.SetBounds(22,181,540,34);d.Controls.Add(second);}
            string password=null;var ok=Theme.Button(confirm?"Continuar exportação":"Abrir arquivo",(s,e)=>{try{DataTransfer.Password(first.Text);if(confirm&&first.Text!=second.Text)throw new ApiException("As duas senhas precisam ser iguais.");password=first.Text;d.DialogResult=DialogResult.OK;d.Close();}catch(Exception ex){MessageBox.Show(d,ex.Message,"Senha da transferência");}},true);ok.SetBounds(22,confirm?245:170,270,44);d.Controls.Add(ok);d.AcceptButton=ok;
            return d.ShowDialog(this)==DialogResult.OK?password:null;
        }
    }
    async void ExportTransfer(){
        if(transferBusy)return;using(var dialog=new SaveFileDialog{Filter="Transferência Raiz protegida|*.raiz",FileName="Raiz_transferencia_"+DateTime.Now.ToString("yyyy-MM-dd")+".raiz"}){
            if(dialog.ShowDialog(this)!=DialogResult.OK||!ExportPathAllowed(dialog.FileName))return;string password=TransferPassword(true);if(password==null)return;string path=dialog.FileName;transferBusy=true;
            try{store.Save();Status("Protegendo transferência…");await Task.Run(()=>DataTransfer.Export(new Store(store.Folder),path,password));if(!closing)MessageBox.Show(this,"Transferência salva. Leve o arquivo ao outro computador e use Configurações → Importar transferência. Guarde a senha separadamente; ela não pode ser recuperada pelo Raiz.","Transferência pronta");}catch(Exception ex){if(!closing)ShowError(ex);}finally{password=null;transferBusy=false;}
        }
    }
    async void ImportTransfer(){
        if(transferBusy)return;if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAIZ_DATA"))){MessageBox.Show(this,"Esta instalação usa uma pasta definida pelo ambiente. Abra a versão portátil sem essa definição para importar e trocar a pasta pelo app.");return;}
        using(var dialog=new OpenFileDialog{Filter="Transferência Raiz protegida|*.raiz"}){
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;string password=TransferPassword(false);if(password==null)return;transferBusy=true;
            try{
                var data=await Task.Run(()=>DataTransfer.Read(dialog.FileName,password));if(closing)return;
                var choice=MessageBox.Show(this,"Arquivo criado em "+data.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")+".\n"+data.Settings.Plants.Count+" planta(s), "+data.Settings.Plants.Sum(p=>p.Care.Waterings.Count)+" rega(s).\n\nSerá criada uma pasta separada. Seus dados atuais serão preservados. Os avisos ficarão pausados e a luz precisará ser validada no destino.\n\nImportar e encerrar o Raiz para aplicar na próxima abertura?","Conferir transferência",MessageBoxButtons.YesNo,MessageBoxIcon.Question);if(choice!=DialogResult.Yes)return;
                string folder=await Task.Run(()=>DataTransfer.ImportToNewFolder(data,Directory.GetParent(store.Folder).FullName));if(closing)return;
                DataTransfer.AtomicWrite(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dados-local.txt"),new UTF8Encoding(false).GetBytes(folder));
                MessageBox.Show(this,"Importação preparada. Abra o Raiz novamente e confira os Primeiros passos. A pasta anterior permanece em:\n"+store.Folder,"Dados preservados");exitRequested=true;Close();
            }catch(Exception ex){if(!closing)ShowError(ex);}finally{password=null;transferBusy=false;}
        }
    }
}
}

