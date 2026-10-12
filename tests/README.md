# Cooked-item regression checks

Run `dotnet run --project tests/RegressionTests.csproj` with the .NET 10 SDK.
Run `pwsh -NoProfile -File tools/test-wiki-defaults.ps1` to check bilingual data and its Wiki revision manifest.

The harness compiles the production modifier and description formatters against a minimal
managed game/Unity model. It checks raw/cooked/burnt/incinerated/unusable description selection,
equivalent first/second cooking descriptions, stale raw-text suppression, disabled effect
components, inactive child branches, inactive inventory roots, live poison duration, petrify
effects, and English/Chinese descriptions. It requires no proprietary game files.

These checks do not execute Unity lifecycle callbacks or the game's cooking implementation.
For in-game verification, compare a raw Green Crispberry with the same item cooked once and
twice. The cooked description and modifiers should match when the effects match. Check the
burnt and incinerated states, a pre-cooked food, and held/dropped/reequipped instances. The
existing 0.25-second content refresh should update the same item without switching targets.
