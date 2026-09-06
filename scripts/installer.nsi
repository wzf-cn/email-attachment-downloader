Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!ifndef APPKEY
!define APPKEY "MailIntake"
!endif
!ifndef APPNAME
!define APPNAME "邮件接收管理"
!endif
Name "${APPNAME}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\${APPKEY}"
InstallDirRegKey HKCU "Software\${APPKEY}" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID zlib
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
Var AppLock

!macro StopApp Prefix
Function ${Prefix}StopApp
  System::Call 'kernel32::OpenMutexW(i 0x100001, i 0, w "Local\KeywordMailDownloader") p .r0'
  ${If} $0 != 0
    IfSilent decline
    MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON2 "邮件软件正在运行。是否退出软件以便继续？请先保存界面中的修改。" IDNO decline
    System::Call 'kernel32::OpenEventW(i 2, i 0, w "Local\MailIntakeUpdateExit") p .r1'
    ${If} $1 == 0
      MessageBox MB_OK "旧版本尚不支持自动退出，请从托盘退出后重试。"
      Goto decline
    ${EndIf}
    System::Call 'kernel32::SetEvent(p r1)'
    System::Call 'kernel32::CloseHandle(p r1)'
    DetailPrint "正在等待邮件处理结束并退出…"
    System::Call 'kernel32::WaitForSingleObject(p r0, i 60000) i .r2'
    ${If} $2 != 0
    ${AndIf} $2 != 128
      MessageBox MB_OK "软件尚未退出，操作已取消。请等待处理完成后重试。"
      Goto decline
    ${EndIf}
    StrCpy $AppLock $0
    Return
    decline:
      System::Call 'kernel32::CloseHandle(p r0)'
      SetErrorLevel 2
      Abort
  ${EndIf}
  System::Call 'kernel32::CreateMutexW(p 0, i 1, w "Local\KeywordMailDownloader") p .r0 ?e'
  Pop $1
  ${If} $0 == 0
  ${OrIf} $1 == 183
    System::Call 'kernel32::CloseHandle(p r0)'
    SetErrorLevel 2
    Abort
  ${EndIf}
  StrCpy $AppLock $0
FunctionEnd
!macroend
!insertmacro StopApp ""
!insertmacro StopApp "un."

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK "本安装包需要 64 位 Windows。"
    Abort
  ${EndIf}
  SetShellVarContext current
FunctionEnd
Function un.onInit
  SetShellVarContext current
FunctionEnd

Section "安装"
  Call StopApp
  SetOutPath "$INSTDIR"
  !include "${INSTALLFILES}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\MailIntake.exe"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\卸载.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\${APPKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "DisplayIcon" "$INSTDIR\MailIntake.exe"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Call un.StopApp
  !include "${DELETEFILES}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\卸载.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "KeywordMailDownloader"
  ${If} $0 == '$\"$INSTDIR\MailIntake.exe$\" --background'
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "KeywordMailDownloader"
  ${EndIf}
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}"
  DeleteRegKey HKCU "Software\${APPKEY}"
  ; Deliberately do not delete AppData, download folders, or any unlisted files.
SectionEnd
