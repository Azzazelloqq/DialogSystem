# Dialog System (Hybrid DSL)

Universal dialog system for Unity built around a text DSL (source of truth) with runtime execution and editor tooling.
Designed for deep branching narratives with conditions, jumps, calls, and reusable dialog graphs.

## Features

- Text DSL (`.dlg`) friendly to git/merge
- Branching with `if/else`, `when`, `jump`, `call/return`
- Explicit outcomes via `<<exit>>` for flow graphs
- C#-style expression language for conditions and commands
- Custom code conditions (registry)
- Runtime API for UI integration
- Editor importer with diagnostics and graph visualization
- WYSIWYG dialog editor for non-coders
- Dialog flow graph editor (outcome-based)

## Quick Start

1. Create a `.dlg` file in your project (example in `Assets/DialogSystem/Example/ExampleDialog.dlg`).
2. Unity imports it into a `DialogAsset`.
3. Use `DialogRunner` with your UI to advance dialog and present choices.

## DSL Syntax

### Dialog and Labels

```
@dialog main
@label start
```

`@dialog` can appear multiple times in one file. `@label` marks entry points inside a dialog.

### Lines

```
Hero: Hello.
Narrator: The room is silent.
```

If there is no speaker, the line is treated as narration.

### Choices

```
* "Ask about work" when Reputation > 0 -> exit:AskJob
* "Leave" -> exit:Leave
```

Choices are grouped by consecutive `*` lines. `when` is optional. If `-> exit:Outcome` is missing,
the dialog continues to the next instruction after the choice block.

### Commands

```
<<set Reputation = 2>>
<<do GiveItem("sword")>>
<<jump end>>
<<call side.start>>
<<return>>
<<exit Angry>>
```

### Conditional Blocks

```
<<if HasKey && Trust > 3>>
Hero: I can help.
<<else>>
Hero: Sorry.
<<endif>>
```

### Tags and Stable IDs

Add stable identifiers or tags to lines/choices for better diffs and merging:

```
Hero: Hello. #id:greeting #tag:intro
* "Ask" -> ask_job #id:ask_job
```

Tags must be at the end of the line. Supported forms: `#id:value`, `#tag:value`, or `#value`.

### Cross-Dialog Targets

Use `dialogId.label` to jump or call other dialogs:

```
<<call side.start>>
* "Go to side" -> side.start
```

## Expressions

Expressions support numbers, strings, booleans, comparisons, and function calls:

```
Reputation > 0 && HasKey
QuestState("main") == "done"
```

Register functions in code with `DialogContext.RegisterFunction`.

## Code Conditions (Registry)

Create conditions in code and register them:

```csharp
using DialogSystem.Runtime.Conditions;

public sealed class HasTraitCondition : IDialogCondition
{
    public string Id => "HasTrait";
    public string DisplayName => "Has Trait";

    public bool Evaluate(DialogConditionContext context, DialogConditionArgs args)
    {
        // args.Args содержит аргументы
        return true;
    }
}
```

Register once at startup:

```csharp
DialogConditionRegistry.Register(new HasTraitCondition());
```

Then use in DSL:

```
Hero: Привет. when HasTrait("Brave")
```

## Runtime API

- `DialogRunner` drives the dialog and returns `DialogEvent` (`Line`, `Choices`, `End`, `Error`).
- `DialogContext` stores variables and functions used by expressions.
- `DialogState` can be captured/restored for save/load.

## Editor Tools

- `.dlg` importer creates `DialogAsset` and reports parse errors
- `DialogAsset` inspector shows errors and opens the graph view
- Graph view provides a read-only visualization of flow
- Visual editor based on `DialogGraph` assets for non-coders
- Dialog DSL Editor for WYSIWYG editing of `.dlg`
- Dialog Flow graph editor for outcome-based flow

## Visual Editor (for non-coders)

1. Create `DialogGraph` asset via **Create → Dialog System → Dialog Graph**.
2. Open it with **Open Visual Editor** in the inspector.
3. Build the dialog visually with nodes and connections.
4. Click **Export .dlg** to generate the text DSL file (auto path by default).
5. If the `.dlg` file was merged manually, use **Import .dlg** to rebuild the graph.

The exported `.dlg` can be versioned and merged in git, while authors work in the editor.
Each `DialogGraph` asset represents a single `@dialog` section.

## Dialog DSL Editor (WYSIWYG)

Create a **Dialog Draft** asset (**Create → Dialog System → Dialog Draft**) and open it in the editor.
Open **Window → Dialog System → Dialog DSL Editor**, edit lines/choices/outcomes, then **Export** to `.dlg`.
Writers never need to edit `.dlg` manually.

## Dialog Flow Graph

Create a `DialogFlow` asset (**Create → Dialog System → Dialog Flow**) and open it via
**Window → Dialog System → Dialog Flow Editor**. Each dialog node exposes outcomes; connect outcomes
to other dialogs or action nodes.

## Example

```
@dialog main
@label start
Hero: Hello.
<<set Reputation = 1>>
* "Ask about work" when Reputation > 0 -> exit:AskJob
* "Leave" -> exit:Leave

@label ask_job
NPC: I'm a blacksmith.
<<return>>
```
