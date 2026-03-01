# XLOTP

[![downloads](https://badgen.net/github/assets-dl/AEBus/XLOTP)](https://github.com/AEBus/XLOTP/releases)
[![release](https://badgen.net/github/release/AEBus/XLOTP)](https://github.com/AEBus/XLOTP/releases)
[![license](https://badgen.net/github/license/AEBus/XLOTP)](./LICENSE)

XLOTP is a small OTP helper for XIVLauncher.  
You keep one OTP secret per profile, then launch the right account and auto-send the right OTP with one command (or one shortcut click).

IMPORTANT FIRST STEP (inside XIVLauncher):
- Open `XIVLauncher.exe`
- Click the gear icon (`Settings`)
- Enable `XL Authenticator app/OTP macro support`

If this is not enabled, `XLOTP --send` cannot auto-fill OTP in XIVLauncher.

Runtime requirement:
- The default slim build requires `.NET 10 Runtime`
- Download: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

## What You Get
- Profile-based OTP storage (`account1`, `account2`, etc.)
- TOTP and HOTP generation
- Direct OTP send to XIVLauncher listener (`/ffxivlauncher/<otp>`)
- Interactive mode (`XLOTP.exe`) and script mode (`XLOTP.exe --send ...`)
- Safe defaults (loopback-only send unless explicitly overridden)

## Build and Publish

Build:
```powershell
cd e:\GitHub\XLOTP
dotnet build
```

Default publish (slim):
```powershell
dotnet publish src\XLOTP\XLOTP.csproj
```

Output:
`src\XLOTP\bin\Release\net10.0\win-x64\publish\`

Optional single-file self-contained EXE:
```powershell
dotnet publish src\XLOTP\XLOTP.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## How To Run

Interactive menu:
```powershell
XLOTP.exe
```

Standard command format:
```powershell
XLOTP.exe --configure [options]
XLOTP.exe --profiles [options]
XLOTP.exe --code [options]
XLOTP.exe --send [options]
```

## Real Example: 3 FFXIV Accounts

Setup:
- `account1` = Steam
- `account2` = Windows / SE client
- `account3` = Windows / SE client with separate XL config at `%APPDATA%\account3`
- Launcher path: `%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe`

One-time OTP profile setup:
```powershell
XLOTP.exe --configure --profile account1 --secret <BASE32_1> --label "account1 (Steam)" --default
XLOTP.exe --configure --profile account2 --secret <BASE32_2> --label "account2 (Windows)"
XLOTP.exe --configure --profile account3 --secret <BASE32_3> --label "account3 (Windows, separate roaming)"
```

Daily login commands:
```powershell
# account1 (Steam)
XLOTP.exe --send --profile account1 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args="--account=account1-True-True"

# account2 (Windows)
XLOTP.exe --send --profile account2 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args="--account=account2-True-False"

# account3 (Windows + separate roaming path)
XLOTP.exe --send --profile account3 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args="--account=account3-True-False --roamingPath=%APPDATA%\account3"
```

Practical flow:
click shortcut -> XLOTP starts -> it opens XIVLauncher with correct account -> you press Log in (OTP dialog opens and listener starts) -> XLOTP sends correct OTP -> game starts.

## Windows Shortcut Setup

Create one shortcut per account:
1. Desktop -> `New` -> `Shortcut`
2. Put full command in `Target`
3. Set `Start in` to your XLOTP folder

Example `Start in`:
```text
C:\Tools\XLOTP
```

Example `Target`:
```text
"C:\Tools\XLOTP\XLOTP.exe" --send --profile account1 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args="--account=account1-True-True"
```

Tip: if quoting gets messy, use small `.cmd` files and point shortcuts to those.

## Commands (Quick Reference)

## `--configure`
Save or update OTP secret for a profile.

Required:
- `--secret <base32>`

Common options:
- `--profile <name>` (default: `default`)
- `--default`
- `--label <text>`
- `--digits <4..10>`
- `--period <seconds>`
- `--algo <sha1|sha256|sha512>`
- `--scope <user|machine>`
- `--allow-plaintext`
- `--config <path>`

## `--profiles`
List/manage profiles.

Options:
- `--set-default <name>`
- `--remove <name>`
- `--config <path>`

## `--code`
Generate OTP code.

Useful options:
- `--profile <name>`
- `--secret <base32>` (one-shot)
- `--time <ISO8601>` / `--unix-time <seconds>`
- `--counter <value>` (HOTP)
- `--digits`, `--period`, `--algo`, `--config`

## `--send`
Generate (or use) OTP and send it to XIVLauncher.

Useful options:
- `--profile <name>`
- `--code <value>`
- `--launcher <path>`
- `--launcher-args=<text>`
- `--launcher-delay <seconds>`
- `--retries <n>`
- `--retry-delay <seconds>`
- `--timeout <seconds>`
- `--server <url>` / `--path <path>`
- `--allow-remote-server`
- `--print` / `--echo`

## Config Path
- Default: `%APPDATA%\XLOTP\config.json`
- Main fields: `version`, `defaultProfile`, `profiles.<name>.*`

## Security Notes
- On Windows, persisted secrets are protected with DPAPI
- Default scope is user-level (`--scope user`)
- Non-Windows encrypted persistence is unavailable (use `--allow-plaintext` only if you accept that risk)
- Send target is loopback-only by default
- Output redacts target URL as `<otp-redacted>`

## Troubleshooting

`Configuration file was not found`
- Run `--configure` first or use `--secret` one-shot mode

`Profile '<name>' was not found`
- Run `--profiles` and check default/profile names

`Refusing to send OTP to non-loopback server`
- Use local listener or explicitly pass `--allow-remote-server`

XIVLauncher does not receive OTP:
- Verify XIVLauncher OTP macro support is enabled
- Verify launcher is running
- OTP listener starts when the OTP dialog is open (after pressing `Log in`)
- Pass launcher args in one token, for example `--launcher-args="--account=... --roamingPath=..."`
- Increase delay/retries:
```powershell
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args="--account=main-True-False" --launcher-delay 2 --retries 120 --retry-delay 1
```
