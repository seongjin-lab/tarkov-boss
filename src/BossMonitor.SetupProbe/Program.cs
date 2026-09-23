using System.Text;
using System.Text.Json.Nodes;
using BossMonitor.Core;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Read-only installer bootstrap: never changes settings, registers tasks or deletes logs.
        if(args.Length<2)return 1;
        string result=args[^1];
        try
        {
            if(args[0]=="discover" && args.Length==2)
            {
                string saved="";try{saved=Maintenance.State().GameRoot;}catch{}
                var candidates=Paths.Discover(saved);
                File.WriteAllLines(result,candidates,new UTF8Encoding(true));return 0;
            }
            if(args[0]=="validate" && args.Length==3)
            {
                string root=Paths.ValidateGame(args[1]);
                var rules=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"Logging.config")))?["rules"]?.AsArray()??throw new IOException(I18n.T("로그 설정의 rules 구조를 확인해 주세요.","请检查日志配置中的 rules 结构。"));
                string[] levels=["Trace","Debug","Information","Warning","Error","Critical"];
                foreach(string name in new[]{"aiData","application","backend"})
                {
                    var matching=rules.Where(r=>r?["fileName"]?.GetValue<string>()==name).ToList();
                    if(matching.Count!=1 || !levels.Contains(matching[0]?["minLevel"]?.GetValue<string>()??""))throw new IOException($"{name} {I18n.T("로그 규칙이 없거나 올바르지 않습니다. 게임 설치를 복구해 주세요.","日志规则缺失或无效，请修复游戏安装。")}");
                }
                File.WriteAllText(result,root,new UTF8Encoding(true));return 0;
            }
            throw new IOException(I18n.T("지원하지 않는 경로 확인 작업입니다.","不支持此路径检查操作。"));
        }
        catch(Exception ex)
        {
            try{File.WriteAllText(result,ex.Message,new UTF8Encoding(true));}catch{}
            return 1;
        }
    }
}
