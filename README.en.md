# Tarkov Boss Monitor

[한국어](README.ko.md) · **English** · [简体中文](README.zh-CN.md)

A Windows x64 app that detects boss spawns in official PvE raids running locally on your PC.

## Features

- Detects the current map from raid logs
- Lets you choose bosses to monitor for each map
- Supports installation while the game is running
- Cleans old game logs safely with a Windows scheduled task

## Download

[Download the latest release](https://github.com/seongjin-lab/tarkov-boss/releases/latest)

The installer is not code-signed, so Windows SmartScreen may display a warning.

## Screenshot

<img src="screenshot.png" alt="Tarkov Boss Monitor boss spawn detection screen" width="600">

## Launcher repair warning

To obtain detailed AI spawn logs, Tarkov Boss Monitor changes the `aiData` log level in `Logging.config`. BSG Launcher may therefore identify this file as modified and display a **game repair required** warning.

If the warning appears, let the launcher finish repairing the game, exit the game completely, reopen Tarkov Boss Monitor and reapply the logging settings, and then restart the game. Repeat these steps whenever a game update or integrity check restores `Logging.config` to the original version.

## Uninstall

Exit Escape from Tarkov completely, then select **Uninstall Tarkov Boss Monitor** from the Start menu or remove Tarkov Boss Monitor from **Windows Settings → Apps → Installed apps**.

The uninstaller removes the log-cleanup scheduled task and automatically restores the `Logging.config` log levels changed by this app to their pre-installation values. If the game is running or the logging configuration has changed since installation, automatic restoration is skipped for safety and the uninstall result shows the backup location.

## Support

- Supported: official PvE raids that run locally and produce detailed AI logs
- Not supported: PvP, online PvE, and maps that do not support local play
- Maps such as Streets of Tarkov, which cannot be played locally, are not supported
- Current position and survival-state detection are not guaranteed

## Build

Requirements: Windows 10 or later, .NET 8 SDK, and Inno Setup 6.

```powershell
.\build.ps1
```

The installer is created in `outputs/installer`.

## License

This project is licensed under the [MIT License](LICENSE).

This is an unofficial tool and is not affiliated with Battlestate Games or Escape from Tarkov.
