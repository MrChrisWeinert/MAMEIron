# MAMEIron
MAMEIron is a .NET-based MAME Front-end built in WPF.
It has support for the NusbioMCU (https://squareup.com/market/madeintheusb-dot-net/item/nusbiomcu) board which can control White LED strips as well as RGB LED strips.
In this particular version, the code is commented out, but there is "motion-detection" by way of a USB camera. When no motion is detected for a period of time, the MAMEIron will
fade to black. When motion is detected, MAMEIron will fade back in.
Additionally, there is rough support for voice-recognition which calls Bing APIs to convert voice to text.

Notes:
- When building the main project, MAMEIronWPF, the output will include a fonts folder with two fonts that should get instlled.
- This was designed for a 1680x1050 screen, so it will look odd at different resolutions.
