# STARS Project
Amateur and professional astronomers use star maps as a guide for locating stars, planets, and other celestial objects. **STARS** is a software system that generates on-screen displays and printable star maps for observers anywhere on Earth, for any given date and time.

---

## System Overview

STARS takes a geographic location and point in time as input and renders a 3D interactive sky map accurate to those parameters. The general workflow is:

1. **Launch** the application
2. **Choose** Quick Start or Custom Setup from the Start Menu
3. **Enter** your location, date, and time on the Input Screen (Custom Setup only)
4. **Explore** the Sky Scene, which was rendered using your inputs
5. **Export** the view as an image or printable star chart

---

## Installation

### Windows / Linux

1. Download the appropriate build for your OS:
   - **Windows:** `STARSProject-StandaloneWindows64-vX.X.X.zip`
   - **Linux:** `STARSProject-StandaloneLinux64-vX.X.X.zip`
2. Extract the contents of the zip file.
3. Run `STARSProject.exe`.

> **Note:** Unity does not need to be installed, this software is fully self-contained.

### Verifying Software Integrity

To confirm your download hasn't been tampered with, compare the checksum of your zip file against the provided `.sha256` file.

On **Windows (PowerShell)**:
```powershell
Get-FileHash .\STARSProject-StandaloneWindows64-vX.X.X.zip -Algorithm SHA256
```

Compare the output hash to the contents of the `.sha256` file. If they match, the software is unaltered.

---

## Getting Started

When you launch `STARSProject.exe`, you'll be greeted by the **Start Menu**, which offers two options:

| Option | Description |
|--------|-------------|
| **Quick Start** | Jumps directly to the Sky Map using the default *Planetary Parade* event |
| **Custom Setup** | Opens the Input Screen to configure your own location, date, and time |

---

## GPU Acceleration

STARS uses a dual-rendering pipeline, letting you balance stability and performance:

| Mode | Description |
|------|-------------|
| **Toggle OFF** *(default)* | Uses the CPU and Unity's Particle System. Best for older hardware or laptops without a dedicated GPU. |
| **Toggle ON** | Offloads star rendering to the GPU for higher frame rates, smoother camera rotation, and sharper visuals. |

---

## Input Screen

The Input Screen collects the following data to compute your sky view:

### Coordinates
- **Latitude** - Degrees (0–90) and Minutes (0–59.999), with a **N/S** toggle
- **Longitude** - Degrees (0–180) and Minutes (0–59.999), with an **E/W** toggle

### Temporal Data
- **Date** - Format: `YYYY-MM-DD` (valid range: 1900–2100)
- **Time** - Format: `HH:MM` (24-hour)

> All inputs are validated, the app will not proceed until astronomically valid data is entered.

### Presets

To speed up setup, STARS includes a preset system:

- **Event Presets** - Notable astronomical and historical events (e.g., Apollo 11 Launch, Planetary Parade)
- **Location Presets** - Famous landmarks (e.g., NYC Statue of Liberty, Mount Everest)
- **User Custom Presets** - Save your own configurations for laterr:
  1. Fill in your latitude, longitude, date, and time.
  2. Click **Save Preset**.
  3. Find your preset under **My Presets**.
  4. To delete a preset, select it from the **My Presets** dropdown and click **Delete Preset**.

---

## Sky Scene

The Sky Scene is a 3D interactive star map rendered accurately for your input location, date, and time. Stars, constellations, and planets are positioned as they would appear to an observer at those exact parameters.

### Navigation

- **Click + Drag** (Left Mouse Button) - Rotate the camera
- The **green plane** represents the horizon - anything below it isn't visible in real life
- The **UI Compass** at the top of the screen helps with orientation
- **Click any star** to display that star's details (see Star Information below)

### Star Information

Clicking a star opens an info panel with the following output:

| Field | Description |
|-------|-------------|
| Name | The star's common or catalog name |
| RA | Right Ascension (celestial longitude) |
| Dec | Declination (celestial latitude) |
| Magnitude | Apparent brightness |
| Spectral Classification | Star type based on temperature and color |
| Distance | Distance from Earth |

### Label Colors

| Color | Type |
|-------|------|
| White | Stars |
| Yellow | Constellations |
| Blue | Planets |

### Settings Menu

Press `ESC` or click the **Settings** button (top-left) to open the settings menu.

**Visual Toggles:**

| Toggle | Description |
|--------|-------------|
| Session Info | Shows/hides active coordinates and timestamp |
| Compass | Toggles cardinal direction indicators (N, S, E, W) |
| Constellations | Draws lines connecting stars into constellation patterns |
| Objects Under Horizon | Removes the ground plane to show objects on the far side of Earth |
| Labels | Toggles text overlays for stars, planets, and constellations |

**Export Options:**

Both exports open a file explorer dialog so you can choose where to save the file.

| Option | Output | Format |
|--------|--------|--------|
| Export JPEG | Current sky view | `.jpg` |
| Export 8.5×11 | Star chart formatted for standard printer paper | `.jpg` |

**Navigation:**

| Option | Description |
|--------|-------------|
| Main Menu | Returns to the Custom Setup screen |
| Quit | Safely exits the application |

---

## Help

If you encounter any issues, please reach out to one of the project authors below.

---

## Authors

- Deanna Deylami
- Camerin Casesa
- Diana De Santiago
- Eli Berg
