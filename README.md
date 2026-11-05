# BL for Linux is still in the making, the readme isnt fully updated!

![minecraft bedrock launcher logo](https://raw.githubusercontent.com/BedrockLauncher/BedrockLauncher.GitHub.io/main/src/assets/images/logos/logo.png)

---

An unofficial **Minecraft for Windows (Bedrock)** launcher for Linux that enables similar features from the **Minecraft Java Edition Launcher**, leveraging compatibility layers to run the Windows AppX/MSIX package.

[![Website](https://img.shields.io/github/v/tag/BedrockLauncher/BedrockLauncher.GitHub.io?color=blue&label=Visit%20Official%20Website&logo=github&style=for-the-badge)](https://bedrocklauncher.github.io/)
[![Download Release](https://shields.io)](https://github.com/BedrockLauncher/BedrockLauncher/releases/latest/)
[![Crowdin](https://img.shields.io/static/v1?color=282C34&labelColor=282C34&label=Crowdin&message=Translate&logo=crowdin&style=for-the-badge)](https://crowdin.com/project/bedrocklauncher)

## Linux Compatibility & Requirements

Since Minecraft Bedrock for PC is built natively as a Universal Windows Platform (UWP) app, running it on Linux requires specific translation layers. This launcher integrates with the following modern compatibility technologies:

* **Wine UWP / WineGDK:** The launcher utilizes advanced Wine forks specifically patched for UWP/GDK (Game Development Kit) support. This allows Linux environments to handle AppX deployment, Windows Store identity services, and DirectX 12 translation required by modern Bedrock builds.

## Prerequisites

This launcher requires specific hardware and software configurations to bridge the Windows-to-Linux gap with optimal performance:

- [Hardware prerequisites](./docs/HARDWARE_PREREQUISITES.md) (Dedicated GPU with Vulkan support recommended for DX12 translation)
- [Software prerequisites](./docs/SOFTWARE_PREREQUISITES.md) (Up-to-date Wine-GE/Proton, WineGDK dependencies, and Vulkan drivers)

## Disclaimers

We specify disclaimers to ensure we go "told ya!" if anything breaks due to updates in the Windows Store protocol or Wine compatibility:

- [Disclaimers](./docs/DISCLAIMERS.md)

## Get the launcher

Visit the official website or the GitHub releases page to download the Linux-compatible build (AppImage/Flatpak):

- [Official website](https://bedrocklauncher.github.io)

## Compiling the launcher yourself

You can compile the launcher yourself to prototype, iterate, or just enjoy the cutting edge Linux build:

- [Compiling instructions](./docs/COMPILING.md)

## Screenshots

Have a look around the launcher before you download on the official website:

- [Screenshots](https://bedrocklauncher.github.io)

## Credits

It's hard to keep the credits page updated on GitHub, so visit the credits page on the official website:

- [Credits page](https://bedrocklauncher.github.io/credits)
