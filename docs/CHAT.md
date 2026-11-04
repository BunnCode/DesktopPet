# Speech bubbles and sticky notes

Pets do two separate things with words:

- **They talk:** short lines in a speech bubble above their head.
- **They leave sticky notes:** a note stuck on the window they're standing on.

Each has its own switch in the menu (**Let them talk**, **Let them leave notes**) and in `settings.json` under `Chat`
(`Enabled`, `NotesEnabled`), and its own timing. The words come from a small language model (Qwen3.5-2B) that runs on
your own PC; nothing is sent anywhere. One copy of the model serves both behaviors and every pet, one line at a time.

Sleeping pets do neither, and nothing happens while the screen is locked. A pet's personality (its `personality.txt`)
goes into everything it says and writes, so editing it changes the very next line.

## Talking

| What | When |
|---|---|
| Needs | A need is below 30: it says it's hungry, lonely or sleepy |
| Banter | Another pet is close by: one says something to the other, and the other answers |
| Jokes | Any time, rarely |
| Clicked | Every time you click it (not when you drag or throw it): it reacts to being petted, woken up or poked |

Bubbles follow the pet and fade after a few seconds. When pets stand side by side, their bubbles stack.

## Sticky notes

A pet standing on a window now and then writes a note for you (drink some water, take a break, a kind word, a silly
thought) and sticks it on that window, where it's standing. The note moves with the window, hides while it's minimized
and goes when it closes. Other windows in front cover it as they cover the window. Click a note to dismiss it. At most
3 are on screen at once, and notes don't come back after a restart.

## How often

Each pet has its own multipliers in its `traits.json` (1 = average, 2 = twice as often, 0 = never):

```jsonc
"Chat": { "Needy": 1.2, "Banter": 0.8, "Jokes": 1.5, "Notes": 0.6 }
```

New pets get random ones (between 0.5 and 2 by default, `NewPetChat` in `settings.json`). The shared timing is in
`settings.json` under `Chat`:

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Pets talk |
| `NeedyBelow` | 30 | A need below this makes the pet say so |
| `NeedyPerHour`, `BanterPerHour`, `JokesPerHour` | 10, 8, 3 | Lines per hour while that kind makes sense |
| `BanterDistanceDip` | 260 | How close two pets must be to chat |
| `MinGapSeconds`, `PetGapSeconds` | 20, 60 | Quiet time between any two lines, and between two from the same pet (clicks skip it) |
| `ClickLineWithinSeconds` | 8 | A reaction to a click that can't start this soon (the model was busy) is skipped |
| `AnswerDelaySeconds` | 2 | The answer in a conversation waits at least this long |
| `BubbleSeconds`, `BubbleSecondsPerChar` | 3, 0.06 | How long a bubble stays |
| `NotesEnabled` | `true` | Pets leave sticky notes |
| `NotesPerHour` | 2 | Notes per hour while a pet stands on a window |
| `NoteGapSeconds`, `PetNoteGapSeconds` | 120, 600 | Time between any two notes, and between two from the same pet |
| `MaxNotes` | 3 | Notes on screen at once |
| `RecentLines` | 5 | Lines (and, separately, notes) a pet is reminded of so it doesn't repeat itself |

## The model

The model file (`Qwen3.5-2B-Q6_K.gguf`, 1.5 GB) lives in a `models` folder next to `DesktopPet.App.exe`. It's loaded
once at startup and stays loaded: about 1.5–2 GB of graphics memory (or RAM on the processor). If the file is missing,
the pets carry on without words and a notification says where it was looked for.

| Setting | Default | Meaning |
|---|---|---|
| `ModelFile` | `Qwen3.5-2B-Q6_K.gguf` | A file name (looked for in `models\` next to the app, then in `%LOCALAPPDATA%\DesktopPet\models\`) or a full path. Any GGUF chat model in the Qwen (ChatML) format works. Applies on restart |
| `UseGpu` | `true` | Run on the graphics card through Vulkan (any recent NVIDIA, AMD or Intel card). Without a usable card it runs on the processor and says so. Applies on restart |
| `MaxTokens`, `Temperature` | 60, 0.8 | Longest reply, and how wild the wording is |
| `ReplyTimeoutSeconds` | 25 | A reply that takes longer is dropped |

For developers: `tools\models\get-model.ps1` downloads the model into `models\` at the repo root, where the build and
the publish task pick it up.
