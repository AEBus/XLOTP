# XLOTP

XLOTP is a lightweight OTP utility for **TOTP/HOTP** workflows and **XIVLauncher OTP auto-fill** integration.

It can:
- store OTP secrets securely (DPAPI on Windows),
- manage multiple profiles (one secret per account/workflow),
- generate TOTP or HOTP codes,
- push codes to XIVLauncher local OTP listener (`/ffxivlauncher/<otp>`),
- run as either an interactive app or automation-friendly command tool.

## Key Capabilities

- Multi-profile configuration in one JSON file.
- TOTP/HOTP code generation with configurable:
  - digits (`4..10`),
  - period,
  - algorithm (`SHA1`, `SHA256`, `SHA512`).
- Secure secret storage on Windows via DPAPI (`CurrentUser` or `LocalMachine`).
- Optional plaintext storage mode for non-Windows (`--allow-plaintext`).
- OTP send-to-launcher flow with retries, timeout, launcher auto-start.
- Safe defaults for send target (loopback-only unless explicitly overridden).
- Atomic config writes and basic file-permission hardening on Unix-like systems.

## Runtime and Platform

- Target framework: `.NET 10` (`net10.0`).
- Project defaults for `dotnet publish`:
  - `Release`
  - `win-x64`
  - framework-dependent (`SelfContained=false`)

## Installation / Build

### Build

```powershell
cd e:\GitHub\XLOTP
dotnet build
```

### Default slim publish (recommended for smallest disk footprint)

```powershell
dotnet publish src\XLOTP\XLOTP.csproj
```

Output:

`src\XLOTP\bin\Release\net10.0\win-x64\publish\`

### Single-file self-contained publish (optional)

```powershell
dotnet publish src\XLOTP\XLOTP.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## How to Run

### Interactive mode

Run with no arguments:

```powershell
XLOTP.exe
```

Interactive menu options:
1. Configure profile
2. Show profiles
3. Set default profile
4. Generate OTP code
5. Send OTP to XIVLauncher
6. Exit

### Automation mode (single-exe flags)

```powershell
XLOTP.exe --configure [options]
XLOTP.exe --profiles [options]
XLOTP.exe --code [options]
XLOTP.exe --send [options]
```

### Legacy command syntax (still supported)

```powershell
XLOTP.exe configure [options]
XLOTP.exe profiles [options]
XLOTP.exe code [options]
XLOTP.exe send [options]
```

## Command Reference

## `configure`

Create or update a profile secret and OTP parameters.

Required:
- `--secret <base32>`

Optional:
- `--profile <name>` (default: `default`)
- `--default` (set profile as default)
- `--label <text>`
- `--digits <number>` (`4..10`, default: `6`)
- `--period <seconds>` (minimum `5`, default: `30`)
- `--algo <sha1|sha256|sha512>` (default: `sha1`)
- `--config <path>`
- `--scope <user|machine>` (Windows DPAPI scope)
- `--allow-plaintext` (required on non-Windows if you want persisted secrets)

Examples:

```powershell
# Create main profile and make it default
XLOTP.exe --configure --profile main --secret JBSWY3DPEHPK3PXP --default

# Create secondary profile with custom params
XLOTP.exe --configure --profile alt --secret MZXW6YTBOI====== --label "Alt Account" --algo sha256 --digits 6 --period 30

# Use custom config path
XLOTP.exe --configure --config D:\otp\xlotp.json --profile raid --secret ABCDEFGH12345678 --default
```

## `profiles`

Inspect and manage profile list.

Optional:
- `--config <path>`
- `--set-default <name>`
- `--remove <name>`

Examples:

```powershell
# List all profiles
XLOTP.exe --profiles

# Set default profile
XLOTP.exe --profiles --set-default main

# Remove profile (cannot remove the last remaining profile)
XLOTP.exe --profiles --remove alt
```

## `code`

Generate and print an OTP code.

Optional:
- `--profile <name>`
- `--config <path>`
- `--secret <base32>` (one-shot, bypass config)
- `--digits <number>`
- `--period <seconds>`
- `--algo <sha1|sha256|sha512>`
- `--time <iso8601|now>`
- `--unix-time <seconds>`
- `--counter <value>` (HOTP mode)

Notes:
- If `--counter` is provided, HOTP generation is used.
- Otherwise, time-based TOTP is used.

Examples:

```powershell
# Generate code from default profile
XLOTP.exe --code

# Generate code from specific profile
XLOTP.exe --code --profile main

# Generate code for a specific timestamp
XLOTP.exe --code --profile main --time 2026-01-01T00:00:00Z

# HOTP-style generation
XLOTP.exe --code --secret JBSWY3DPEHPK3PXP --counter 1234

# Override OTP parameters on the fly
XLOTP.exe --code --profile main --algo sha512 --digits 8 --period 45
```

## `send`

Generate (or accept) a code and send it to XIVLauncher OTP listener.

Optional:
- `--profile <name>`
- `--code <value>` (use provided code instead of generating)
- `--secret`, `--config`, `--digits`, `--period`, `--algo`, `--time`, `--unix-time`, `--counter`
- `--server <url>` (default: `http://127.0.0.1:4646`)
- `--path <path>` (default: `/ffxivlauncher/`)
- `--allow-remote-server` (unsafe; disables loopback-only guard)
- `--retries <number>` (default: `10`)
- `--retry-delay <seconds>` (default: `0.75`)
- `--timeout <seconds>` (default: `3`)
- `--launcher <path>` (start launcher before sending)
- `--launcher-args <text>`
- `--launcher-delay <seconds>` (default: `2`)
- `--print` / `--echo` (also print OTP to stdout)

Examples:

```powershell
# Standard send using default profile
XLOTP.exe --send

# Send from explicit profile and auto-start XIVLauncher
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe"

# Send and choose launcher account
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-args "--account myaccount"

# Custom retry and timeout behavior
XLOTP.exe --send --profile main --retries 20 --retry-delay 0.5 --timeout 2

# Push a manually provided OTP
XLOTP.exe --send --code 123456 --print

# Custom listener endpoint
XLOTP.exe --send --server http://127.0.0.1:4646 --path /ffxivlauncher/
```

## XIVLauncher Setup

1. In XIVLauncher, enable:
   - `Settings -> Enable XL Authenticator app/OTP macro support`
2. (Optional) enable auto-login flow in launcher settings.
3. Trigger `XLOTP.exe --send ...` from your shortcut/script/StreamDeck.

## Configuration File

Default path:
- `%APPDATA%\XLOTP\config.json`

Main fields:
- `version`
- `defaultProfile`
- `profiles.<name>.label`
- `profiles.<name>.protectedSecret`
- `profiles.<name>.secretIsPlainText`
- `profiles.<name>.protectionScope`
- `profiles.<name>.algorithm`
- `profiles.<name>.periodSeconds`
- `profiles.<name>.digits`
- `profiles.<name>.createdUtc`
- `profiles.<name>.updatedUtc`

## Security Notes

- On Windows, DPAPI encryption protects persisted secrets at OS level.
- `--scope user` (default) limits decryption to the same user context.
- `--scope machine` allows machine-wide DPAPI scope.
- On non-Windows, encrypted persistence is unavailable; use `--allow-plaintext` only if you accept that risk and protect files externally.
- OTP send output redacts full URL target as `<otp-redacted>`.
- By default, OTP send is blocked for non-loopback servers.

## Troubleshooting

### "Configuration file was not found"
Run `--configure` first or pass `--secret` directly for one-shot operations.

### "Profile '<name>' was not found"
Run `--profiles` to list available profiles, or set the correct default profile.

### "Refusing to send OTP to non-loopback server"
Use a localhost listener, or explicitly pass `--allow-remote-server` if you understand the security tradeoff.

### XIVLauncher does not receive OTP
- Ensure XIVLauncher is running.
- Ensure OTP macro support is enabled.
- Increase retries and launcher delay:

```powershell
XLOTP.exe --send --profile main --launcher "%LOCALAPPDATA%\XIVLauncher\XIVLauncher.exe" --launcher-delay 3 --retries 20
```

## License

See repository license file.
