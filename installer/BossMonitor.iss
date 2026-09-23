#define AppVersion "1.0.11"

[Setup]
AppId={{3B92D97F-CBF0-4938-9687-B86AF284864F}
AppName=Tarkov Boss Monitor
AppVersion={#AppVersion}
AppPublisher=Tarkov Boss Monitor
DefaultDirName={autopf}\Tarkov Boss Monitor
DisableDirPage=yes
DefaultGroupName=Tarkov Boss Monitor
DisableProgramGroupPage=yes
OutputDir=..\outputs\installer
OutputBaseFilename=TarkovBossMonitor-Setup-{#AppVersion}-win-x64
SetupIconFile=..\assets\boss-monitor.ico
UninstallDisplayIcon={app}\BossMonitor.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LanguageDetectionMethod=uilanguage
CloseApplications=yes
RestartApplications=no
AppMutex=Local\TarkovBossMonitor-GUI
VersionInfoDescription=Tarkov Boss Monitor Installer
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"; InfoBeforeFile: "install-notes.txt"
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"; InfoBeforeFile: "install-notes.zh-CN.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; InfoBeforeFile: "install-notes.en.txt"

[CustomMessages]
korean.DesktopShortcut=바탕화면 바로가기 만들기
korean.Shortcuts=바로가기:
korean.UninstallShortcut=Tarkov Boss Monitor 제거
korean.RunApp=Tarkov Boss Monitor 실행
korean.GamePageTitle=타르코프 연결
korean.GamePageDescription=게임 설치 폴더 확인 및 Trace 자동 설정
korean.GamePagePrompt=EscapeFromTarkov.exe와 Logging.config가 있는 폴더를 선택하세요.%n게임 실행 중에도 설치할 수 있습니다. 필요한 로그 설정은 게임 종료 후 적용됩니다.
korean.GameFolder=타르코프 설치 폴더:
korean.AutoFound=자동으로 찾은 설치 폴더 (선택하면 위 경로에 반영됩니다)
korean.Refresh=다시 탐색
korean.SelectGameFolder=타르코프 설치 폴더를 선택해 주세요.
korean.CannotCheckPath=게임 설치 경로를 확인하지 못했습니다.
korean.CheckPathOrLogging=게임 설치 경로 또는 로그 설정을 확인해 주세요.
korean.CannotStopCleanup=기존 로그 정리 작업을 중지하지 못했습니다.
korean.RetryAfterCleanup=기존 로그 정리 작업 종료 후 설치를 다시 시도해 주세요.
korean.CannotInstallTask=로그 정리 예약 작업 설치를 시작하지 못했습니다. 설치 프로그램을 다시 실행해 주세요.
korean.CannotRegisterTask=로그 정리 예약 작업 등록에 실패했습니다. 설치 프로그램을 다시 실행해 주세요.
korean.CannotStartLogging=타르코프 로그 설정 적용을 시작하지 못했습니다.
korean.GameRunningInstalled=타르코프가 실행 중이지만 설치는 완료되었습니다.%n필요한 Trace 설정은 모니터를 실행해 두면 게임 종료 후 자동 적용됩니다. 적용 후 게임을 다시 실행해 주세요.
korean.CannotApplyLogging=타르코프 로그 설정을 적용하지 못했습니다. 설치 프로그램을 다시 실행해 주세요.
korean.CannotStartUninstallTask=예약 작업 제거를 시작하지 못했습니다. 제거를 다시 시도해 주세요.
korean.CannotRemoveTask=예약 작업 제거를 완료하지 못했습니다. 제거를 다시 시도해 주세요.
chinesesimplified.DesktopShortcut=创建桌面快捷方式
chinesesimplified.Shortcuts=快捷方式：
chinesesimplified.UninstallShortcut=卸载 Tarkov Boss Monitor
chinesesimplified.RunApp=运行 Tarkov Boss Monitor
chinesesimplified.GamePageTitle=连接《逃离塔科夫》
chinesesimplified.GamePageDescription=确认游戏安装文件夹并自动设置 Trace
chinesesimplified.GamePagePrompt=请选择包含 EscapeFromTarkov.exe 和 Logging.config 的文件夹。%n游戏运行时也可以安装。所需日志设置将在游戏退出后应用。
chinesesimplified.GameFolder=《逃离塔科夫》安装文件夹：
chinesesimplified.AutoFound=自动找到的安装文件夹（选择后将填入上方路径）
chinesesimplified.Refresh=重新查找
chinesesimplified.SelectGameFolder=请选择《逃离塔科夫》安装文件夹。
chinesesimplified.CannotCheckPath=无法检查游戏安装路径。
chinesesimplified.CheckPathOrLogging=请检查游戏安装路径或日志设置。
chinesesimplified.CannotStopCleanup=无法停止现有日志清理任务。
chinesesimplified.RetryAfterCleanup=请在现有日志清理任务结束后重试安装。
chinesesimplified.CannotInstallTask=无法启动日志清理计划任务安装，请重新运行安装程序。
chinesesimplified.CannotRegisterTask=日志清理计划任务注册失败，请重新运行安装程序。
chinesesimplified.CannotStartLogging=无法开始应用《逃离塔科夫》日志设置。
chinesesimplified.GameRunningInstalled=游戏正在运行，但安装已完成。%n保持监控程序运行，退出游戏后将自动应用所需 Trace 设置。应用后请重新启动游戏。
chinesesimplified.CannotApplyLogging=无法应用《逃离塔科夫》日志设置，请重新运行安装程序。
chinesesimplified.CannotStartUninstallTask=无法开始删除计划任务，请重试卸载。
chinesesimplified.CannotRemoveTask=无法完成计划任务删除，请重试卸载。
english.DesktopShortcut=Create a desktop shortcut
english.Shortcuts=Shortcuts:
english.UninstallShortcut=Uninstall Tarkov Boss Monitor
english.RunApp=Run Tarkov Boss Monitor
english.GamePageTitle=Connect Escape from Tarkov
english.GamePageDescription=Confirm the game folder and configure Trace automatically
english.GamePagePrompt=Select the folder containing EscapeFromTarkov.exe and Logging.config.%nYou can install while the game is running. Required logging settings are applied after the game exits.
english.GameFolder=Escape from Tarkov folder:
english.AutoFound=Automatically detected folders (select one to fill the path above)
english.Refresh=Search again
english.SelectGameFolder=Select the Escape from Tarkov installation folder.
english.CannotCheckPath=Could not check the game installation path.
english.CheckPathOrLogging=Check the game installation path or logging settings.
english.CannotStopCleanup=Could not stop the existing log cleanup task.
english.RetryAfterCleanup=Retry installation after the existing log cleanup task exits.
english.CannotInstallTask=Could not start installing the log cleanup task. Run the installer again.
english.CannotRegisterTask=Could not register the log cleanup task. Run the installer again.
english.CannotStartLogging=Could not start applying Tarkov logging settings.
english.GameRunningInstalled=The game is running, but installation is complete.%nKeep the monitor running. Required Trace settings will be applied after the game exits. Restart the game afterward.
english.CannotApplyLogging=Could not apply Tarkov logging settings. Run the installer again.
english.CannotStartUninstallTask=Could not start removing the scheduled task. Retry uninstalling.
english.CannotRemoveTask=Could not finish removing the scheduled task. Retry uninstalling.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:Shortcuts}"; Flags: unchecked

[Files]
Source: "..\work\probe\BossMonitor.SetupProbe.exe"; Flags: dontcopy
Source: "..\work\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "resources\usage.ko.txt"; DestDir: "{app}"; DestName: "사용법.txt"; Flags: ignoreversion; Languages: korean
Source: "resources\usage.zh-CN.txt"; DestDir: "{app}"; DestName: "使用说明.txt"; Flags: ignoreversion; Languages: chinesesimplified
Source: "resources\usage.en.txt"; DestDir: "{app}"; DestName: "README.txt"; Flags: ignoreversion; Languages: english

[Icons]
Name: "{group}\Tarkov Boss Monitor"; Filename: "{app}\BossMonitor.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallShortcut}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Tarkov Boss Monitor"; Filename: "{app}\BossMonitor.exe"; Tasks: desktopicon; WorkingDir: "{app}"; IconFilename: "{app}\BossMonitor.exe"

[InstallDelete]
Type: files; Name: "{commondesktop}\Tarkov Boss Monitor.lnk"; Tasks: desktopicon

[Run]
Filename: "{app}\BossMonitor.exe"; Description: "{cm:RunApp}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  GamePage: TInputDirWizardPage;
  GameCandidates: TNewComboBox;
  ProbeReady: Boolean;

function RunProbe(const Params: String; var Code: Integer): Boolean;
begin
  if not ProbeReady then begin
    ExtractTemporaryFile('BossMonitor.SetupProbe.exe');
    ProbeReady := True;
  end;
  Result := Exec(ExpandConstant('{tmp}\BossMonitor.SetupProbe.exe'), Params, ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, Code);
end;

procedure CandidateSelected(Sender: TObject);
begin
  if GameCandidates.ItemIndex >= 0 then
    GamePage.Values[0] := GameCandidates.Items[GameCandidates.ItemIndex];
end;

procedure DiscoverGame(Sender: TObject);
var
  Code, I: Integer;
  Candidates: TArrayOfString;
  Response: String;
begin
  Response := ExpandConstant('{tmp}\game-candidates.txt');
  GameCandidates.Items.Clear;
  if RunProbe('discover "' + Response + '"', Code) and (Code = 0) then begin
    if LoadStringsFromFile(Response, Candidates) then begin
      for I := 0 to GetArrayLength(Candidates) - 1 do
        if Trim(Candidates[I]) <> '' then
          GameCandidates.Items.Add(Candidates[I]);
      if GameCandidates.Items.Count > 0 then begin
        GameCandidates.ItemIndex := 0;
        if Trim(GamePage.Values[0]) = '' then
          GamePage.Values[0] := GameCandidates.Items[0];
      end;
    end;
  end;
end;

procedure InitializeWizard();
var
  LabelControl: TNewStaticText;
  RefreshButton: TNewButton;
begin
  GamePage := CreateInputDirPage(wpSelectTasks, ExpandConstant('{cm:GamePageTitle}'), ExpandConstant('{cm:GamePageDescription}'),
    ExpandConstant('{cm:GamePagePrompt}'), False, '');
  GamePage.Add(ExpandConstant('{cm:GameFolder}'));
  GamePage.Values[0] := ExpandConstant('{param:GAMEPATH|}');
  LabelControl := TNewStaticText.Create(WizardForm);
  LabelControl.Parent := GamePage.Surface;
  LabelControl.Caption := ExpandConstant('{cm:AutoFound}');
  LabelControl.Top := GamePage.Edits[0].Top + GamePage.Edits[0].Height + ScaleY(20);
  LabelControl.Width := GamePage.SurfaceWidth;
  GameCandidates := TNewComboBox.Create(WizardForm);
  GameCandidates.Parent := GamePage.Surface;
  GameCandidates.Top := LabelControl.Top + LabelControl.Height + ScaleY(6);
  GameCandidates.Width := GamePage.SurfaceWidth - ScaleX(110);
  GameCandidates.Style := csDropDownList;
  GameCandidates.OnChange := @CandidateSelected;
  RefreshButton := TNewButton.Create(WizardForm);
  RefreshButton.Parent := GamePage.Surface;
  RefreshButton.Caption := ExpandConstant('{cm:Refresh}');
  RefreshButton.Left := GameCandidates.Width + ScaleX(10);
  RefreshButton.Top := GameCandidates.Top;
  RefreshButton.Width := ScaleX(100);
  RefreshButton.Height := GameCandidates.Height;
  RefreshButton.OnClick := @DiscoverGame;
  DiscoverGame(nil);
end;

function ValidateGameSelection(): String;
var
  Code, I: Integer;
  Response: String;
  Details: TArrayOfString;
begin
  Result := '';
  if Trim(GamePage.Values[0]) = '' then begin
    Result := ExpandConstant('{cm:SelectGameFolder}');
    Exit;
  end;
  Response := ExpandConstant('{tmp}\game-validation.txt');
  if not RunProbe('validate "' + GamePage.Values[0] + '" "' + Response + '"', Code) then
    Result := ExpandConstant('{cm:CannotCheckPath}')
  else if Code <> 0 then begin
    if LoadStringsFromFile(Response, Details) then begin
      for I := 0 to GetArrayLength(Details) - 1 do begin
        if Result <> '' then Result := Result + #13#10;
        Result := Result + Details[I];
      end;
      if Result = '' then Result := ExpandConstant('{cm:CheckPathOrLogging}');
    end
    else Result := ExpandConstant('{cm:CheckPathOrLogging}');
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Error: String;
begin
  Result := True;
  if CurPageID = GamePage.ID then begin
    Error := ValidateGameSelection();
    if Error <> '' then begin
      MsgBox(Error, mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := ValidateGameSelection();
  if Result <> '' then Exit;
  if FileExists(ExpandConstant('{app}\BossMonitor.SetupHelper.exe')) then begin
    if not Exec(ExpandConstant('{app}\BossMonitor.SetupHelper.exe'), 'prepare-update', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := ExpandConstant('{cm:CannotStopCleanup}')
    else if Code <> 0 then
      Result := ExpandConstant('{cm:RetryAfterCleanup}');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not Exec(ExpandConstant('{app}\BossMonitor.SetupHelper.exe'), 'install', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException(ExpandConstant('{cm:CannotInstallTask}'));
    if Code <> 0 then
      RaiseException(ExpandConstant('{cm:CannotRegisterTask}'));
    if not Exec(ExpandConstant('{app}\BossMonitor.SetupHelper.exe'), 'configure-install "' + GamePage.Values[0] + '"', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException(ExpandConstant('{cm:CannotStartLogging}'));
    if Code = 2 then begin
      if not WizardSilent then
        MsgBox(ExpandConstant('{cm:GameRunningInstalled}'), mbInformation, MB_OK);
    end
    else if Code <> 0 then
      RaiseException(ExpandConstant('{cm:CannotApplyLogging}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Code: Integer;
begin
  if (CurUninstallStep = usUninstall) and FileExists(ExpandConstant('{app}\BossMonitor.SetupHelper.exe')) then begin
    if not Exec(ExpandConstant('{app}\BossMonitor.SetupHelper.exe'), 'uninstall', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException(ExpandConstant('{cm:CannotStartUninstallTask}'));
    if Code <> 0 then
      RaiseException(ExpandConstant('{cm:CannotRemoveTask}'));
  end;
end;
