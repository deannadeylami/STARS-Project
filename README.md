# STARS-Project
Amateur and professional astronomers use star maps as a guide in finding the locations of stars, planets, and other celestial objects for observation.  This class project will be to produce a software system which will create on screen displays and printable star maps for observers at any location on the earth for a given date and time.

## Description

An in-depth paragraph about your project and overview of use.

## Installing

### For Windows Users
1. Download STARSProject-StandaloneWindows64-vX.X.X or STARSProject-StandaloneLinux64-vX.X.X.zip,
   depending on your OS.
3. Extract the contents.
4. Run STARSProject.exe.

**Note: **It is not required to have Unity installed, this software is self-contained.

### Verifying Software Integrity
To ensure your download is legitimate, compare the checksum of your downloaded zip file with the provided **.sha256** string.
* For Windows, you can use Get-FileHash command in Powershell to get the hash, as shown below.
<img width="1079" height="120" alt="image" src="https://github.com/user-attachments/assets/febcb115-019e-47eb-9b85-71de6b36e22f" />

* Compare the hash with the contents of the .sha256 file, as shown below.
<img width="886" height="39" alt="image" src="https://github.com/user-attachments/assets/4c52f719-8e28-4437-b479-2fd8344a8217" />
* If they are the same, the software has not been altered.

## Executing program
### Start Menu
* After opening STARSProject.exe, you will be met with the start menu, as shown below.
<img width="576" height="477" alt="image" src="https://github.com/user-attachments/assets/4c6f8313-c4fa-4d2d-869b-9e4764733eb8" />

* There are two options to continue to the next scene, quick start and custom setup.
* Quick start jumps the user straight to the Sky Map, using the default Planetary Parade event (most of the planets lining up in the sky).
* Custom setup takes the user to the input screen.

## GPU Acceleration
* STARS utilizes a dual-rendering pipeline so users can choose between higher stability vs. high performance visuals.
* Toggle OFF (default) - Uses the CPU and Unity's Particle System to draw stars. This is highly compatible with older hardware and laptops without dedicated graphics cards.
* Toggle ON - Offloads the star rendering to the GPU. This provides a much higher frame rate, smoother camera rotation, and sharper star visuals.

## Input Screen
<img width="640" height="650" alt="image" src="https://github.com/user-attachments/assets/5de3ff84-779b-4677-b3c3-c37c8a43196e" />
* The input scene collects four categories of data to compute the sky scene.
   - Coordinates
         - Latitude - Input in Degrees (0-90) and Minutes (0-59.999), with a toggle for North (N) or South (S).
         - Longitude - Input in Degrees (0-180) and Minutes (0-59.999), with a toggle for East (E) or West (W).
   - Temporal Data
         - Date - Entered in YYYY-MM-DD formate to determine planet positions and star visibility
         - Time - Entered in HH:MM (24-hour format).
   - All input values are validated, the system will not allow you to continue until correct astronomical data is included.

* Presets
  - To streamline the experience for users, we have a preset system.
  - Preset events contain special astronomical and historical events such as the Apollo 11 Launch or the Planetary Parade.
  - Preset Locations contain significant landmarks and wonders of the world such as NYC statue or Mount everst.
  - User custom presets are able to be created,
      - Fill out longitude, latitude, date & time as usual.
      - Click "Save Preset"
      - Check under my presets for your user presets.
      - If you want to delete your preset, click it under the "my presets" dropdown and click "Delete Preset"

## SkyScene

* Step-by-step bullets
```
code blocks for commandsLatitude: Input in Degrees (0–90) and Minutes (0–59.999), with a toggle for North (N) or South (S).
```

## Help

Any advise for common problems or issues.
```
command to run if program contains helper info
```

## Authors

Deanna Deylami
Camerin Casesa
Diana De Santiago
Eli Berg

## Version History

* 0.2
    * Various bug fixes and optimizations
    * See [commit change]() or See [release history]()
* 0.1
    * Initial Release
