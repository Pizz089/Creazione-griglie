# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**DirectX .X Editor 3D** — A WPF application for editing 3D bowling graphics styles used by Steltronic's Focus bowling management system. It reads DirectX `.x` mesh files, lets users modify materials/textures, and saves changes back to the style folder.

## Build & Run

```bash
# Build
dotnet build "Creazione griglie/Creazione griglie.csproj"

# Run (Debug)
dotnet run --project "Creazione griglie/Creazione griglie.csproj"

# Publish as single file (not self-contained)
dotnet publish "Creazione griglie/Creazione griglie.csproj" -c Release -r win-x64
```

There are no tests. The project targets `net8.0-windows` and requires WPF (Windows only). The single NuGet dependency is `HelixToolkit.Wpf`.

## Architecture

### Entry Point & Startup
`App.xaml.cs` calls `EmbeddedResourceManager.EstraiRisorseSeNecessario()` before the UI starts. This extracts `BaseStyles/**/*` (embedded as assembly resources) to `%TEMP%\CreazioneGriglie_Resources\BaseStyles`. These are the factory default style templates shipped inside the EXE.

### Main Window (`MainWindow.xaml.cs`)
Three-panel layout:
- **Left**: `TreeView` hierarchy of style components (`alberoGerarchico`, an `ObservableCollection<MeshData>`)
- **Center**: Gallery of 3D thumbnails (`pannelloGalleria`) or fullscreen HelixToolkit viewport (`pannelloMassimizzato`)
- **Right**: Properties panel for color, texture, and texture zoom

### Core Data Model (`Classi di funzionamento/MeshData.cs`)
`MeshData` is both a tree node (with `Children`) and a 3D mesh descriptor. Key distinction: nodes with `OriginalXFileContent != null` are actual `.x` files; nodes where `IsGroup=true` and `OriginalXFileContent` is empty/null are folder nodes. The tree mirrors the physical folder hierarchy of a style.

### Style System
Styles live at:
- **Local**: `C:\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles`
- **Network server** (auto-detected by `Focus.lnk` on the desktop): `\\10.11.1.1\c$\Program Files (x86)\Steltronic\Vision\MediaNova\MeshBase\Styles`

`StyleEnvironment.cs` handles path discovery and style protection rules:
- `style#0` through `style#12`, plus `style#L1/L2/L3` = **factory protected** (read-only, Save is disabled)
- User custom styles start at `style#13` and fill gaps

### File Loading Flow
1. `BtnCarica_Click` → `StyleSelectorWindow` (folder picker dialog) → `EseguiCaricamentoFisico()`
2. `XFileEngine.Parse()` converts raw `.x` text into a `List<MeshData>` tree
3. `XmlHelper.LeggiCoordinateDaXML()` reads position/rotation/scale from `.xml` files co-located in the style folder
4. `XmlHelper.ConfiguraLuciDaXML()` sets up the 3D scene lighting
5. Gallery is populated by `MostraGalleria()` which builds `GalleriaGroup` objects bound to `listaGruppiGalleria`

### 3D Rendering
`HelixToolkit.Wpf` (`HelixViewport3D`) is used for:
- Per-thumbnail mini-viewports in the gallery (one `HelixViewport3D` per `ViewportItem`)
- The fullscreen `viewPortMassimizzato` for inspecting individual components

`MeshHelper.CreaMaterialeWPF()` builds WPF `Material` objects from `MeshData` properties, handling color overrides, texture loading, and texture UV scaling.

### Undo System
`UndoManager` + `MementoState` implement a memento pattern. Before any material change, `RegistraStatoUndo()` snapshots affected `MeshData` instances. Max 50 history states. `MementoState.Restore()` re-creates the WPF material after restoring properties.

### Localization & Theming
- `App.xaml` merges three resource dictionaries: `Tema.xaml` (colors), `Stili.xaml` (control styles), and `Lingue/Stringhe_IT.xaml` (Italian strings)
- All UI strings use `{DynamicResource StrXxx}` keys defined in the language files
- `Lingue/Stringhe_EN.xaml` exists for English but is not currently wired up

### Supported Bowling Game Types
The loader recognizes subfolders by name: `p10` (10 Pin), `p5` (5 Pin), `duck` (Duck Pin), `candle` (Candle Pin). Files starting with `t_` are excluded (they are frame-type animation frames). `recap.x` at the style root is always included.
