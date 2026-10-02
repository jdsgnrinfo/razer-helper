<#
.SYNOPSIS
    Adds the "Razer Blade" power plan: full performance for gaming and work,
    cool and quiet at rest, and longer on battery.

.DESCRIPTION
    Starts from Windows' own Balanced plan and changes only the processor:

      Energy performance preference   33 plugged in, 80 on battery
                                      (0 is all speed, 100 all efficiency;
                                      Balanced's own are 33 and 50)
      Minimum processor state         5 % (the CPU can rest when idle)
      Maximum processor state         100 %
      Processor boost (turbo)         Aggressive plugged in; on battery
                                      Efficient Enabled, only when it pays off
      Cores kept unparked             All plugged in; half on battery, so
                                      loads still start without a stall

    Everything else (screen, sleep, brightness) stays as Windows' Balanced
    has it. Running it again updates the plan instead of adding another.
    Needs no administrator rights. Works on any Windows 10 or 11 laptop,
    Intel or AMD; RazerHelper is not needed for it.

.PARAMETER Activate
    Also makes it the active plan.

.PARAMETER Remove
    Removes the plan (Windows switches to Balanced first if it is active).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\RazerBladePowerPlan.ps1 -Activate
#>
param(
    [switch]$Activate,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

# A fixed id, so the plan is found again on the next run.
$plan = 'e1811cef-6588-4a28-876f-cc20c84725f8'
$balanced = '381b4222-f694-41f0-9685-ff5bb260df2e'

$spanish = (Get-UICulture).TwoLetterISOLanguageName -eq 'es'
$name = 'Razer Blade'
$description = if ($spanish) {
    'Máximo rendimiento para jugar y trabajar, fresco y silencioso en reposo.'
} else {
    'Full performance for gaming and work, cool and quiet at rest.'
}

function Invoke-PowerCfg {
    # powercfg's own messages (a plan not found) are not errors here: the exit code says how it went.
    $ErrorActionPreference = 'Continue'
    & powercfg @args 2>&1 | Out-Null
    return $LASTEXITCODE -eq 0
}

$exists = Invoke-PowerCfg /query $plan

if ($Remove) {
    if (-not $exists) {
        Write-Host "The plan is not installed."
        return
    }

    if ((powercfg /getactivescheme) -match $plan) {
        Invoke-PowerCfg /setactive $balanced | Out-Null
    }

    if (-not (Invoke-PowerCfg /delete $plan)) { throw "Windows did not remove the plan." }
    Write-Host "Removed the '$name' plan."
    return
}

if (-not $exists) {
    if (-not (Invoke-PowerCfg /duplicatescheme $balanced $plan)) { throw "Windows did not create the plan." }
}

# setting alias, plugged in, on battery
$values = @(
    @('PERFEPP', 33, 80),          # Energy performance preference
    @('PERFEPP1', 33, 80),         # The same, for the efficiency cores of newer CPUs
    @('PROCTHROTTLEMIN', 5, 5),    # Minimum processor state, %
    @('PROCTHROTTLEMAX', 100, 100),# Maximum processor state, %
    @('PERFBOOSTMODE', 2, 3),      # Processor boost: Aggressive plugged in, Efficient Enabled on battery
    @('CPMINCORES', 100, 50)       # Cores kept unparked, %: all plugged in, half on battery
)

foreach ($value in $values) {
    $ok = (Invoke-PowerCfg /setacvalueindex $plan SUB_PROCESSOR $value[0] $value[1]) -and
          (Invoke-PowerCfg /setdcvalueindex $plan SUB_PROCESSOR $value[0] $value[2])

    # PERFEPP1 only exists on CPUs with two kinds of cores; the rest are everywhere.
    if (-not $ok -and $value[0] -ne 'PERFEPP1') {
        Write-Warning "Could not set $($value[0])."
    }
}

Invoke-PowerCfg /changename $plan $name $description | Out-Null

$active = (powercfg /getactivescheme) -match $plan

if ($Activate -or $active) {
    # Activating again applies the new values to a plan already in use.
    Invoke-PowerCfg /setactive $plan | Out-Null
}

$verb = if ($exists) { 'Updated' } else { 'Added' }
Write-Host "$verb the '$name' power plan."
Write-Host ($(if ($Activate -or $active) { 'It is the active plan now.' } else { 'Choose it in Control Panel > Power Options, or run this again with -Activate.' }))
