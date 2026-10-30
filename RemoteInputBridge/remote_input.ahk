RemoteInputBridgePath() {
	EnvGet, configuredPath, GRPC_REMOTE_INPUT_BRIDGE
	if (configuredPath != "")
		Return configuredPath
	SplitPath, A_LineFile,, helperDir
	Return helperDir . "\RemoteInputBridge.exe"
}

RemoteInputInitialize() {
	OnExit("RemoteInputOnExit")
	Return RemoteInputOpen()
}

RemoteInputOpen() {
	global RemoteInputHandle
	if (RemoteInputHandle)
		Return true
	Loop, 200
	{
		handle := DllCall("CreateFile", "Str", "\\.\pipe\GRPCRemoteInput", "UInt", 0xC0000000, "UInt", 0, "Ptr", 0, "UInt", 3, "UInt", 0, "Ptr", 0, "Ptr")
		if (handle != -1) {
			RemoteInputHandle := handle
			Return true
		}
		if (A_Index = 1) {
			bridge := RemoteInputBridgePath()
			if (!FileExist(bridge))
				Return false
			Run, "%bridge%" listen,, Hide UseErrorLevel
			if (ErrorLevel)
				Return false
		}
		Sleep, 10
	}
	Return false
}

RemoteInputOnExit(exitReason, exitCode) {
	RemoteInputClose()
}

RemoteInputClose() {
	global RemoteInputHandle
	if (RemoteInputHandle) {
		DllCall("CloseHandle", "Ptr", RemoteInputHandle)
		RemoteInputHandle := 0
	}
}

RemoteInputWrite(action, key) {
	global RemoteInputHandle, RemoteInputLastError
	RemoteInputLastError := ""
	if (InStr(key, "`n") || InStr(key, "`r") || InStr(key, "|")) {
		RemoteInputLastError := "Invalid key"
		Return false
	}
	command := action . "|" . key . "`n"
	result := DllCall("WriteFile", "Ptr", RemoteInputHandle, "AStr", command, "UInt", StrLen(command), "UIntP", written, "Ptr", 0)
	if (!result || written != StrLen(command)) {
		RemoteInputLastError := "Pipe write failed"
		RemoteInputClose()
		Return false
	}
	started := A_TickCount
	reply := ""
	Loop {
		if (!DllCall("PeekNamedPipe", "Ptr", RemoteInputHandle, "Ptr", 0, "UInt", 0, "Ptr", 0, "UIntP", available, "Ptr", 0))
			break
		if (available) {
			VarSetCapacity(buffer, 1, 0)
			if (!DllCall("ReadFile", "Ptr", RemoteInputHandle, "Ptr", &buffer, "UInt", 1, "UIntP", read, "Ptr", 0) || read != 1)
				break
			character := Chr(NumGet(buffer, 0, "UChar"))
			if (character = "`n") {
				reply := RTrim(reply, "`r")
				if (reply = "ok")
					Return true
				RemoteInputLastError := reply
				Return false
			}
			reply .= character
			if (StrLen(reply) > 8192)
				break
		} else {
			if (A_TickCount - started >= 3000)
				break
			Sleep, 1
		}
	}
	RemoteInputLastError := "Pipe reply failed or timed out"
	RemoteInputClose()
	Return false
}

RemoteInputKey(action, key) {
	global RemoteInputLastError
	previousCritical := A_IsCritical
	Critical, On
	try {
		if (!RemoteInputOpen()) {
			RemoteInputLastError := "Cannot connect to RemoteInputBridge"
			Return false
		}
		Return RemoteInputWrite(action, key)
	} finally {
		Critical, %previousCritical%
	}
}

RemoteInputPress(key) {
	Return RemoteInputKey("press", key)
}

RemoteInputDown(key) {
	Return RemoteInputKey("down", key)
}

RemoteInputUp(key) {
	Return RemoteInputKey("up", key)
}
