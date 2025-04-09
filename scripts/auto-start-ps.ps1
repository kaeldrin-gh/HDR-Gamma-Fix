$taskName = "Apply sRGB to Gamma LUT"
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask -ne $null) {
	Write-Host "Removing previous task"
	Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$exeFile = "dispwin.exe"
$arg1 = "lut.cal"

$action = New-ScheduledTaskAction -Execute $exeFile -Argument $arg1 -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings
