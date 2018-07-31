# MAMEIron
MAMEIron is a .NET-based MAME Front-end built in WPF.
![screenshot](https://raw.githubusercontent.com/MrChrisWeinert/MAMEIron/master/Images/screenshot.png)

Prerequisites:
1) You must have MAME installed
2) You must have snapshots for all the games you wish to play

Installation:
1A) Unzip the [Release](../../raw/master/Releases/MAMEIron.zip) zip file into your MAME folder, or
1B) Build from source and copy the files from /bin/Release to your MAME folder
2)  Modify the MAMEIron.exe.config file to set your preferences
3)  Install the two fonts included in the /fonts folder

Notes:
- The very first time you run MAMEIron, it will use the MAME executable to generate an XML list of all the games it [MAME] supports.
MAMEIron will then filter down that list to exclude a games based on certain criteria. (See the source code for details)
This process will take a few minutes and ultimately generates a games.json file. This is a one-time process and subsequent launches of MAMEIron are fast.

- MAMEIron was designed to run at 1680x1050. It will look weird at other resolutions.

Credits:
Thank you to the person, or team of people, that created the Nevato[https://www.onyxarcade.com/nevato] Theme. I hacked up their cabinet picture for use in MAMEIron.
Thank you to Greg Schechter for creating the WPF Planerator[https://blogs.msdn.microsoft.com/greg_schechter/2007/10/26/enter-the-planerator-dead-simple-3d-in-wpf-with-a-stupid-name/] which allowed me to 3D-rotate the game screenshots to match the arcade cabinet.
Thank you to the fine people that created these sweet fonts, “Arcade” by Jakob Fischer[https://pizzadude.dk/site/about/] and “PacFont Good” by Fontalicious[http://www.abstractfonts.com/designer/89/Fontalicious]. Without them I’d probably still be using Comic Sans.
Thank you to noirenex for the sound effect[https://www.freesound.org/people/noirenex/sounds/98883/]