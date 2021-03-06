﻿;-----------------------------------------------------------------------------
; Name:        A5GapOutsidePageNumT16.au3
; Purpose:     Script ConfigurationTool to test Custom A5 with wider column gap
;              and centered page header with page numbers on the outside margins
;              (Edited script created by AutoItRecorder.)
;
; Author:      <greg_trihus@sil.org>
;
; Created:     2013/11/22
; Copyright:   (c) 2013 SIL International
; Licence:     <MIT>
;-----------------------------------------------------------------------------
Opt("WinWaitDelay",100)
Opt("WinDetectHiddenText",1)
Opt("MouseCoordMode",0)
$TitleMatchStart = 1
Opt("WinTitleMatchMode", $TitleMatchStart) 

Send("{SHIFTDOWN}")
Run("C:\\Program Files (x86)\\SIL\\Pathway7\\ConfigurationTool.exe")
_WinWaitActivate("Pathway Configuration Tool - BTE","")
Send("{SHIFTUP}")
MouseMove(371,10)
MouseDown("left")
MouseMove(479,9)
MouseUp("left")
MouseClick("left",69,434,1)
MouseClick("left",61,162,1)
MouseClick("left",205,165,1)
MouseClick("left",941,166,1)
MouseClick("left",83,50,1)
MouseClick("left",875,165,1)
MouseClick("left",1131,196,1)
Send("{SHIFTDOWN}{HOME}a{SHIFTUP}5{SPACE}with{SPACE}larger{SPACE}gap")
MouseClick("left",936,161,1)
MouseClick("left",1019,196,1)
MouseClick("left",1024,227,1)
MouseClick("left",991,299,1)
Send("{SHIFTDOWN}{HOME}{SHIFTUP}36pt")
MouseClick("left",1083,574,1)
MouseClick("left",1082,602,1)
MouseClick("left",325,43,1)
_WinWaitActivate("Select Your Organization - Scripture","")
MouseClick("left",206,160,1)
_WinWaitActivate("Set Defaults - Scripture","")
MouseClick("left",171,42,1)
MouseClick("left",171,42,1)
MouseClick("left",174,75,1)
MouseClick("left",193,363,1)
MouseClick("left",146,100,1)
MouseClick("left",202,180,1)
Send("{SHIFTDOWN}a{SHIFTUP}5{SPACE}gap{SPACE}outside{SPACE}ma{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}centerhead{BACKSPACE}{BACKSPACE}{BACKSPACE}{BACKSPACE}ed{SPACE}head")
MouseClick("left",160,415,1)
MouseClick("left",190,462,1)
_WinWaitActivate("Pathway Configuration Tool - BTE","")
MouseClick("left",1141,2,1)

#region --- Internal functions Au3Recorder Start ---
Func _WinWaitActivate($title,$text,$timeout=0)
	WinWait($title,$text,$timeout)
	If Not WinActive($title,$text) Then WinActivate($title,$text)
	WinWaitActive($title,$text,$timeout)
EndFunc
#endregion --- Internal functions Au3Recorder End ---
