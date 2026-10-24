; Kurumsal Müzik Player kurulum dosyası (Inno Setup 6). Derleme: scripts\kurulum.ps1
; Kullanıcı bazlı kurulum, yönetici izni istemez (BRIEF §2.2). İmzasız (BRIEF §2.1 karar 3).

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "Kurumsal Müzik Player"
#define AppExe "KurumsalMuzikPlayer.exe"
#define Src "..\src\KurumsalMuzikPlayer\bin\Release"

[Setup]
AppId={{6F1B6C2E-4E0B-4C59-9C8B-6B1A0C7E2D41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Viva Medya
AppPublisherURL=https://vivamedya.com
AppSupportURL=https://kurumsalmuzik.net
VersionInfoVersion=1.0.0.0
VersionInfoCompany=Viva Medya
VersionInfoDescription={#AppName} Kurulum
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=6.1sp1
OutputDir=..\dist
OutputBaseFilename=Kurumsal-Muzik-Player-Kurulum-{#AppVersion}
SetupIconFile=..\src\KurumsalMuzikPlayer\Resources\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
Source: "{#Src}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Src}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Src}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Src}\mpv\*"; DestDir: "{app}\mpv"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Windows oturumu açılınca başlar (pencere simge durumunda; giriş gerekiyorsa açık gelir).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"" --gizli"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} programını başlat"; Flags: nowait postinstall skipifsilent
; Program kendini güncellerken (sessiz kurulum, /guncelleme=1) kurulum bitince program yeniden açılır.
Filename: "{app}\{#AppExe}"; Parameters: "--gizli"; Flags: nowait; Check: IsUpdate

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--kapat"; Flags: runhidden waituntilterminated; RunOnceId: "Kapat"

[UninstallDelete]
; İnen arşiv, günlük ve şube kaydı (sunucudaki kayıt durur; yeniden kurulunca şube adıyla tekrar girilir).
Type: filesandordirs; Name: "{localappdata}\Kurumsal Muzik Player"

[Code]
const
  Net48Release = 528040;

function InitializeSetup(): Boolean;
var
  Release: Cardinal;
  ErrorCode: Integer;
begin
  Result := True;
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
     or (Release < Net48Release) then
  begin
    if MsgBox('Kurumsal Müzik Player için bu bilgisayarda Microsoft .NET Framework 4.8 gerekli.' + #13#10#13#10 +
              'Microsoft''un indirme sayfası açılsın mı? .NET Framework 4.8''i kurduktan sonra bu kurulumu yeniden çalıştırın.',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;

function IsUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:guncelleme|0}') = '1';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Exe: String;
  ResultCode: Integer;
begin
  Result := '';
  // Güncellemede çalışan program dosyaları kilitlemesin: motoru düzgünce kapat.
  Exe := ExpandConstant('{app}\{#AppExe}');
  if FileExists(Exe) then
    Exec(Exe, '--kapat', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
