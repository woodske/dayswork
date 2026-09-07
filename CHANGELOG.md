# Changelog

## Unreleased

### Added

- You can now build more than one Farmhand Office, and hire a farmhand from each. Every office has
  its own contract, its own worker, and its own porch chests; each worker starts and finishes at its
  own office door, and each office lights up in the evening when its own farmhand is done.
- When two contracts cover the same crops, animals, machines or fish ponds, the work is only done
  once — whichever farmhand gets there first takes it.
- The crop zone drawing screen now shades the crop areas already claimed by another office's
  farmhand, so you can see where someone is already working. Those tiles stay selectable.
- You can now name your farmhand from the contract's Preferences screen. The name shows up in
  notifications and on the contract screen.
- A machine group's input chest can now live in any location — the farmhand makes a trip to fetch
  the inputs before working the machines.
- Auto-grabbers can be picked as a machine input chest, so coop and barn products feed straight
  into your machines.
- The contract screen now shows your farmhand's name, the machines and fish ponds it
  tends, and its energy tier, with a colour-coded status.
- Chopping trees and breaking rocks now earns you foraging and mining experience, the same as
  harvesting always has. The Preferences screen has a toggle to turn a contract's experience off.
- Preferences also gained a "Work while you're away" toggle, so you can say whether your farmhand
  should keep working when you aren't around.

### Changed

- Everything about a contract — the fee, the seeds and fertiliser your farmhand buys, the tool
  levels it works with, and where its output is shipped — now follows the player who owns the
  office, rather than whoever happens to be playing.

- Farmhands no longer plan a shopping trip for gold another farmhand is already on the way to
  spend, so two workers can't both set out and leave one arriving at an empty till.
- The farmhand building is now called the Farmhand Office everywhere, instead of the Farmhand
  Cabin. Its two porch chests are named "Farmhand Office - Input" and "Farmhand Office - Output",
  and the menus and notices that mention them were reworded to match.
- Clicking an office opens a single "Contract" page for that office's own contract, instead of a
  scrollable list of every contract on the farm.
- New contracts now default to managing machines when they finish their assigned work early,
  instead of going straight home. Existing contracts keep whatever you set.
- The farmhand works crop fields row by row instead of always heading for the nearest tile, which
  cuts a lot of wasted walking. A row split by a tree or building is worked one side at a time.
- Deposit trips are ordered by how far the farmhand actually has to walk, so it no longer
  repeatedly enters and leaves the same building to drop things off.
- General pathfinding is faster, especially on large or heavily built farms.
- If you have the destination chest open when the farmhand arrives, it now waits a few seconds for
  you to close it instead of immediately diverting the items to the shipping bin.

### Fixed

- Demolishing a Farmhand Office now ends that farmhand's shift safely — anything it was carrying is
  delivered or shipped rather than lost. The office's contract goes with the building, and its fee
  is not refunded.
- Saves made with an older build could keep extra hidden contracts that were charged for every
  morning without a farmhand ever showing up. Loading such a save now keeps only the contract you
  could actually see and attaches it to your office; the rest are dropped.
- Fruit trees the player isn't standing next to — greenhouse trees especially — stopped dropping
  fruit after the first shake of the save.
- Gates could be left hanging open when the farmhand's route changed partway through a walk.

## 1.2.1

- Initial tracked release.
