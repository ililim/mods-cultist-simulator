# Cultist Simulator Mods

This is a collection of mods that improve the UX and add extra functionality to Cultist Simulator, mostly geared towards improving usability and keyboard control of the game.

## 🚀 ShiftPopulate

**Reduce dragging significantly by empowering your shift+click!**

There is a _lot_ of dragging in cultist simulator which can really strain your wrists (and get tedious). This mod will enable you to instantly manipulate cards with shift+clicks.

When a situation/verb window is open, simply shift+click on a card and it will be automatically inserted into the first compatible slot.

When no situation window is open, shift+clicking on a card will open the closest situation window that can accept the card and insert it there.

Finally, shift+clicking on an empty slot will populate it with the closest card that fits (in case of ties, it will pick the closest top-left-most card).

Now you can move cards into slots without dragging, but what about removing them? Just press E for [E]ject. It will remove the right-most card from the slot and put it back into the last location the game remembered it being.

Together with the built-in [S]tart and [C]ollect shortcuts, shift+click and [E]ject will greatly reduce the amount of dragging required to play this game and make experimenting with lots of cards much smoother (and more fun!).

## 🔨 SituationAutomation

**Automate repetitive tasks!**

After ctrl+clicking on a situation (square token) it will be decorated with an anvil icon in the bottom right. It is now automated and will find cards, start tasks and collect the results automatically.

The automated situation token will grab cards within a small radius around it (starting from top to bottom, then left to right). It will insert these cards into as many empty slots as possible, then start the task, collect the results, and finally repeat it all over again.

Additionally: The automated situation will pause while the player has its window open or when the game is paused. Ctrl+click on the situation again to unautomate it. Note that this mod does not touch your save file, so ctrl+click on those tokens again after loading.

You can use this mod however you would like, but it's recommended to automate the simpler grindy tasks of the game (working, buying books) so you can focus your full attention on the exciting development your cult and exploration of your dreams.

Automating more complex tasks is definitely possible, but be mindful of the order in which the slots are populated and how the game handles returning cards back to the tabletop (which is reset each time you load the game).

An example showing the automation in action:

![An example showing the automation in action](resources/demo-automation.gif?raw=true)

An example showing in which order the cards will be used:

![An example showing in which order the cards will be used](resources/demo-automation-order.png?raw=true)

## 👻 EscapeDismisses

**Dismiss panels and windows by pressing escape.**

Arguably the most useful button on your keyboard is escape, so why have it open the settings menu?

No more having to move your mouse all the way to the upper right corner of each window to hide it! This mod rebinds escape to close all information/notification panels and situation windows.

If any information or notification panels are open, those will be closed first. If those are all closed then the open situation window will be closed instead. (To open the settings menu use shift+esc).

## 💾 QuickSave

**Save/load with F5/F9.**

Not recommended for your first few games! But after several playthroughs you might decide you want to start playing with manual saves.

This mod puts save and load at your finger tips (F5 and F9 respectively). Each time it you save the autosave timer will also be reset, so the game will not immediately overwrite your save file right as your favorite cultists dies.

## Installation

- Install [Partiality Launcher](https://github.com/PartialityModding/PartialityLauncher/blob/master/Tutorial.md)
- Unzip the mods [(Download link)](https://github.com/ililim/CultistSimulatorMods/releases) into [GAME_FOLDER]\Mods\
- Enable the mods your would like to use in the Partiality Launcher menu. You **must** enable **0Harmony** and **IlilimModUtils** for the other mods to work.
- Run the launcher and enjoy!

### Feedback and issues

If you have any suggestions, contributions, or issues feel free to create a new issue or pull request on Github.
