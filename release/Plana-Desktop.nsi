Unicode True
RequestExecutionLevel admin
ManifestDPIAware true
!include LogicLib.nsh

!ifndef REPOSITORY_ROOT
  !error "REPOSITORY_ROOT must point to the Plana Desktop repository."
!endif
!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "${REPOSITORY_ROOT}\release-output\Plana-Desktop-x64-Setup.exe"
!endif

!define PRODUCT_NAME "Plana Desktop"
!define COMPANY_NAME "Plana Desktop"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Plana Desktop"

Name "${PRODUCT_NAME}"
OutFile "${OUTPUT_FILE}"
InstallDir "$PROGRAMFILES64\Plana Desktop"
InstallDirRegKey HKLM "${UNINSTALL_KEY}" "InstallLocation"
Icon "${REPOSITORY_ROOT}\src\Plana.Brand\AppIcon.ico"
UninstallIcon "${REPOSITORY_ROOT}\src\Plana.Brand\AppIcon.ico"
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "Install"
  SetShellVarContext all
  SetOutPath "$INSTDIR"
  File /r "${REPOSITORY_ROOT}\artifacts\native-win-x64\*"

  SetOutPath "$INSTDIR\ControlCenter"
  File /r "${CONTROL_CENTER_PACKAGE}\*"

  DetailPrint "Registering Plana Desktop Control Center..."
  ExecWait '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\ControlCenter\Install.ps1" -SkipLoggingTelemetry -Force' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Control Center installation failed (exit code $0)."
    Abort
  ${EndIf}

  CreateDirectory "$SMPROGRAMS\Plana Desktop"
  CreateShortcut "$SMPROGRAMS\Plana Desktop\Plana Desktop.lnk" "$INSTDIR\Plana.Desktop.exe" "" "$INSTDIR\Assets\AppIcon.ico"
  CreateShortcut "$DESKTOP\Plana Desktop.lnk" "$INSTDIR\Plana.Desktop.exe" "" "$INSTDIR\Assets\AppIcon.ico"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\Assets\AppIcon.ico"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1

  Exec '"$INSTDIR\Plana.Desktop.exe"'
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  nsExec::ExecToLog 'taskkill /IM Plana.Desktop.exe /F'
  nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxPackage -Name 10EB374A-4174-4174-A0F4-DD873A4FA97A | Remove-AppxPackage"'
  Delete "$DESKTOP\Plana Desktop.lnk"
  RMDir /r "$SMPROGRAMS\Plana Desktop"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd
