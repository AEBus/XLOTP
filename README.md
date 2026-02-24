# XLOTP

A utility that generates one-time passwords (TOTP/HOTP) compatible with WinAuth and can automatically send them to [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) using `XL Authenticator app/OTP macro support`.

## Features

- Multiple OTP profiles (one per login/account) in a single config.
- Square Enix secret (base32) storage with DPAPI encryption (or plaintext when explicitly requested for non-Windows systems).
- One-time code generation (WinAuth-compatible) with SHA1/SHA256/SHA512, custom periods, and custom digit length.
- Automatic code delivery to XIVLauncher built-in HTTP listener (`http://127.0.0.1:4646/ffxivlauncher/<OTP>`), including retries and optional launcher start.
- Script-friendly behavior with readable error messages for PowerShell and batch automation.

## Build

```powershell
cd e:\GitHub\XLOTP
dotnet build
```

The project targets `.NET 10` (`net10.0`).

The built executable is in `src\XLOTP\bin\Debug\net10.0\XLOTP.exe`.

## Default Publish (Release + Slim)

The project is configured with default publish settings:
- `Release`
- `win-x64`
- framework-dependent (`SelfContained=false`)

Run:

```powershell
dotnet publish src\XLOTP\XLOTP.csproj
```

Output (slim package, minimal disk size):
`src\XLOTP\bin\Release\net10.0\win-x64\publish\`

## Single EXE (Self-Contained)

By default, `XLOTP.exe` starts in interactive mode (menu), where you can:
- configure profiles,
- view/change default profile,
- generate a code,
- send a code to XIVLauncher.

To publish a single self-contained EXE:

```powershell
dotnet publish src\XLOTP\XLOTP.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `src\XLOTP\bin\Release\net10.0\win-x64\publish\XLOTP.exe`.

## Secret Configuration (Profiles)

1. Get the base32 secret from WinAuth (export text or view the Square Enix token settings).
2. Create a profile:

```powershell
XLOTP.exe --configure --profile main --secret YOUR_BASE32_SECRET --default
XLOTP.exe --configure --profile alt --secret YOUR_ALT_SECRET
```

Default parameters are 6 digits, 30s period, SHA1. Config is stored at `%APPDATA%\XLOTP\config.json`. On Windows, secrets are encrypted with DPAPI (`--scope user|machine`). On non-Windows systems, use `--allow-plaintext`.

List profiles:

```powershell
XLOTP.exe --profiles
XLOTP.exe --profiles --set-default main
```

## Generate Code Manually

```powershell
XLOTP.exe --code --profile main
```

Additional options are available: `--time`, `--unix-time`, `--counter`, `--secret`, `--config`, `--profile`, `--digits`, `--period`, `--algo`.

## Send to XIVLauncher

```powershell
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe"
```

This command:

1. Optionally starts XIVLauncher and waits `--launcher-delay` seconds (default: 2).
2. Generates OTP (or uses `--code`).
3. Sends up to `--retries` HTTP GET requests to `--server` + `--path` (default `http://127.0.0.1:4646/ffxivlauncher/`), matching XL Authenticator macro support behavior.

Flags `--print` (or `--echo`) also print OTP to stdout for manual fallback.
By default, sending is allowed only to localhost/127.0.0.1/::1; for a remote endpoint use `--allow-remote-server`.

## QuickLauncher Integration

1. In XIVLauncher, enable `Settings -> Enable XL Authenticator app/OTP macro support`.
2. Optionally enable `Log in automatically` to open OTP flow without extra manual steps.
3. Add `XLOTP.exe --send ...` to a shortcut, AHK/PowerShell script, or StreamDeck button. The command sends OTP to local port 4646, similar to the 1Password example in `FFXIVQuickLauncher/misc/1password-cli-otp`.

## Defaults and Files

- Config path: `%APPDATA%\XLOTP\config.json`.
- Config fields: `defaultProfile`, `profiles.<name>.*`.
- You can override config location with `--config <file>` for all commands.

## Security

- On Windows, secrets are encrypted with OS-level DPAPI. Decryption requires the same user (or machine if `--scope machine` is used).
- On other platforms, you can store plaintext secrets (`--allow-plaintext`) and protect files externally (BitLocker, LUKS, etc.).
- Make sure unauthorized users cannot access `%APPDATA%\XLOTP`.
- URI output in logs/stdout is redacted as `<otp-redacted>` to avoid leaking the full OTP.

## WinAuth Compatibility

HOTP/TOTP and Base32 implementation behavior is aligned with WinAuth sources, so generated codes match for identical secret/algorithm/period settings. You can export a secret from WinAuth and use it directly.
