using BossMonitor.Core;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            if(!Maintenance.IsAdmin())throw new UnauthorizedAccessException(I18n.T("관리자 권한이 필요합니다.","需要管理员权限。"));
            if(args.Length==2 && args[0]=="configure-install")
                return Maintenance.ConfigureForInstall(args[1])?2:0;
            string message=args.FirstOrDefault() switch
            {
                "install"=>Install(),
                "configure" when args.Length==2=>Maintenance.Configure(args[1]),
                "repair"=>Repair(),
                "prepare-update"=>PrepareUpdate(),
                "uninstall"=>Maintenance.Uninstall(),
                _=>throw new IOException(I18n.T("지원하지 않는 작업입니다.","不支持此操作。"))
            };
            // Installer actions are silent; configuration feedback is shown by the GUI.
            if(args[0]=="uninstall" && !message.StartsWith(I18n.T("예약 작업 제거 및","计划任务已删除，并")))
                System.Windows.Forms.MessageBox.Show(message,I18n.T("Tarkov Boss Monitor 제거","卸载 Tarkov Boss Monitor"),System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Information);
            return 0;
        }
        catch(Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(ex.Message,"Tarkov Boss Monitor",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Error);
            return 1;
        }
    }
    private static string Install(){Maintenance.Install();return I18n.T("설치 완료","安装完成");}
    private static string Repair(){Maintenance.Install();return I18n.T("예약 작업 복구 완료","计划任务修复完成");}
    private static string PrepareUpdate(){Maintenance.PrepareUpdate();return I18n.T("업데이트 준비 완료","更新准备完成");}
}
