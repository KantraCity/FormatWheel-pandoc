; Format Wheel - Pandoc extension installer (documents).
; Installs the extension DLL into the app's extensions folder and makes sure Pandoc is present (via winget).

#define ExtName    "Format Wheel - Pandoc"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{9E2F4A61-3C7B-4D58-A1E0-6B8C5D2F7A93}
AppName={#ExtName}
AppVersion={#AppVersion}
AppVerName={#ExtName} {#AppVersion}
AppPublisher=Format Wheel

PrivilegesRequired=lowest
; extensions live next to the app, each in its own subfolder
DefaultDirName={localappdata}\Programs\FormatWheel\extensions\pandoc
DisableDirPage=yes
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

OutputDir=..\dist
OutputBaseFilename=FormatWheel-Pandoc-Setup-{#AppVersion}
UninstallDisplayName={#ExtName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
en.NeedApp=Format Wheel is not installed. Install the app first, then this extension.
ru.NeedApp=Format Wheel не установлен. Сначала установите программу, затем это расширение.
en.InstallingPandoc=Installing Pandoc via winget (this can take a minute)...
ru.InstallingPandoc=Устанавливаю Pandoc через winget (может занять минуту)...
en.NoWinget=winget is not available. Install Pandoc manually: winget install JohnMacFarlane.Pandoc
ru.NoWinget=winget недоступен. Установите Pandoc вручную: winget install JohnMacFarlane.Pandoc

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
// без приложения расширение бессмысленно
function InitializeSetup(): Boolean;
begin
  if not DirExists(ExpandConstant('{localappdata}\Programs\FormatWheel')) then
  begin
    MsgBox(ExpandConstant('{cm:NeedApp}'), mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;

procedure InstallPandoc();
var code: Integer;
begin
  WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingPandoc}');
  if not Exec(ExpandConstant('{cmd}'), '/c winget install --id JohnMacFarlane.Pandoc -e --scope user --accept-source-agreements --accept-package-agreements',
       '', SW_HIDE, ewWaitUntilTerminated, code) then
    MsgBox(ExpandConstant('{cm:NoWinget}'), mbInformation, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var code: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // ставим Pandoc только если его ещё нет
    if (not FileExists(ExpandConstant('{localappdata}\Pandoc\pandoc.exe'))) and
       ((not Exec(ExpandConstant('{cmd}'), '/c where pandoc', '', SW_HIDE, ewWaitUntilTerminated, code)) or (code <> 0)) then
      InstallPandoc();
  end;
end;
