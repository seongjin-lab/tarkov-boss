using System.Globalization;

namespace BossMonitor.Core;

public static class I18n
{
    private static string Language
    {
        get
        {
            string? forced = Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            return string.IsNullOrWhiteSpace(forced) ? CultureInfo.CurrentUICulture.Name : forced;
        }
    }

    private static readonly Dictionary<string,string> English = new()
    {
        [" · 레이드 종료"]=" · Raid ended", [" 알림을 클릭하면 창을 다시 엽니다."]=" Click the notification to reopen the window.",
        ["●  감시 일시 중지"]="●  Monitoring paused", ["●  감시 중"]="●  Monitoring",
        ["감시 안 함"]="Do not monitor", ["감시 오류: "]="Monitoring error: ", ["감시 일시 중지"]="Pause monitoring",
        ["감시 일시 중지 / 재개"]="Pause / resume monitoring", ["감시 재개"]="Resume monitoring",
        ["개인 경로·닉네임·프로필·원본 로그를 제외한 진단 파일을 저장했습니다."]="Saved a diagnostic file without personal paths, nicknames, profiles, or raw logs.",
        ["개체"]="Instance", ["건너뜀"]="Skipped",
        ["건의 로그 설정은 게임 실행·설정 변경 등으로 자동 복원하지 않았습니다. Logging.config의 aiData minLevel을 백업의 이전 값으로 수동 복원할 수 있습니다. 백업 위치:"]=" logging settings were not restored automatically because the game was running or the settings had changed. You can manually restore aiData minLevel in Logging.config to the previous value in the backup. Backup location:",
        ["게임 경로 미등록"]="Game path not registered", ["게임 경로 설정 대기"]="Waiting for game path setup",
        ["게임 경로를 선택하면 로그 용량을 확인할 수 있습니다."]="Select the game path to check log storage usage.",
        ["게임 로그"]="Game logs", ["게임 로그 · 최근 7일 보관"]="Game logs · Keep the latest 7 days",
        ["게임 로그 생성 대기"]="Waiting for game logs", ["게임 로그 세션 대기"]="Waiting for a game log session",
        ["게임 로그 용량 확인 중…"]="Checking game log size…", ["게임 실행 감지 · 정리 중단"]="Game detected · Cleanup stopped",
        ["게임 실행 중 · 정리 건너뜀"]="Game running · Cleanup skipped",
        ["게임 실행 중입니다. 설정 저장 후 게임을 종료하면 자동 적용합니다."]="The game is running. Save the settings and they will be applied automatically after the game exits.",
        ["게임 연결"]="Game connection", ["게임 연결, 맵별 보스, 로그 보관 설정을 관리합니다."]="Manage the game connection, bosses by map, and log retention.",
        ["게임 종료 후 로그 설정을 적용해 주세요."]="Exit the game before applying logging settings.",
        ["게임 폴더를 확인하고 설정을 저장하면 감시를 시작합니다."]="Confirm the game folder and save the settings to start monitoring.",
        ["게임이 실행되었습니다. 종료 후 다시 적용해 주세요."]="The game has started. Exit it and apply the settings again.",
        ["경로 자동 탐색"]="Find path automatically", ["경로·권한·게임 상태 확인 실패 · 삭제 중단"]="Path, permission, or game-state check failed · Deletion stopped",
        ["공식 로컬 PvE 상세 로그를 지원합니다. 업데이트는 새 설치 프로그램으로 진행합니다."]="Supports detailed logs from official local PvE. Update by running the new installer.",
        ["관리자 권한 요청이 취소되었습니다."]="The administrator permission request was canceled.", ["관리자 권한이 필요합니다."]="Administrator privileges are required.",
        ["기록"]="Recorded", ["나중에 설정"]="Set up later", ["다른 프로그램이 로그 설정을 변경했습니다. 다시 적용해 주세요."]="Another program changed the logging settings. Apply them again.",
        ["다음 레이드를 기다리고 있습니다"]="Waiting for the next raid", ["닫으면 트레이로 숨기기"]="Hide to tray when closed",
        ["대상 경로를 안전하게 잠글 수 없습니다."]="Could not safely lock the target path.", ["더 새로운 프로그램 버전의 설정입니다."]="These settings were created by a newer version of the app.",
        ["독립 로그 정리 작업은 Program Files의 정식 설치 폴더에서만 등록할 수 있습니다."]="The standalone log cleanup task can only be registered from the installed folder under Program Files.",
        ["돌아가기"]="Back", ["드라이브 여유"]="Drive free space", ["레이드 감지 대기"]="Waiting to detect a raid", ["로그 관리"]="Log management",
        ["로그 규칙이 없거나 올바르지 않습니다. 게임 설치를 복구해 주세요."]="The logging rule is missing or invalid. Repair the game installation.",
        ["로그 규칙이 없거나 중복되었습니다. 게임 설정을 복구해 주세요."]="The logging rule is missing or duplicated. Repair the game settings.",
        ["로그 대기"]="Waiting for logs", ["로그 복원 중…"]="Restoring logs…", ["로그 설정 변경에 관리자 권한이 필요합니다."]="Administrator privileges are required to change logging settings.",
        ["로그 설정 읽기 실패"]="Failed to read logging settings", ["로그 설정 자동 적용"]="Apply logging settings automatically",
        ["로그 설정 자동 적용 및 정리 경로 등록 중…"]="Applying logging settings and registering the cleanup path…",
        ["로그 설정 작업이 완료된 후 종료해 주세요."]="Wait for the logging setup operation to finish before exiting.",
        ["로그 설정 적용 완료 · 게임을 다시 실행해 주세요."]="Logging settings applied · Restart the game.", ["로그 설정 적용 중"]="Applying logging settings",
        ["로그 설정 확인 필요"]="Logging settings need attention", ["로그 설정 JSON이 올바르지 않습니다."]="The logging settings JSON is invalid.",
        ["로그 설정에 rules가 없습니다."]="The logging settings do not contain rules.", ["로그 설정을 읽을 수 없습니다."]="Could not read the logging settings.",
        ["로그 설정의 rules 구조를 확인해 주세요."]="Check the rules structure in the logging settings.",
        ["로그 연결됨 · 상세 AI 기록 확인"]="Logs connected · Detailed AI records detected", ["로그 정리 실행 파일을 찾을 수 없습니다."]="Could not find the log cleanup executable.",
        ["로그 정리 예약 작업 복구 완료"]="Log cleanup scheduled task repaired", ["로그 정리 작업 비활성화 · 복구 필요"]="Log cleanup task disabled · Repair required",
        ["로그 정리 작업 없음 · 복구 필요"]="Log cleanup task missing · Repair required", ["로그 정리 작업 종료를 기다리지 못했습니다. 제거를 다시 시도해 주세요."]="Could not wait for the log cleanup task to exit. Try uninstalling again.",
        ["로그 정리 프로세스 종료 후 업데이트를 다시 시도해 주세요."]="Try the update again after the log cleanup process exits.",
        ["로그 파일을 독점적으로 열 수 없습니다."]="Could not open the log file exclusively.", ["로그 파일을 삭제하지 못했습니다."]="Could not delete the log file.",
        ["로컬 고정 드라이브의 게임 폴더를 선택해 주세요."]="Select a game folder on a local fixed drive.",
        ["링크 또는 확인할 수 없는 경로는 처리하지 않습니다."]="Links and unverifiable paths are not processed.", ["링크 파일은 삭제하지 않습니다."]="Link files are not deleted.",
        ["링크 파일은 처리하지 않습니다."]="Link files are not processed.",
        ["매일 04:00에 Windows 예약 작업으로 정리합니다. 앱이 꺼져 있어도 실행됩니다.\n게임 실행 중·가장 최근 세션은 보호하며, 최근 7일 로그는 용량을 이유로 삭제하지 않습니다."]="A Windows scheduled task cleans logs every day at 04:00, even when the app is closed.\nLogs in use and the newest session are protected, and logs from the latest 7 days are never deleted to save space.",
        ["보스"]="Bosses", ["AI PMC 웨이브 기록 대기"]="Waiting for AI PMC wave records",
        ["예정 웨이브"]="Planned waves", ["감지된 웨이브"]="Observed waves", ["웨이브 확률"]="Wave chance",
        ["초기 스폰"]="Initial spawn", ["초기 스폰 예정"]="Initial spawn planned", ["초기 스폰 없음"]="No initial spawn",
        ["추가 스폰 감시 중"]="Monitoring for additional spawns", ["추가 스폰 예정"]="Additional spawn planned", ["추가 스폰 감시 종료"]="Additional spawn monitoring ended",
        ["실제 활성화"]="Actual activations", ["첫 활성화"]="First activation",
        ["맵별 감시 보스"]="Bosses to monitor by map", ["맵별 보스"]="Bosses by map", ["맵은 자동으로 감지합니다. 여기서 각 맵의 감시 대상을 선택하세요."]="Maps are detected automatically. Select which bosses to monitor on each map.",
        ["명"]="", ["모든 보스 감시"]="Monitor all bosses", ["모든 보스 감시 중 · 보스 판정 기록 대기"]="Monitoring all bosses · Waiting for boss records",
        ["백업 경로가 올바르지 않습니다."]="The backup path is invalid.", ["변경된 설정은 자동 복원하지 않습니다."]="Modified settings are not restored automatically.",
        ["보스 스폰 확인"]="Boss spawn confirmed", ["사망 확인"]="Death confirmed", ["사용할 설치 경로를 선택하세요."]="Select the installation path to use.",
        ["삭제"]="Deleted", ["상태 새로고침"]="Refresh status", ["생존 기록 없음"]="No survival record", ["생존 추정"]="Estimated alive",
        ["선택된 보스가 없습니다. 설정에서 보스를 선택해 주세요."]="No bosses are selected. Select bosses in Settings.", ["선택한 보스만 감시"]="Monitor selected bosses only",
        ["설정"]="Settings", ["설정 · Tarkov Boss Monitor"]="Settings · Tarkov Boss Monitor", ["설정 저장 · 감시 시작"]="Save settings · Start monitoring",
        ["설정 적용 취소됨"]="Applying settings canceled", ["설정 프로그램을 실행하지 못했습니다."]="Could not start the setup helper.",
        ["설정을 적용하지 못했습니다. 오류 안내를 확인해 주세요."]="Could not apply the settings. Check the error message.", ["설치 경로 탐색 중…"]="Searching for the installation path…",
        ["설치 관리자 권한이 필요합니다."]="Administrator privileges are required for installation.", ["설치 완료"]="Installation complete", ["설치 후보"]="Installation candidates",
        ["스폰 예정"]="Planned spawn", ["스폰 예정 없음"]="No planned spawn", ["스폰 확률"]="Spawn chance", ["스폰 확인"]="Spawn confirmed",
        ["아직 정리 실행 기록이 없습니다."]="No cleanup run has been recorded yet.", ["알 수 없는 로그 수준입니다. 자동 변경을 중단했습니다."]="Unknown log level. Automatic changes were stopped.",
        ["업데이트 준비 완료"]="Update preparation complete", ["예약 작업 복구"]="Repair scheduled task", ["예약 작업 복구 실패"]="Scheduled task repair failed",
        ["예약 작업 복구 완료"]="Scheduled task repair complete", ["예약 작업 제거 및"]="Scheduled task removed and", ["예약 작업 제거 및 제품이 변경한 로그 설정 복원 완료."]="Scheduled task removed and logging settings changed by the app restored.",
        ["예약 작업 제거 완료."]="Scheduled task removed.", ["예약됨 · 매일 04:00 · 다음"]="Scheduled · Daily at 04:00 · Next", ["오류 기록"]="Error log",
        ["의 실제 활성화 기록을 확인했습니다. 클릭하면 감시 화면을 엽니다."]=" actual activation record was detected. Click to open the monitor.",
        ["이 맵은 감시하지 않도록 설정되어 있습니다."]="Monitoring is disabled for this map.", ["이미 실행 중입니다. 작업 표시줄의 트레이 아이콘을 확인해 주세요."]="The app is already running. Check the system tray icon.",
        ["자동 탐색 결과가 없습니다. 폴더를 선택하거나 경로를 입력해 주세요."]="No installation was found automatically. Select a folder or enter a path.",
        ["작업 스케줄러 확인 불가"]="Could not check Task Scheduler", ["작업 시간이 초과되었습니다."]="The operation timed out.", ["작업을 완료하지 못했습니다."]="Could not complete the operation.",
        ["정리 결과를 읽지 못했습니다."]="Could not read the cleanup result.", ["정리 명세 저장소 권한이 올바르지 않습니다."]="The cleanup configuration store has invalid permissions.",
        ["정리 완료"]="Cleanup complete", ["정리 완료 · 일부 파일 건너뜀"]="Cleanup complete · Some files skipped", ["정션·심볼릭 링크 경로는 지원하지 않습니다."]="Junction and symbolic-link paths are not supported.",
        ["종료"]="Exit", ["종료 시 사망 미확인"]="Death unconfirmed at raid end", ["종료 코드"]="Exit code", ["종료된 레이드"]="Ended raid",
        ["지원하지 않는 경로 확인 작업입니다."]="Unsupported path-check operation.", ["지원하지 않는 작업입니다."]="Unsupported operation.", ["진단 내보내기"]="Export diagnostics",
        ["진단 파일"]="Diagnostic file", ["창 열기"]="Open window", ["처음 시작하기"]="Get started", ["처음 시작하기 · Tarkov Boss Monitor"]="Get started · Tarkov Boss Monitor",
        ["체크하면 닫기(X)로 숨긴 뒤에도 감시합니다. 해제하면 창을 닫을 때 종료합니다.\n트레이로 숨길 때 안내하고, 숨겨진 상태에서 보스 스폰 확인 시 Windows 알림을 표시합니다."]="When enabled, closing the window hides it to the tray and monitoring continues. When disabled, closing the window exits the app.\nA notification appears when hidden to the tray and when a monitored boss spawn is confirmed while hidden.",
        ["최근 결과"]="Last result", ["최초 구역"]="Initial zone", ["최초 좌표"]="Initial coordinates", ["타르코프 게임 설치 폴더 선택"]="Select the Escape from Tarkov installation folder",
        ["타르코프 설치 폴더"]="Escape from Tarkov installation folder", ["트레이로"]="To tray",
        ["트레이로 숨겼습니다. 감시는 현재 일시 중지 상태입니다."]="Hidden to the tray. Monitoring is currently paused.",
        ["트레이로 숨겼습니다. 보스 감시는 계속됩니다."]="Hidden to the tray. Boss monitoring continues.",
        ["트레이로 숨겼습니다. 창을 다시 열어 게임 연결 설정을 완료해 주세요."]="Hidden to the tray. Reopen the window to finish the game connection setup.",
        ["폴더 선택"]="Select folder", ["프로그램을 시작하지 못했습니다."]="Could not start the app.", ["프로세스를 시작하지 못했습니다."]="Could not start the process.",
        ["항상 위에 표시"]="Always on top", ["현재 레이드"]="Current raid", ["확인 불가"]="Unknown", ["활성화 개체"]="Active instances",
        ["AI 상세 로그 대기 · 로컬 PvE 지원"]="Waiting for detailed AI logs · Local PvE supported",
        ["EscapeFromTarkov.exe가 있는 게임 폴더를 선택해 주세요."]="Select the game folder containing EscapeFromTarkov.exe.",
        ["EscapeFromTarkov.exe와 Logging.config가 있는 폴더를 선택하세요.\n맵은 자동 감지합니다. 실행 중인 게임은 종료한 뒤 Trace 설정이 적용됩니다."]="Select the folder containing EscapeFromTarkov.exe and Logging.config.\nMaps are detected automatically. Trace settings are applied after the running game exits.",
        ["JSON 파일|*.json"]="JSON files|*.json", ["Logging.config를 찾을 수 없습니다. 게임 설치를 확인해 주세요."]="Logging.config was not found. Check the game installation.",
        ["Tarkov Boss Monitor 시작 오류"]="Tarkov Boss Monitor startup error", ["Tarkov Boss Monitor 제거"]="Uninstall Tarkov Boss Monitor",
        ["Trace 설정 완료 · 게임을 실행하거나 다시 실행해 주세요."]="Trace setup complete · Start or restart the game.", ["Trace 설정 저장 결과가 일치하지 않습니다."]="The saved Trace settings do not match.",
        ["Trace 설정 필요"]="Trace setup required", ["Trace 설정 확인 완료 · 로그 정리 경로 등록 완료."]="Trace settings verified · Log cleanup path registered.", ["Trace 설정됨"]="Trace configured",
        ["Windows 로그 정리 예약 작업을 등록하지 못했습니다."]="Could not register the Windows log cleanup scheduled task."
    };

    public static bool IsChinese => Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    public static bool IsKorean => Language.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
    public static bool IsEnglish => !IsChinese && !IsKorean;
    public static string T(string korean, string chinese) => IsChinese ? chinese : IsKorean ? korean : English.GetValueOrDefault(korean, korean);
    public static string FontFamily => IsChinese ? "Microsoft YaHei UI" : IsKorean ? "Malgun Gothic" : "Segoe UI";
    public static IReadOnlyCollection<string> EnglishKeys => English.Keys;
}
