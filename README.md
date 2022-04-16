# Triad Buddy
Dalamud plugin for Triple Triad solver, NPC matches only.  
Requires custom game launcher that supports modding framework. Please see https://github.com/goatcorp/FFXIVQuickLauncher for details.  

Pretty pictures / feature list:
1. Highlight next move (not available in PvP!)  
![Game overlay](/assets/image1.png)  
2. Optimize deck for NPC before match  
![Deck optimizer](/assets/image2.png)  
3. Find missing cards more easily  
![Collection details](/assets/image3.png)  

Standalone tool can be found here: https://github.com/MgAl2O4/FFTriadBuddy

## PvP / Tournament support

Solver is based around known NPC decks. Tournaments break that rule and allow much wider selection of cards, making all predictions garbage.  
Yes, you can copy project and turn off PvP check - license allows to modify code as much as you like. However, solver will not magically start planning for unexpected cards and you may as well place stuff on board randomly. If you wish to have true PvP support, please be ready to redo entire solver as well. You have been warned :)


## Translation

You can help with translation here: https://crowdin.com/project/fftriadbuddy. Project files:  
* plugin.json = localization file for this plugin
* strings.resx = standalone tool


Contact: MgAl2O4@protonmail.com
