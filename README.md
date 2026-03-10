# MelInEx
A mod to load mods interchangably between MelonLoader and BepInEx

Currently loads mods, just needs polish, a better installation process, and patchers.

To use simply put it in your Mods/plugins folder (it works on either mod loader), download the opposite respective mod loader and do the following:
### On MelonLoader:
Copy JUST the BepInEx folder and put it in your game's directory, from there it'll work like normal BepInEx (patchers unsupported)
### On BepInEx:
Copy the contents of the melonloader folder, make a new folder in the BepInEx folder called MelonLoader and put the contents in there, that MelonLoader folder is where you will put your melonLoader files (Plugins have very poor support)
For example, MelonLoader.dll should be at BepInEx/MelonLoader/MelonLoader/net35/MelonLoader.dll (you don't have to know what that is to do this, just make sure that file is at that path.)
