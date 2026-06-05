# U-MC-02 Functional Design Clarification Questions

I detected one contradiction that needs clarification before generating the Functional Design artifacts.

## Contradiction 1: Output Chest Selectability

Question 4 was answered `B`, which means the output chest should be selectable explicitly. However, the approved U-MC-02 scope and FR-MC-35 say `ChestResolver` excludes both built-in office chests from player-selectable destination lists.

The additional clarification says the farmhand cabin input chest is where crop management draws supplies from, and the output chest is where task output can be deposited. That role can be implemented either as an implicit output fallback or as an explicit selectable destination.

## Question 1
How should the farmhand office output chest be represented for task-output deposits?

A) Keep both built-in office chests excluded from selectable destination lists; output chest remains the implicit default/fallback task-output deposit destination.
B) Override FR-MC-35 for output chest only; input chest remains excluded, but output chest is selectable explicitly as a per-zone/task-output destination.
C) Keep both built-in office chests excluded from normal discovered chest lists, but expose a separate fixed "Farmhand Office Output" destination option outside `ChestResolver` discovery.
D) Other (please describe after `[Answer]:` tag below)

[Answer]: B, output chest should remain default and selectable
