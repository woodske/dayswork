# Changelog

## Unreleased

### Added

- You can now name your farmhand from the contract's Preferences screen. The name shows up in
  notifications and on the Active Contracts screen.
- A machine group's input chest can now live in any location — the farmhand makes a trip to fetch
  the inputs before working the machines.
- Auto-grabbers can be picked as a machine input chest, so coop and barn products feed straight
  into your machines.
- The Active Contracts screen now shows your farmhand's name, the machines and fish ponds it
  tends, and its energy tier, with a colour-coded status.

### Changed

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

- Fruit trees the player isn't standing next to — greenhouse trees especially — stopped dropping
  fruit after the first shake of the save.
- Gates could be left hanging open when the farmhand's route changed partway through a walk.

## 1.2.1

- Initial tracked release.
