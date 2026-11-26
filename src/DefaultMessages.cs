namespace StudyMonitor
{
    // The built-in lines. On first run these are written to data\messages.txt so you can edit them.
    static class DefaultMessages
    {
        public const string Text = @"# Study Monitor - everything it says out loud.
#
# How this file works:
#   - Each [section] is one situation. It picks a random line from that section and
#     won't repeat a line until it has used all the others.
#   - Add, change or delete lines freely. Lines starting with # are ignored.
#   - Lines starting with ! are the harsh ones. Gentle tone never uses them, Normal mixes
#     them in, Brutal uses only them (when a section has any).
#   - Delete a whole section and the built-in lines for it come back.
#   - Changes are picked up automatically. No restart needed.
#
# Placeholders. A line is skipped when one of its placeholders has no value right now,
# for example {exam_in} when no exam date is set, or {avg} on your first day.
#   {session}      length of the session you just stopped
#   {elapsed}      how long the current session has been running
#   {today}        total studied today
#   {goal}         your daily goal
#   {left}         what's left of today's goal
#   {pace}         today vs. where you should be by now, like 40 percent
#   {streak}       days in a row you hit your goal
#   {avg}          your average per day over the last 7 days
#   {week_short}   how far behind you fall in a week at your current average
#   {month_short}  the same, over 30 days
#   {offtask}      time on the laptop since you last studied (10 minutes or more)
#   {exam}         your exam's name
#   {exam_in}      in 12 days / tomorrow / today
#   {bedtime}      your bedtime
#   {time}         the time now
#   {end}          when the session ended

[welcome]
Study Monitor is running. Today's goal is {goal}. I'm watching.
Study Monitor is running. I'll keep track of your time from now on.

[new_day]
New day. Today's goal is {goal}.
New day. {goal} to do today.
New day. Your {exam} is {exam_in}. Today's goal is {goal}.
New day. You're on a {streak} day streak. Keep it alive.
!New day. {goal} today. No excuses.

[start]
Timer started. {left} to go today.
Let's go. {left} left for today.
Started. Phone away, eyes on the work.
Locked in. Make this one count.
Started. Your {exam} is {exam_in}.
Good. Every minute counts.
!Finally. {offtask} of doing nothing, and now you start.
!About time. {left} to go, don't waste it.

[start_after_goal]
Bonus round. Your goal is already done, this is extra.
Goal's already done and you're still going. Respect.
Extra time. Future you says thanks.

[tick]
{elapsed}.

[tick_goal]
{elapsed}. {left} left for today.
{elapsed} in. {left} to go today.

[goal_half]
Halfway to today's goal.
That's half your goal done. Keep going.
Halfway there. {left} to go.

[goal_close]
{left} to your goal. Don't stop now.
Almost there. {left} left. Finish it.
You're close. {left} to go. Don't give up now.
!{left} left. Don't you dare stop now.

[goal_hit]
Goal done for today. {streak} day streak.
That's your goal. Nice work. {streak} days in a row.
Daily goal hit. Anything more is a bonus.
You did it. {today} today.

[break]
{elapsed} straight. Stand up for a minute.
Stand up and stretch for a minute.
Quick break. Stand up, roll your shoulders, then back to it.
You've been sitting for {elapsed}. Get up for a minute, your back will thank you.

[water]
Drink some water.
Water break. Take a few sips.
Have some water. Even mild dehydration makes it harder to focus.
Hydrate. Grab some water.

[stop_short]
{session}? At this rate you're going to fail.
That's it? {session}. You've done {today} of {goal} today.
{session} and you're done? At this rate you'll be {week_short} short every week.
You stopped after {session}. You're at {pace} of where you should be by now.
!{session}. That's not studying, that's a warm up.
!You quit after {session}. At this rate you're going to fail, and you know it.
!{session}? At this rate you'll be {month_short} behind by next month.

[stop_short_ok]
Short one, but you're still on pace today.
Quick session. You're on track, just don't make a habit of it.

[stop_behind]
{session} done. You're still behind. {today} of {goal} so far.
Okay, {session}. But you still need {left} today.
That's {session}. You're at {pace} of where you should be by now.
Logged {session}. Your daily average is {avg}. Your goal is {goal}.
!{session}. Still behind. At this rate you'll be {week_short} short this week.
!{session}, and you're still behind. Your {exam} is {exam_in}.

[stop_good]
Solid {session}. {left} to go today.
Nice, {session}. You're on pace.
{session} logged. Good. Keep that rhythm.
Good session. {today} done today.

[stop_close]
You're {left} from your goal. Don't stop now.
Really? Only {left} left. Finish it.
!{left} from your goal and you're stopping? Come on.

[stop_impossible]
Even if you studied nonstop until {bedtime}, you won't hit your goal today. Do as much as you can.
Today's goal is out of reach now. Don't let tomorrow go the same way.
!You can't hit today's goal anymore. That's how failing starts, one day at a time.

[stop_goal_done]
Goal done. {today} today. Enjoy your break.
That's the goal. {streak} day streak. Rest, you earned it.

[stop_after_goal]
Bonus {session} logged. {today} total today.
Extra {session}. That's how you get ahead.

[cancelled]
Cancelled. That didn't count.

# Sometimes added after a stop line: motivation, facts, or doom.
[kicker]
Fun fact. Spreading your studying over several days beats cramming for remembering things long term.
Fun fact. Testing yourself works better than re-reading your notes.
Fun fact. After a distraction it can take over 20 minutes to fully get back into a task.
Fun fact. Your brain locks in what you learned while you sleep, so all-nighters backfire.
Small steps every day beat one big panic the night before.
The work doesn't get easier by waiting. It just gets closer to the deadline.
Future you is watching. Don't embarrass them.
Your {exam} is {exam_in}. Every day counts now.
!Keep this up and you'll be retaking this class.
!At this rate you'll end up homeless. Go back and study.
!Imagine explaining this to your parents.
!Somewhere, someone with the same goals as you is studying right now.
!Your competition is studying. You're not.

# Nags when you're on the laptop but not studying. 1 = an hour or more, 2 = two hours, 3 = three or more.
[nag_1]
It's been {offtask}. Time to get back to work.
{offtask} on the laptop and no studying. Let's change that.
You've been off task for {offtask}. You still need {left} today.
Hey. {offtask} of not studying. Start a session.
!{offtask} gone. Get back to work or you'll fail.

[nag_2]
{offtask} of not studying. Get back to work.
It's been {offtask}. Your goal isn't going to do itself.
{offtask} off task. You still need {left} today.
!It's been {offtask}. At this rate you'll be {week_short} short this week.
!{offtask} wasted. Your {exam} is {exam_in}.

[nag_3]
It's been {offtask}. Get back to work or you'll fail.
{offtask} of not studying. This is how semesters get lost.
!It's been {offtask}. Get back to work or enjoy being homeless.
!{offtask}. You're not taking a break anymore, you're just failing slowly.
!{offtask} gone. At this rate you'll be {month_short} behind in a month.
!{offtask} of games and videos. Your future self is begging you to stop.

# Nag when today's goal can no longer be reached before bedtime.
[nag_lost]
You can't hit today's goal anymore. Do an hour anyway, it still counts.
Today's goal is out of reach. Don't let tomorrow go the same way.
!It's been {offtask}, and today's goal is gone. Keep this up and so is the semester.

# Nag when it's past your bedtime.
[nag_late]
It's past your bedtime. Sleep now, study tomorrow.
It's after {bedtime}. Go to sleep, a tired brain doesn't remember much.
!It's past {bedtime} and you're still on here. Go to sleep.

[auto_stop_warn]
Still studying? Touch the mouse or I'll stop the timer in 10 minutes.

[auto_stopped]
No keyboard or mouse for a long time, so I stopped your session at {end}.

[slept]
Your laptop went to sleep, so I stopped your session at {end}.

[recovered]
The app closed during a session, so I logged it until {end}.

[resumed]
Welcome back. Your session is still running.
";
    }
}
