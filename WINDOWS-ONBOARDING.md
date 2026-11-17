# Buntly — setup on Windows

Hold Right Ctrl, talk, let go. Your words appear wherever the cursor already was. Everything happens on this PC. Audio is never saved. The only time Buntly uses the internet is the one-time speech-model download.

Version 1.0.0 · 64-bit Windows 10 or Windows 11

---

## 1. Install

Run `Buntly-Setup.exe`.

Windows will say it protected your PC. That is expected. This copy is not signed with a paid certificate, so Windows has not vouched for it.

1. Click **More info**.
2. Click **Run anyway**.

You only do this once. If you would rather not run unsigned software, stop here. That is a fair choice.

Security software may also warn. Buntly has to notice the dictation key while another program is in front. It does not record your other keys.

The installer does not ask for an administrator password. It adds Buntly to the Start menu.

## 2. The checklist

Open Buntly. A short checklist appears. When it is finished, Buntly lives in the **system tray** — the row of small icons at the bottom-right, next to the clock. Click the ^ if you do not see it. That tray icon is the Windows version of a Mac menu bar. There is no window you have to keep open.

| Row | What to do |
|---|---|
| **Microphone** | Allow desktop apps to use the microphone. The checklist opens that settings page. |
| **Dictation key** | **Right Ctrl** is already chosen. Hold it to talk. You can pick another key. The **Fn** key on most laptops never reaches Windows, so Buntly cannot use it. |
| **Speech model** | One download from Hugging Face, about **1.6 GB**. Internet is needed only for this. After it finishes, Buntly works offline. |

Do not try to dictate until the model has finished and the tray says Ready.

## 3. Use it

- **Hold Right Ctrl, talk, release.** The words are pasted at the cursor.
- **Hold longer than about two seconds** and it locks on, so you can let go of the key. Tap Right Ctrl again to finish.
- **Double-tap** Right Ctrl to lock it on the same way.
- **Esc** cancels. Nothing is pasted.
- While it is locked on, the pill has an **✕**. That cancels the paste and shows the text with a **Copy** button for 9 seconds.

A small black pill sits at the bottom of the screen while Buntly is listening. It does not show words. If a take is too quiet, or nothing was recognized, the pill flashes a mark and then goes back to a small bead. A successful paste does not announce itself. The words in the document are the confirmation.

The pill can move to the top of the screen from the tray menu → Settings.

## 4. History

Tray icon → **History**. Windows asks you to confirm it is you, with Windows Hello, a PIN, or your password. That is the lock in front of the list.

The list is text only. **Audio is never stored.** You can clear history, turn it off, or keep 7, 30, or 90 days of it, in Settings.

Settings is also where you add words Buntly keeps mishearing. A row replaces that whole word only.

## 5. Uninstall

Windows Settings → Apps → Buntly → Uninstall.

That removes the program. The speech model and history stay in `%LOCALAPPDATA%\Buntly` until you delete that folder.

---

## Rough edges

- The first transcription after you open Buntly is slower while the model warms up. Later ones are quicker. How quick depends on the laptop.
- A word or two is sometimes ignored as too quiet. Say a sentence.
- A password field, or a program you opened with **Run as administrator**, will not accept the paste. The text stays on your clipboard. Press Ctrl+V yourself.
- There is no phone version.

---

Built by Nithin. There is no support desk and no telemetry. If something breaks, tell him. He will not know otherwise.
