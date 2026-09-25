# Installation

There are several ways to install this RimWorld mod. As an end user, the most convenient way is to download it from the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3348840185).

## Manual Installation

1. Download the latest release from the [GitHub releases page](https://github.com/frederik-hoeft/rimworld-more-injuries/releases).
2. Extract the downloaded ZIP file to your RimWorld mods directory. This directory is usually located at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods` on Windows or `~/Library/Application Support/Steam/steamapps/common/RimWorld/Mods` on macOS.
3. Activate the mod in the RimWorld mod menu.

## Compiling from Source

If you are a developer and want to compile the mod from source, you will need to have the .NET SDK capable of targeting .NET Framework 4.8 and .NET 10 installed on your machine. The mod assembly targets `net48`. Do not use APIs that exist only on .NET Framework 4.8; the game still runs on Mono. Wiki generation and localization tests target `net10.0`. The easiest way to do this is by installing [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) (Community Edition is sufficient) and selecting the required workloads during installation. Alternatively, you can use Visual Studio Code or another compatible IDE, but you will need to ensure that you have the necessary .NET SDKs installed and properly configured. See [Microsoft's documentation](https://dotnet.microsoft.com/en-us/download) for more information on installing the .NET SDK.

> [!NOTE]
> While RimWorld runs on Unity and the Mono runtime, More Injuries targets `net48` and uses some more recent C# and Roslyn features that are shimmed in `Source/MoreInjuries/MoreInjuries/Roslyn`. Do not call APIs that exist only on .NET Framework 4.8. A recent .NET SDK is still required for the compiler and for the `net10.0` tooling projects.

Once you have the required tools installed, follow these steps to compile the mod from source:

1. Clone the repository to your local machine.
2. Open the solution file (`Source/MoreInjuries/MoreInjuries.slnx`) in Visual Studio or another compatible IDE.
3. Open `Source/MoreInjuries/MoreInjuries/_build/hostconfig.json` and adjust the `steam_root` property to point to the steam library containing your RimWorld installation. The steam library should directly contain a `steamapps` directory in which the RimWorld installation is located (`"<your steam root path>/steamapps/common/RimWorld"`).
4. Execute `Source/MoreInjuries/MoreInjuries/_build/deploy.ps1` with PowerShell to build and deploy the mod to your RimWorld mods directory. The script will automatically build the project, create the required mod directory structure in `<your steam root path>/steamapps/common/RimWorld/Mods/MoreInjuries`, and copy the necessary files. As part of the deployment, the script will download the old versions (e.g., 1.5) of the mod from the GitHub releases page and copy them into the necessary directories. You can double-check the script source code [here](/Source/MoreInjuries/MoreInjuries/_build/deploy.ps1) to see what it does.
5. Activate the mod in the RimWorld mod menu.