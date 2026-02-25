# XLOTP

XLOTP is a small helper app that deals with OTP codes and can auto-send them into XIVLauncher.

So instead of opening your phone / password manager every login, you can do one click and let it handle OTP delivery.

IMPORTANT FIRST STEP (in XIVLauncher, not in XLOTP):
- Open `XIVLauncher.exe`
- Click the gear icon (`Settings`)
- Enable: `XL Authenticator app/OTP macro support`

If this option is off, `XLOTP --send` cannot auto-fill OTP into XIVLauncher.

Runtime requirement:
- Slim/default build requires installed `.NET 10 Runtime`.
- Official download page: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

## What this thing does

- Stores OTP secrets in profiles (so one profile per account is easy).
- Generates TOTP/HOTP codes.
- Sends OTP directly to XIVLauncher listener (`/ffxivlauncher/<otp>`).
- Works in interactive mode (menu) or command mode (scripts/shortcuts).

## Quick start

### Build

```powershell
cd e:\GitHub\XLOTP
dotnet build
```

### Publish (default is slim)

```powershell
dotnet publish src\XLOTP\XLOTP.csproj
```

Output:

`src\XLOTP\bin\Release\net10.0\win-x64\publish\`

### Optional: single-file EXE

```powershell
dotnet publish src\XLOTP\XLOTP.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## How to run it

### Option 1: interactive menu

```powershell
XLOTP.exe
```

You get a menu where you can:
1. Configure profile
2. Show profiles
3. Set default profile
4. Generate OTP
5. Send OTP to XIVLauncher
6. Exit

### Option 2: command mode

```powershell
XLOTP.exe --configure [options]
XLOTP.exe --profiles [options]
XLOTP.exe --code [options]
XLOTP.exe --send [options]
```

## Real setup example (3 FFXIV accounts)

Say your setup is:
- `account1` = Steam
- `account2` = Windows / SE client
- `account3` = Windows / SE client with separate XL config at `%APPDATA%\account3`
- Launcher path is standard: `%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe`

One-time secret setup:

```powershell
XLOTP.exe --configure --profile account1 --secret <BASE32_1> --label "account1 (Steam)" --default
XLOTP.exe --configure --profile account2 --secret <BASE32_2> --label "account2 (Windows)"
XLOTP.exe --configure --profile account3 --secret <BASE32_3> --label "account3 (Windows, separate roaming)"
```

Daily login commands:

```powershell
# account1 (Steam)
XLOTP.exe --send --profile account1 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args "--account account1-True-True"

# account2 (Windows)
XLOTP.exe --send --profile account2 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args "--account account2-True-False"

# account3 (Windows + separate roaming path)
XLOTP.exe --send --profile account3 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args "--account account3-True-False --roamingPath ""%APPDATA%\account3"""
```

Real flow is basically:
- click shortcut
- XLOTP starts
- it opens XIVLauncher with the right account
- you press Log in
- XLOTP sends correct OTP
- game starts

No manual OTP copy/paste every login.

## Shortcut setup (Windows)

If you want clean one-click launch:

1. Desktop -> Right click -> `New` -> `Shortcut`
2. Put full command in `Target`
3. Set `Start in` to your XLOTP folder
4. Make one shortcut per account

Example `Start in`:

```text
C:\Tools\XLOTP
```

Example `Target` for account1:

```text
"C:\Tools\XLOTP\XLOTP.exe" --send --profile account1 --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args "--account account1-True-True"
```

Tip: if shortcut quoting is annoying, use small `.cmd` files and point shortcuts to those scripts.

## Command reference (short + practical)

## `--configure`

Use this to save/update OTP secret in a profile.

Required:
- `--secret <base32>`

Common options:
- `--profile <name>` (default: `default`)
- `--default` (set default profile)
- `--label <text>`
- `--digits <4..10>`
- `--period <seconds>`
- `--algo <sha1|sha256|sha512>`
- `--scope <user|machine>` (Windows DPAPI)
- `--allow-plaintext` (non-Windows / explicit insecure storage)
- `--config <path>`

Example:

```powershell
XLOTP.exe --configure --profile main --secret JBSWY3DPEHPK3PXP --default
```

## `--profiles`

Use this to list/manage profiles.

Options:
- `--set-default <name>`
- `--remove <name>`
- `--config <path>`

Examples:

```powershell
XLOTP.exe --profiles
XLOTP.exe --profiles --set-default main
XLOTP.exe --profiles --remove alt
```

## `--code`

Print OTP code.

Useful options:
- `--profile <name>`
- `--secret <base32>` (one-shot)
- `--time <ISO8601>` / `--unix-time <seconds>`
- `--counter <value>` (HOTP mode)
- plus `--digits`, `--period`, `--algo`, `--config`

Examples:

```powershell
XLOTP.exe --code --profile main
XLOTP.exe --code --profile main --time 2026-01-01T00:00:00Z
XLOTP.exe --code --secret JBSWY3DPEHPK3PXP --counter 1234
```

## `--send`

Generate (or use) OTP and push it to XIVLauncher.

Useful options:
- `--profile <name>`
- `--code <value>` (manual OTP)
- `--launcher <path>`
- `--launcher-args <text>`
- `--launcher-delay <seconds>`
- `--retries <n>`
- `--retry-delay <seconds>`
- `--timeout <seconds>`
- `--server <url>` / `--path <path>`
- `--allow-remote-server` (off by default for safety)
- `--print` / `--echo`

Example:

```powershell
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe"
```

## Config file

Default path:
- `%APPDATA%\XLOTP\config.json`

Main fields you’ll see:
- `version`
- `defaultProfile`
- `profiles.<name>.*`

## Security notes (important)

- On Windows, secrets are protected by DPAPI.
- Default scope is user-level (`--scope user`).
- Non-Windows encrypted storage is not available in this app; use `--allow-plaintext` only if you accept that risk.
- `--send` is loopback-only by default (you must explicitly allow remote).
- Output redacts OTP target URL as `<otp-redacted>`.

## Troubleshooting

"Configuration file was not found"
- Run `--configure` first or pass `--secret` for one-shot use.

"Profile '<name>' was not found"
- Run `--profiles` and verify profile name/default profile.

"Refusing to send OTP to non-loopback server"
- Use local listener, or pass `--allow-remote-server` if you really need remote.

XIVLauncher doesn’t receive OTP
- Make sure OTP macro support is enabled in XIVLauncher settings.
- Make sure launcher is actually running.
- Increase delay/retries:

```powershell
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-delay 3 --retries 20
```

## License

See repository license file.
