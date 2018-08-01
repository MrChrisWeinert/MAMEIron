# MAMEIron
MAMEIron is a Windows-based MAME Front-end built in WPF.
- It displays the year and playcount per-game.
- You can add games to your favorites, which appear at the top of the game list.
- If a [Nusbio](https://squareup.com/market/madeintheusb-dot-net/item/nusbiomcu) control board is present, MAMEIron can control LED lights during various functions.
- If a webcam is present, MAMEIron will sleep after a period of inactivity, and wake up when motion is detected. Also, by way of the webcam's mic, there is some voice-control functionality as well!


![screenshot](https://github.com/MrChrisWeinert/MAMEIron/raw/master/MAMEIronWPF/Images/screenshot.png)

## Prerequisites:
1) You must have MAME installed
2) You must have snapshots for all the games you wish to play

## Installation:
1) Unzip the [Release](https://github.com/MrChrisWeinert/MAMEIron/raw/master/Releases/MAMEIron.zip) zip file into your MAME folder, or build from source and copy the files from /bin/Release to your MAME folder
2)  Modify the MAMEIron.exe.config file to set your preferences
3)  Install the two fonts included in the /fonts folder


## Notes:
1) The very first time you run MAMEIron, it will use the MAME executable to generate an XML list of all the supported games.
MAMEIron will then filter down that list to exclude games based on certain criteria. See the [wiki](https://github.com/MrChrisWeinert/MAMEIron/wiki/Game-Filters) or source code for more details. 
This is a one-time process and subsequent launches of MAMEIron are fast.
2) MAMEIron was designed to run at 1680x1050. It will look weird at other resolutions.

Credits:
- Thank you to the person, or team of people, that created the [Nevato](https://www.onyxarcade.com/nevato) Theme. I hacked up their cabinet picture for use in MAMEIron.
- Thank you to Greg Schechter for creating the WPF [Planerator](https://blogs.msdn.microsoft.com/greg_schechter/2007/10/26/enter-the-planerator-dead-simple-3d-in-wpf-with-a-stupid-name/) which allowed me to 3D-rotate the game screenshots to match the arcade cabinet.
- Thank you to the fine people that created these sweet fonts, [Arcade](https://pizzadude.dk/site/about/) by Jakob Fischer and [PacFont Good](http://www.abstractfonts.com/designer/89/Fontalicious) by Fontalicious. Without them I’d probably still be using Comic Sans.
- Thank you to noirenex for the [sound effect](https://www.freesound.org/people/noirenex/sounds/98883/)
