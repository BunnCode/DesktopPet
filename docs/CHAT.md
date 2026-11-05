# Speech bubbles and sticky notes

Pets do two separate things with words:

- **They talk:** short lines in a speech bubble above their head.
- **They leave sticky notes:** a note stuck on the window they're standing on or in front of.

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
| Remarks | It's on, in front of or beside a window and reading the screen is on: it comments on something it reads there |

Bubbles follow the pet and fade after a few seconds. When pets stand side by side, their bubbles stack.

## Sticky notes

A pet standing on a window now and then writes a note for you (drink some water, take a break, a kind word, a silly
thought) and sticks it on that window, where it's standing. A pet standing in front of a window (on the taskbar, say)
sticks it on that window instead, just above its head. The note moves with the window, hides while it's minimized
and goes when it closes. Other windows in front cover it as they cover the window. Click a note to dismiss it. At most
3 are on screen at once, and notes don't come back after a restart.

## Reading the screen

Pets can read the text on the window they're on (standing on it or climbing its side), in front of (it's behind
them, for example while they walk along the taskbar), or right beside, and talk or write about it: a comment on a recipe, a question about an email's subject, a tip about what you're writing. Switch it
in the menu (**Let them read the screen**) or with `ReadingEnabled` in `settings.json`. It's on by default.

- **How:** the pet takes a picture of the part of the window around it (about 900 × 700 DIP) and reads it with the text
  recognition (OCR) built into Windows. Only text that's really on that window counts: anything covered by other
  windows, or by the pets, bubbles and notes themselves, is left out. The few lines nearest the pet (300 characters by
  default) go to the model.
- **When:** only at the moment a pet makes a remark, or writes a note that is about the window (about 4 in 10 notes
  when there's something to read). It doesn't watch the screen in between.
- **Private:** everything stays on your PC. What a pet reads goes into that one prompt and is never logged or saved.
  Pets never read:
  - apps in `ReadExcludedApps` (process names; by default the common password managers and Windows' sign-in prompts);
  - windows whose title contains a word in `ReadExcludedTitleWords` (by default InPrivate, Incognito, Private
    Browsing, Password, Sign in, Log in, Login).

  Add your own, such as `"Outlook"` or `"Bank"`. Some browsers don't put "Incognito" in a private window's title, so
  those windows can't be told apart; switch reading off while you use them if that matters to you.
- **Needs** a Windows OCR language. Most installs have one for their display language; without one, a notification
  says so and pets carry on without reading.

## How often

Each pet has its own multipliers in the `Traits` of its `save.json` (1 = average, 2 = twice as often, 0 = never). `Curiosity` is how often it remarks on, or writes notes about, what it
reads:

```jsonc
"Chat": { "Needy": 1.2, "Banter": 0.8, "Jokes": 1.5, "Notes": 0.6, "Curiosity": 1.4 }
```

New pets get random ones (between 0.5 and 2 by default, `NewPetChat` in `settings.json`). The shared timing is in
`settings.json` under `Chat`:

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Pets talk |
| `ChatFrequency` | 1 | Multiplies how often every pet talks (needs, banter, jokes, remarks; not click reactions): 2 is twice as chatty, 0 is quiet. The gaps below still cap it |
| `NeedyBelow` | 30 | A need below this makes the pet say so |
| `NeedyPerHour`, `BanterPerHour`, `JokesPerHour` | 10, 8, 3 | Lines per hour while that kind makes sense |
| `BanterDistanceDip` | 260 | How close two pets must be to chat |
| `MinGapSeconds`, `PetGapSeconds` | 20, 60 | Quiet time between any two lines, and between two from the same pet (clicks skip it) |
| `ClickLineWithinSeconds` | 8 | A reaction to a click that can't start this soon (the model was busy) is skipped |
| `AnswerDelaySeconds` | 2 | The answer in a conversation waits at least this long |
| `BubbleSeconds`, `BubbleSecondsPerChar` | 3, 0.06 | How long a bubble stays |
| `NotesEnabled` | `true` | Pets leave sticky notes |
| `NotesPerHour` | 2 | Notes per hour while a pet stands on or in front of a window (times its `Notes` trait) |
| `NoteGapSeconds`, `PetNoteGapSeconds` | 120, 600 | Time between any two notes, and between two from the same pet |
| `MaxNotes` | 3 | Notes on screen at once |
| `RecentLines` | 5 | Lines (and, separately, notes) a pet is reminded of so it doesn't repeat itself |
| `ReadingEnabled` | `true` | Pets read the window they're on, in front of or beside |
| `RemarksPerHour` | 4 | Remarks per hour about what a pet reads, while it's on, in front of or beside a window |
| `NoteReadChance` | 0.4 | Chance a note is about what the pet reads on that window (otherwise a usual topic) |
| `ReadReachDip` | 40 | How close to a window's side counts as beside it |
| `ReadChars` | 300 | The most text a pet is given from what it reads |
| `ReadExcludedApps`, `ReadExcludedTitleWords` | see above | Never read these apps, or windows with these words in their title |

## The model

The model file (`Qwen3.5-2B-Q6_K.gguf`, 1.5 GB) lives in a `models` folder next to `DesktopPet.App.exe`. It's loaded
once at startup and stays loaded: about 1.5–2 GB of graphics memory (or RAM on the processor). If the file is missing,
the pets carry on without words and a notification says where it was looked for.

| Setting | Default | Meaning |
|---|---|---|
| `ModelFile` | `Qwen3.5-2B-Q6_K.gguf` | A file name (looked for in `models\` next to the app, then in `data\models\`) or a full path. Any GGUF chat model in the Qwen (ChatML) format works. Applies on restart |
| `UseGpu` | `true` | Run on the graphics card through Vulkan (any recent NVIDIA, AMD or Intel card). Without a usable card it runs on the processor and says so. Applies on restart |
| `ContextTokens` | 1024 | Room for the prompt and the reply, in tokens. Applies on restart |
| `MaxTokens` | 60 | Longest reply, in tokens |
| `Temperature` | 0.8 | How wild the wording is: 0 is always the likeliest words, 2 is wild |
| `TopP`, `TopK`, `MinP` | 0.9, 40, 0 | Which words are considered at all: the likeliest ones making up `TopP` of the chance, at most `TopK` of them (0: no limit), none below `MinP` times the likeliest (0: off). Lower `TopP`/`TopK` or higher `MinP` is tamer |
| `RepeatPenalty` | 1.1 | Above 1, words already used are less likely again (1: off) |
| `MaxSentences`, `MaxChars` | 2, 160 | Replies (speech and notes) are cut to this |
| `ReplyTimeoutSeconds` | 25 | A reply that takes longer is dropped |

For developers: `tools\models\get-model.ps1` downloads the model into `models\` at the repo root, where the build and
the publish task pick it up.
