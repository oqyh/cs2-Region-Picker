---
<h2 align="center">.:[ Community | Support ]:.</h2>
<p align="center">
  <a href="https://discord.com/invite/U7AuQhu">
    <img src="https://img.shields.io/badge/Discord-Join-5865F2?style=for-the-badge&logo=discord&logoColor=white" />
  </a>
  <a href="https://ko-fi.com/goldkingz">
    <img src="https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=kofi&logoColor=white" />
  </a>
</p>

---

# CS2 Region Picker (1.0.0)

Block CS2 matchmaking regions you don't want, straight from a world map. Uses Windows Firewallp

<img width="800" height="737" alt="CS2RegionPicker" src="https://github.com/user-attachments/assets/87e126b8-f1b8-4511-adae-2afdcfdfbd54" />

---

## How It Works

CS2 routes you through **Valve's SDR relays** — servers around the world that act
as the on-ramps into matchmaking.

1. The app fetches Valve's public relay list.
2. You click the regions you don't want.
3. It blocks those relay IPs in Windows Firewall.
4. Matchmaking can't reach them, so it puts you somewhere else.

Valve rotates relay IPs often — the app checks on every launch and rebuilds your
rules automatically. You never have to redo anything.

---

## How To Use

1. Run **CS2RegionPicker.exe** (needs admin — it writes firewall rules).
2. Click regions on the map to mark them:
   🟢 allowed · 🟣 blocked · 🔵 not applied yet
3. Hit **▶ Apply**.
4. Close the app. The block stays.

**Set your max ping in CS2** — the app tells you the exact number:

```
mm_dedicated_search_maxping <number>
```

If it's lower than your slowest allowed region, matchmaking will never find a
server.

**Using an antivirus firewall?** (Bitdefender, Kaspersky, ESET…) It can override
Windows Firewall and ignore the rules. The app warns you — disable its *firewall
module* to fix it.

**To undo everything:** Mark All → Apply.

---

## Where are my settings saved?

`%LocalAppData%/CS2RegionPicker/`

---

## Q & A
 
Have a question — VAC safety, undoing the block, antivirus firewalls, matchmaking
not finding a server?
 
👉 **[Browse the Q&A Discussions](../../discussions/categories/q-a)**
 
---
## 📜 Changelog

<details>
<summary><b>📋 View Version History</b> (Click to expand 🔽)</summary>

### [1.0.0]
- Initial Release

</details>
