# Study Monitor

A tiny Windows tray app that times your study sessions out loud and calls you out when you slack off.

- **Start/stop** with the big button, the tray icon, or **Ctrl+Alt+S** from anywhere (even in a game).
- **While you study** it says the time every 5 minutes, tells you to stand up every 30, reminds you to drink water at random, and speaks up when you're halfway, close and done.
- **When you stop**, it tells you how you're doing. Quit after 12 minutes while you're behind and you'll hear "12 minutes? At this rate you're going to fail."
- **When you're not studying**, it counts the time you spend on the laptop and nags you at random intervals that get harsher: an hour, two, three or more.
- It only counts time you're **actually using the laptop** (keyboard or mouse in the last 10 minutes), so it stays quiet when you're away or asleep, and doesn't say "it's been 9 hours" after a night's sleep.
- Female voice by default (Microsoft Zira on English Windows). Everything stays on your PC in plain files.

## Get it running

1. Download this repo (**Code → Download ZIP**, then unzip) or `git clone` it.
2. Double-click **`build.bat`**. It uses the C# compiler that's already part of Windows, so there's nothing to install. If Windows warns about the file, click **More info → Run anyway**.
3. Double-click **`bin\StudyMonitor.exe`**.

From then on it starts with Windows and lives in the tray (you can turn that off in Settings). The X button only hides the window. To really exit, right-click the tray icon → **Quit**.

The tray icon is **green** while you study, **gray** when you're not, and **gold** once today's goal is done.

If you'd rather not build it yourself, every push builds the exe on GitHub: **Actions → latest run → Artifacts → StudyMonitor**. A downloaded exe may get a SmartScreen warning; building it yourself avoids that.

## How it decides what to say

It tracks three numbers:

- **Pace:** what you've done today vs. where you should be by now. With a 3 hour goal and a 9 AM to midnight study day, at 6 PM you "should" have done about 1h 48m.
- **Time left vs. time needed:** what's left of today's goal vs. the time left before bedtime. If it no longer fits, you'll hear about it.
- **Projection:** your 7-day average vs. your goal: "at this rate you'll be 7 hours short this week" / "31 hours behind in a month".

| Situation | Example |
|---|---|
| Stop after under 20 min while behind | "12 minutes? At this rate you're going to fail." (sometimes plus a fact, a motivation or a bit of doom) |
| Stop within 20 min of your goal | "You're 15 minutes from your goal. Don't stop now." |
| Stop while on pace | "Nice, 50 minutes. You're on pace." |
| Goal can't be reached before bedtime | "Even if you studied nonstop until midnight, you won't hit your goal today." |
| Halfway / 15 min left / goal hit | "Halfway there." / "Almost there. 12 minutes left. Finish it." / "Goal done for today. 6 day streak." |
| 30 min into a session | "Stand up and stretch for a minute." |
| On the laptop 1h / 2h / 3h+ without studying | "It's been 3 hours 15 minutes. Get back to work or enjoy being homeless." |
| Past bedtime and still on the laptop | "It's past your bedtime. Sleep now, study tomorrow." |
| Random, every 40–80 min while you're there | "Drink some water." |
| First time you're on the laptop each day | "New day. Your exam is in 12 days. Today's goal is 3 hours." |

Nags only happen while you're at the laptop (input in the last minute), never right on top of another message, and never once today's goal is done.

## Your files

Everything is in `bin\data\` (or `%APPDATA%\StudyMonitor` if that folder isn't writable):

| File | What's in it |
|---|---|
| `sessions.csv` | Every session: day, start, end, minutes, goal. Opens in Excel. The **History** button opens a copy, so Excel can't lock the real file. |
| `messages.txt` | Everything it says. Edit it freely; changes apply right away. |
| `settings.ini` | Your settings. Easier to change with the Settings button. |
| `state.ini` | Its own bookkeeping: the running session, off-task time, a heartbeat every minute. |
| `errors.log` | Only appears if something went wrong. |

**Safety nets:** close the lid mid-session and the session ends when the laptop fell asleep, not 8 hours later. If the app crashes or the battery dies, the session is logged up to the last heartbeat (at most a minute lost); restart it within 3 minutes and the session just carries on. If you forget to stop, 2 hours without keyboard or mouse ends the session at your last input (with a spoken warning 10 minutes before). If `sessions.csv` is locked, sessions are kept safe in `state.ini` and saved once it's free.

## Editing what it says

`messages.txt` has one `[section]` per situation and one line per option. It picks a random line and won't repeat one until it has used all the others.

- Lines starting with `!` are the harsh ones. **Gentle** tone never uses them, **Normal** mixes them in, **Brutal** uses only them.
- Placeholders like `{left}`, `{today}`, `{streak}`, `{avg}`, `{week_short}`, `{exam_in}` are filled in. A line is skipped when one of its placeholders has no value yet (e.g. `{avg}` on your first day). The full list is at the top of the file.
- Delete a whole section and the built-in lines come back.

## Settings

Daily goal · your study day (start and bedtime, used for pace) · when the new day starts (4 AM by default, so late nights count for the day before) · exam countdown · how often it says the time · stand-up reminder · water reminders · when the first nag comes and how often after · tone (gentle / normal / brutal) · when you count as away · auto-stop for forgotten sessions · voice, volume and speed · hotkey · start with Windows.

## Troubleshooting

- **No female voice:** Settings → Voice lists the voices this app can use. Zira is built into English Windows. Voices you add under Windows Settings → Time & language → Speech are for newer apps and usually don't show up here.
- **Hotkey doesn't work:** another app already uses it. The window says so; pick a different one in Settings.
- **Silent:** it talks through your normal audio output, so muted laptop = silent. The last thing it said is always shown in the window.
- **"Couldn't find the C# compiler":** you need .NET Framework 4.8, which is built into Windows 10 (1903 or later) and Windows 11.

## For tinkerers

- `src\Engine.cs` makes all the decisions. `History.cs` handles sessions, streaks and averages. `Messages.cs` picks lines. `MainForm.cs` and `SettingsForm.cs` are the windows. `Speech.cs` talks to the built-in Windows voice.
- The code sticks to C# 5 so the compiler built into Windows can build it. Run `build.bat` again after a change (quit the app first).
- `tests\run-tests.bat` runs the logic tests: hours of simulated use with a fake clock, no audio.
